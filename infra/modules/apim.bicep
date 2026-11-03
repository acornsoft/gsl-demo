// API Management, Consumption tier. No fixed fee. First 1M calls per subscription per month included.
// Do not change the SKU. Developer tier is about $48/month and is not free.
param location string
param tags object
param name string
param publisherEmail string
param publisherName string

resource apim 'Microsoft.ApiManagement/service@2024-05-01' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: 'Consumption'
    capacity: 0
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: publisherName
  }
}

output gatewayUrl string = apim.properties.gatewayUrl
output principalId string = apim.identity.principalId
