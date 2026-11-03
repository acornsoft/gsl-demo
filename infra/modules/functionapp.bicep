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

output name string = site.name
output principalId string = site.identity.principalId
