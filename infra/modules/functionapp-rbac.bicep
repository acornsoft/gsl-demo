// Storage data roles for the Function App host. Scoped to its own storage account only.
param principalId string
param storageAccountName string

var roles = {
  blobDataOwner: 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
  queueDataContributor: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
  tableDataContributor: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
}

resource sa 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource ra 'Microsoft.Authorization/roleAssignments@2022-04-01' = [for r in items(roles): {
  name: guid(sa.id, principalId, r.value)
  scope: sa
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', r.value)
  }
}]
