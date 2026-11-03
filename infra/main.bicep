// GSL demo landing zone (fictional Gotham Savings and Loan practice model).
// One subscription, ONE dedicated tagged resource group. Consumption/free tiers only.
// Names follow CAF: <abbr>-<workload>-<env>-<region>-<instance>
// https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations
// No secrets. Parameters hold only names, IDs and an alert email.
targetScope = 'subscription'

@description('Environment code used in names, e.g. dev.')
@maxLength(4)
param env string = 'dev'

@description('Primary region.')
param location string = 'eastus2'

@description('Short region code for length-limited names (Key Vault, Storage).')
param regionShort string = 'eus2'

@description('Instance number.')
param instance string = '001'

param ownerTag string = 'gsl-cloud-platform'
param costCenterTag string = 'cc-gsl-demo-placeholder'

@description('Email for budget and alert notices.')
param alertEmail string

param apimPublisherEmail string = alertEmail
param budgetStartDate string = '${utcNow('yyyy-MM')}-01'

@description('Monthly budget ceiling in USD. Target spend is about $12.')
param budgetAmount int = 50

@description('Log Analytics daily cap in GB (0.1 GB/day stays inside the 5 GB/month free grant).')
param logDailyCapGb string = '0.1'

@allowed([ 'Basic', 'Standard' ])
@description('Basic = queues only. Switch to Standard when ADO 2791/2793 need topics.')
param serviceBusSku string = 'Basic'

@description('Deploy subscription policy definitions/assignments (admin run only; CI leaves false).')
param deployGovernance bool = false

var workload = 'gsl'
var sfx = '${workload}-${env}-${location}-${instance}'
var names = {
  rg: 'rg-${sfx}'
  log: 'log-${sfx}'
  appi: 'appi-${sfx}'
  kv: 'kv-${workload}-${env}-${regionShort}-${instance}'
  st: toLower('st${workload}${env}${regionShort}${instance}')
  asp: 'asp-${sfx}'
  func: 'func-${sfx}'
  apim: 'apim-${sfx}'
  sbns: 'sbns-${sfx}'
  sbq: 'sbq-${workload}-${env}-${instance}'
  ag: 'ag-${workload}-${env}-${instance}'
  budget: 'budget-${workload}-${env}-${instance}'
}

var tags = {
  workload: workload
  env: env
  owner: ownerTag
  costCenter: costCenterTag
  dataClass: 'synthetic'
  managedBy: 'bicep-azd'
  'azd-env-name': env
}

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: names.rg
  location: location
  tags: tags
}

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  scope: rg
  params: {
    location: location
    tags: tags
    logAnalyticsName: names.log
    appInsightsName: names.appi
    dailyCapGb: logDailyCapGb
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  scope: rg
  params: {
    location: location
    tags: tags
    name: names.kv
  }
}

module actionGroup 'modules/actiongroup.bicep' = {
  name: 'actiongroup'
  scope: rg
  params: {
    tags: tags
    name: names.ag
    shortName: 'gsldevcost'
    alertEmail: alertEmail
  }
}

module functionApp 'modules/functionapp.bicep' = {
  name: 'functionapp'
  scope: rg
  params: {
    location: location
    tags: tags
    planName: names.asp
    functionAppName: names.func
    storageAccountName: names.st
    appInsightsName: monitoring.outputs.appInsightsName
    appInsightsResourceGroup: rg.name
    keyVaultName: keyVault.outputs.name
    keyVaultResourceGroup: rg.name
  }
}

module serviceBus 'modules/servicebus.bicep' = {
  name: 'servicebus'
  scope: rg
  params: {
    location: location
    tags: tags
    name: names.sbns
    skuName: serviceBusSku
    queueNames: [ names.sbq ]
  }
}

module apim 'modules/apim.bicep' = {
  name: 'apim'
  scope: rg
  params: {
    location: location
    tags: tags
    name: names.apim
    publisherEmail: apimPublisherEmail
    publisherName: 'GSL Demo (fictional)'
  }
}

// Budget is a subscription-scope cost object (no Azure resource is created outside the RG).
module budget 'modules/budget.bicep' = {
  name: 'budget'
  params: {
    name: names.budget
    amount: budgetAmount
    startDate: budgetStartDate
    actionGroupId: actionGroup.outputs.id
    alertEmail: alertEmail
  }
}

// Includes the deny policy for any non-Consumption APIM SKU.
module policy 'modules/policy.bicep' = if (deployGovernance) {
  name: 'policy'
  params: {
    allowedLocations: [ location ]
  }
}

output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_RESOURCE_GROUP string = rg.name
output AZURE_FUNCTION_NAME string = functionApp.outputs.name
output AZURE_KEY_VAULT_NAME string = keyVault.outputs.name
output SERVICE_BUS_NAMESPACE string = serviceBus.outputs.name
output APIM_GATEWAY_URL string = apim.outputs.gatewayUrl
