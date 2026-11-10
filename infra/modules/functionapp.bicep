// Azure Functions on the Flex Consumption plan (FC1). Scales to zero. No always-ready instances.
// Identity-based storage and App Insights auth. No connection strings with keys.
param location string
param tags object
param planName string
param functionAppName string

@maxLength(24)
param storageAccountName string
param appInsightsName string
param appInsightsResourceGroup string
param keyVaultName string
param keyVaultResourceGroup string

@description('Service Bus namespace for identity-based connection (no connection string).')
param serviceBusNamespaceName string
param serviceBusQueueName string

@description('Entra app registration client ID (API audience) for Easy Auth. Not a secret. Empty skips auth config.')
param entraApiClientId string = ''

var serviceBusFqdn = '${serviceBusNamespaceName}.servicebus.windows.net'
var serviceBusDataSender = '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39'
var serviceBusDataReceiver = '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0'

@allowed([
  'dotnet-isolated'
  'python'
  'node'
  'java'
  'powerShell'
])
param runtimeName string = 'dotnet-isolated'
param runtimeVersion string = '8.0'

@minValue(40)
@maxValue(1000)
@description('Hard ceiling on scale-out. Keep low for the demo to cap runaway cost.')
param maximumInstanceCount int = 40

@allowed([
  512
  2048
  4096
])
param instanceMemoryMB int = 512

var deployContainer = 'app-package'

module fnStorage 'storage.bicep' = {
  name: 'fn-storage'
  params: {
    location: location
    tags: tags
    name: storageAccountName
    containers: [
      deployContainer
    ]
  }
}

resource appi 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
  scope: resourceGroup(appInsightsResourceGroup)
}

resource plan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  tags: tags
  kind: 'functionapp'
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource site 'Microsoft.Web/sites@2024-04-01' = {
  name: functionAppName
  location: location
  tags: union(tags, { 'azd-service-name': 'api' })
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        {
          name: 'AzureWebJobsStorage__accountName'
          value: fnStorage.outputs.name
        }
        {
          name: 'AzureWebJobsStorage__credential'
          value: 'managedidentity'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appi.properties.ConnectionString
        }
        {
          name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING'
          value: 'Authorization=AAD'
        }
        {
          name: 'KEY_VAULT_URI'
          value: 'https://${keyVaultName}${environment().suffixes.keyvaultDns}/'
        }
        {
          name: 'ServiceBusConnection__fullyQualifiedNamespace'
          value: serviceBusFqdn
        }
        {
          name: 'ServiceBusConnection__credential'
          value: 'managedidentity'
        }
        {
          name: 'SERVICEBUS_QUEUE_NAME'
          value: serviceBusQueueName
        }
      ]
    }
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${fnStorage.outputs.blobEndpoint}${deployContainer}'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: maximumInstanceCount
        instanceMemoryMB: instanceMemoryMB
      }
      runtime: {
        name: runtimeName
        version: runtimeVersion
      }
    }
  }
}


// Entra ID Easy Auth (authsettingsV2). Bearer-token validation only: no client secret.
// Unauthenticated calls get 401. /api/health is excluded and stays anonymous.
resource authSettings 'Microsoft.Web/sites/config@2024-04-01' = if (!empty(entraApiClientId)) {
  name: 'authsettingsV2'
  parent: site
  properties: {
    platform: {
      enabled: true
    }
    globalValidation: {
      requireAuthentication: true
      unauthenticatedClientAction: 'Return401'
      excludedPaths: [
        '/api/health'
      ]
    }
    httpSettings: {
      requireHttps: true
    }
    identityProviders: {
      azureActiveDirectory: {
        enabled: true
        registration: {
          clientId: entraApiClientId
          openIdIssuer: '${environment().authentication.loginEndpoint}${tenant().tenantId}/v2.0'
        }
        validation: {
          allowedAudiences: [
            'api://${entraApiClientId}'
            entraApiClientId
          ]
        }
      }
    }
  }
}

// Least-privilege data-plane roles for the Function App identity.
module rbac 'functionapp-rbac.bicep' = {
  name: 'fn-rbac'
  params: {
    principalId: site.identity.principalId
    storageAccountName: fnStorage.outputs.name
  }
}

module appiRbac 'role-on-appinsights.bicep' = {
  name: 'fn-appi-rbac'
  scope: resourceGroup(appInsightsResourceGroup)
  params: {
    principalId: site.identity.principalId
    appInsightsName: appInsightsName
  }
}

module kvRbac 'role-on-keyvault.bicep' = {
  name: 'fn-kv-rbac'
  scope: resourceGroup(keyVaultResourceGroup)
  params: {
    principalId: site.identity.principalId
    keyVaultName: keyVaultName
  }
}

resource sb 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' existing = {
  name: serviceBusNamespaceName
}

resource sbRoles 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for role in [serviceBusDataSender, serviceBusDataReceiver]: {
  name: guid(sb.id, site.id, role)
  scope: sb
  properties: {
    principalId: site.identity.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', role)
    description: 'Function App identity: Service Bus data plane (no connection string)'
  }
}]

output name string = site.name
output principalId string = site.identity.principalId
