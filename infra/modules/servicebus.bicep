// Service Bus namespace, Basic tier (queues only, cheapest: about $0.05 per million operations).
// NOTE: Basic has no topics/subscriptions. Move sku to Standard (~$10/month base) when
// ADO 2791 / 2793 need topics. Local (SAS) auth is disabled; use Entra RBAC only.
param location string
param tags object
param name string
param queueNames array = []
@allowed([ 'Basic', 'Standard' ])
param skuName string = 'Basic'

resource ns 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: name
  location: location
  tags: tags
  sku: {
    name: skuName
    tier: skuName
  }
  properties: {
    disableLocalAuth: true
    minimumTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

resource q 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = [for n in queueNames: {
  parent: ns
  name: n
  properties: {
    maxDeliveryCount: 10
    deadLetteringOnMessageExpiration: true
  }
}]

output name string = ns.name
output id string = ns.id
