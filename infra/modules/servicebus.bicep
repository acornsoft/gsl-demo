// Service Bus namespace. Standard tier (topics and subscriptions); Basic is queues only.
// Local (SAS) auth is disabled; use Entra RBAC only.
param location string
param tags object
param name string
param queueNames array = []
@allowed([ 'Basic', 'Standard' ])
param skuName string = 'Basic'

@description('Create the Message Explorer (LFME) synthetic-traffic queue, topic and subscription. Topic and subscription need Standard.')
param lfmeSynthEntities bool = false

@description('Object ID granted Azure Service Bus Data Sender on the LFME queue and topic only. Empty = no role assignment.')
param lfmeSenderPrincipalId string = ''

@allowed([ 'User', 'ServicePrincipal', 'Group' ])
param lfmeSenderPrincipalType string = 'User'

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
    // Pinned to what Basic gave it (P14D), so the move to Standard (default: no expiry) leaves it unchanged.
    defaultMessageTimeToLive: 'P14D'
  }
}]

// LFME synthetic traffic (fictional data only). Short TTL so nothing piles up; expired messages dead-letter.
// Rollback: delete only these three entities; the namespace and sbq-gsl-dev-001 are untouched.
var lfme = {
  queue: 'sbq-lfme-synth-dev-001'
  topic: 'sbt-lfme-synth-dev-001'
  subscription: 'sbs-lfme-synth-all'
  ttl: 'PT1H'
}
var lfmeTopic = lfmeSynthEntities && skuName == 'Standard'

resource lfmeQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = if (lfmeSynthEntities) {
  parent: ns
  name: lfme.queue
  properties: {
    defaultMessageTimeToLive: lfme.ttl
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 10
  }
}

resource lfmeTopicRes 'Microsoft.ServiceBus/namespaces/topics@2022-10-01-preview' = if (lfmeTopic) {
  parent: ns
  name: lfme.topic
  properties: {
    defaultMessageTimeToLive: lfme.ttl
  }
}

resource lfmeSub 'Microsoft.ServiceBus/namespaces/topics/subscriptions@2022-10-01-preview' = if (lfmeTopic) {
  parent: lfmeTopicRes
  name: lfme.subscription
  properties: {
    defaultMessageTimeToLive: lfme.ttl
    deadLetteringOnMessageExpiration: true
    maxDeliveryCount: 10
  }
}

// Azure Service Bus Data Sender, scoped to the LFME queue and topic only (never the namespace).
var dataSender = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39')
var grantSender = lfmeSynthEntities && !empty(lfmeSenderPrincipalId)

resource lfmeQueueSender 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (grantSender) {
  name: guid(ns.id, lfme.queue, lfmeSenderPrincipalId, 'sender')
  scope: lfmeQueue
  properties: {
    principalId: lfmeSenderPrincipalId
    principalType: lfmeSenderPrincipalType
    roleDefinitionId: dataSender
  }
}

resource lfmeTopicSender 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (grantSender && lfmeTopic) {
  name: guid(ns.id, lfme.topic, lfmeSenderPrincipalId, 'sender')
  scope: lfmeTopicRes
  properties: {
    principalId: lfmeSenderPrincipalId
    principalType: lfmeSenderPrincipalType
    roleDefinitionId: dataSender
  }
}

output name string = ns.name
output id string = ns.id
