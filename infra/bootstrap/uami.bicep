// User-assigned managed identity with GitHub OIDC federated credentials.
param location string
param tags object
param name string
param subjects array

resource id 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: name
  location: location
  tags: tags
}

// Federated credentials must be created one at a time.
@batchSize(1)
resource fic 'Microsoft.ManagedIdentity/userAssignedIdentities/federatedIdentityCredentials@2023-01-31' = [for (s, i) in subjects: {
  parent: id
  name: 'github-${i}'
  properties: {
    issuer: 'https://token.actions.githubusercontent.com'
    subject: s
    audiences: [
      'api://AzureADTokenExchange'
    ]
  }
}]

output principalId string = id.properties.principalId
output clientId string = id.properties.clientId
