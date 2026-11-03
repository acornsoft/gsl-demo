// GSL demo landing zone (fictional Gotham Savings and Loan practice model).
// Resource-group scope only. Consumption/free tiers only.
// Names follow CAF: <abbr>-<workload>-<env>-<region>-<instance>
// https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations
// No secrets. Parameters hold only names, IDs and an alert email.
targetScope = 'resourceGroup'
// Deployed by the pipeline with `az deployment group` into rg-gsl-dev-eastus-001.
// The RG, budget, policy and pipeline identity are created by infra/bootstrap (one-time Owner run).

@description('Environment code used in names, e.g. dev.')
@maxLength(4)
param env string = 'dev'

@description('Primary region.')
param location string = resourceGroup().location

@description('Short region code for length-limited names (Key Vault, Storage).')
param regionShort string = 'eus'

@description('Instance number.')
param instance string = '001'

param ownerTag string = 'gsl-cloud-platform'
param costCenterTag string = 'cc-gsl-demo-placeholder'


@description('APIM publisher email (shown on the APIM instance).')
param apimPublisherEmail string

@description('Log Analytics daily cap in GB (0.1 GB/day stays inside the 5 GB/month free grant).')
param logDailyCapGb string = '0.1'

@allowed([ 'Basic', 'Standard' ])
@description('Basic = queues only. Switch to Standard when ADO 2791/2793 need topics.')
param serviceBusSku string = 'Basic'

var workload = 'gsl'
var sfx = '${workload}-${env}-${location}-${instance}'
var names = {
  log: 'log-${sfx}'
  appi: 'appi-${sfx}'
  kv: 'kv-${workload}-${env}-${regionShort}-${instance}'
  st: toLower('st${workload}${env}${regionShort}${instance}')
  asp: 'asp-${sfx}'
  func: 'func-${sfx}'
  apim: 'apim-${sfx}'
  sbns: 'sbns-${sfx}'
  sbq: 'sbq-${workload}-${env}-${instance}'
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

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
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
  params: {
    location: location
    tags: tags
    name: names.kv
  }
}

module functionApp 'modules/functionapp.bicep' = {
  name: 'functionapp'
  params: {
    location: location
    tags: tags
    planName: names.asp
    functionAppName: names.func
    storageAccountName: names.st
    appInsightsName: monitoring.outputs.appInsightsName
    appInsightsResourceGroup: resourceGroup().name
    keyVaultName: keyVault.outputs.name
    keyVaultResourceGroup: resourceGroup().name
  }
}

module serviceBus 'modules/servicebus.bicep' = {
  name: 'servicebus'
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
  params: {
    location: location
    tags: tags
    name: names.apim
    publisherEmail: apimPublisherEmail
    publisherName: 'GSL Demo (fictional)'
  }
}

output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_RESOURCE_GROUP string = resourceGroup().name
output AZURE_FUNCTION_NAME string = functionApp.outputs.name
output AZURE_KEY_VAULT_NAME string = keyVault.outputs.name
output SERVICE_BUS_NAMESPACE string = serviceBus.outputs.name
output APIM_GATEWAY_URL string = apim.outputs.gatewayUrl
