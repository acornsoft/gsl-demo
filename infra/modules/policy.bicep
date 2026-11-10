// GSL policy ASSIGNMENTS, scoped to the GSL resource group only (never the subscription).
// Custom definitions live at subscription scope in policy-defs.bicep.
// Deployed by the Owner bootstrap when deployGovernance = true.
targetScope = 'resourceGroup'

@description('Resource ID of the serverless-plans-only policy definition.')
param planSkuDefinitionId string

param allowedLocations array

@description('Tags every resource group must carry.')
param requiredRgTags array = [
  'workload'
  'env'
  'owner'
  'costCenter'
]

@description('Expensive resource types blocked in the GSL resource group.')
param blockedResourceTypes array = [
  'Microsoft.Network/azureFirewalls'
  'Microsoft.Network/applicationGateways'
  'Microsoft.Network/virtualNetworkGateways'
  'Microsoft.Network/expressRouteCircuits'
  'Microsoft.Network/ddosProtectionPlans'
  'Microsoft.Network/bastionHosts'
  'Microsoft.Network/natGateways'
  'Microsoft.ContainerService/managedClusters'
  'Microsoft.Sql/managedInstances'
  'Microsoft.Compute/virtualMachines'
  'Microsoft.Compute/virtualMachineScaleSets'
  'Microsoft.Databricks/workspaces'
  'Microsoft.Synapse/workspaces'
]

var builtIn = {
  allowedLocations: 'e56962a6-4747-49cd-b67b-bf8b01975c4c'
  allowedRgLocations: 'e765b5de-1225-4ba3-bd56-1ac6695af988'
  requireRgTag: '96670d01-0a4d-4649-9c89-2d3abc0a5025'
  notAllowedTypes: '6c112d4e-5bc7-47ae-a041-ea2d9dccd749'
  storageNoPublicBlob: '4fa4b6c0-31ca-4c0d-b10d-24b96f62a751'
  storageNoSharedKey: '8c6a50c6-9ffd-4ae7-986f-5fa6111f9a54'
  kvRbac: '12d4fa5e-1f9f-4c21-97a9-b99b3c6611b5'
  kvSoftDelete: '1e66c121-a66a-4b1f-9b83-0fd99bf0fc2d'
}

func defId(id string) string => tenantResourceId('Microsoft.Authorization/policyDefinitions', id)

resource paLocations 'Microsoft.Authorization/policyAssignments@2024-04-01' = {
  name: 'gsl-demo-allowed-locations'
  properties: {
    displayName: 'GSL demo: allowed locations'
    policyDefinitionId: defId(builtIn.allowedLocations)
    definitionVersion: '1.*.*' // pinned to the value Azure stores
    parameters: {
      listOfAllowedLocations: {
        value: allowedLocations
      }
    }
  }
}

resource paRgLocations 'Microsoft.Authorization/policyAssignments@2024-04-01' = {
  name: 'gsl-demo-allowed-rg-locations'
  properties: {
    displayName: 'GSL demo: allowed resource group locations'
    policyDefinitionId: defId(builtIn.allowedRgLocations)
    definitionVersion: '1.*.*' // pinned to the value Azure stores
    parameters: {
      listOfAllowedLocations: {
        value: allowedLocations
      }
    }
  }
}

resource paRgTags 'Microsoft.Authorization/policyAssignments@2024-04-01' = [for t in requiredRgTags: {
  name: 'gsl-demo-rg-tag-${toLower(t)}'
  properties: {
    displayName: 'GSL demo: require tag ${t} on the GSL resource group'
    policyDefinitionId: defId(builtIn.requireRgTag)
    definitionVersion: '1.*.*' // pinned to the value Azure stores
    parameters: {
      tagName: {
        value: t
      }
    }
  }
}]

resource paBlockedTypes 'Microsoft.Authorization/policyAssignments@2024-04-01' = {
  name: 'gsl-demo-blocked-types'
  properties: {
    displayName: 'GSL demo: block expensive resource types'
    policyDefinitionId: defId(builtIn.notAllowedTypes)
    definitionVersion: '2.*.*' // pinned to the value Azure stores
    parameters: {
      listOfResourceTypesNotAllowed: {
        value: blockedResourceTypes
      }
    }
  }
}

resource paStoragePublic 'Microsoft.Authorization/policyAssignments@2024-04-01' = {
  name: 'gsl-demo-st-no-public'
  properties: {
    displayName: 'GSL demo: storage public access disallowed'
    policyDefinitionId: defId(builtIn.storageNoPublicBlob)
    definitionVersion: '3.*.*' // pinned to the value Azure stores
    parameters: {
      effect: {
        value: 'Deny'
      }
    }
  }
}

resource paStorageSharedKey 'Microsoft.Authorization/policyAssignments@2024-04-01' = {
  name: 'gsl-demo-st-no-sharedkey'
  properties: {
    displayName: 'GSL demo: storage shared key access prevented'
    policyDefinitionId: defId(builtIn.storageNoSharedKey)
    definitionVersion: '2.*.*' // pinned to the value Azure stores
    parameters: {
      effect: {
        value: 'Deny'
      }
    }
  }
}

// Audit only (default effect) for Key Vault rules.
resource paKvRbac 'Microsoft.Authorization/policyAssignments@2024-04-01' = {
  name: 'gsl-demo-kv-rbac'
  properties: {
    displayName: 'GSL demo: Key Vault uses RBAC (audit)'
    policyDefinitionId: defId(builtIn.kvRbac)
    definitionVersion: '1.*.*' // pinned to the value Azure stores
  }
}

resource paKvSoftDelete 'Microsoft.Authorization/policyAssignments@2024-04-01' = {
  name: 'gsl-demo-kv-softdelete'
  properties: {
    displayName: 'GSL demo: Key Vault soft delete (audit)'
    policyDefinitionId: defId(builtIn.kvSoftDelete)
    definitionVersion: '3.*.*' // pinned to the value Azure stores
  }
}

resource paPlanSku 'Microsoft.Authorization/policyAssignments@2024-04-01' = {
  name: 'gsl-demo-serverless-plans'
  properties: {
    displayName: 'GSL demo: App Service plans must be Flex Consumption or Consumption'
    policyDefinitionId: planSkuDefinitionId
    definitionVersion: '1.*.*' // pinned to the value Azure stores
  }
}
