// Subscription cost budget: actual alerts at 50/80/100% plus a forecast alert at 100%.
targetScope = 'subscription'

param name string
param amount int
@description('First day of a month, yyyy-MM-dd.')
param startDate string
param actionGroupId string
param alertEmail string
param actualThresholds array = [ 50, 80, 100 ]
param forecastThreshold int = 100

var actual = reduce(actualThresholds, {}, (acc, t) => union(acc, {
  'actual${t}': {
    enabled: true
    operator: 'GreaterThanOrEqualTo'
    threshold: t
    thresholdType: 'Actual'
    contactEmails: [ alertEmail ]
    contactGroups: [ actionGroupId ]
  }
}))

resource budget 'Microsoft.Consumption/budgets@2023-11-01' = {
  name: name
  properties: {
    category: 'Cost'
    amount: amount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: startDate
    }
    notifications: union(actual, {
      'forecast${forecastThreshold}': {
        enabled: true
        operator: 'GreaterThanOrEqualTo'
        threshold: forecastThreshold
        thresholdType: 'Forecasted'
        contactEmails: [ alertEmail ]
        contactGroups: [ actionGroupId ]
      }
    })
  }
}
