// Monitoring Metrics Publisher on App Insights, so the app can send telemetry with Entra auth.
param principalId string
param appInsightsName string

var monitoringMetricsPublisher = '3913510d-42f4-4e42-8a64-420c390055eb'

resource appi 'Microsoft.Insights/components@2020-02-02' existing = {
  name: appInsightsName
}

resource ra 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(appi.id, principalId, monitoringMetricsPublisher)
  scope: appi
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', monitoringMetricsPublisher)
  }
}
