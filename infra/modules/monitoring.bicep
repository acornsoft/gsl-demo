// Log Analytics with a daily cap, and workspace-based Application Insights.
param location string
param tags object
param logAnalyticsName string
param appInsightsName string

@description('Daily ingestion cap in GB, as a string so decimals work (for example "0.1").')
param dailyCapGb string = '0.1'

@description('Interactive retention in days. 30 is inside the included retention.')
param retentionDays int = 30

resource law 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionDays
    workspaceCapping: {
      dailyQuotaGb: json(dailyCapGb)
    }
    features: {
      disableLocalAuth: true
    }
  }
}

resource appi 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: law.id
    IngestionMode: 'LogAnalytics'
    DisableLocalAuth: true
    SamplingPercentage: 50
  }
}

output logAnalyticsId string = law.id
output appInsightsName string = appi.name
output appInsightsId string = appi.id
