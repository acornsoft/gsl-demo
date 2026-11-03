// Action group for budget and cost alerts. Email only. No webhooks, no secrets.
param tags object
param name string

@maxLength(12)
param shortName string
param alertEmail string

resource ag 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: name
  location: 'global'
  tags: tags
  properties: {
    groupShortName: shortName
    enabled: true
    emailReceivers: [
      {
        name: 'cost-owner'
        emailAddress: alertEmail
        useCommonAlertSchema: true
      }
    ]
  }
}

output id string = ag.id
