// ONE-TIME OWNER BOOTSTRAP (subscription scope). Everything subscription-scoped lives here:
//   - resource group rg-gsl-dev-eastus-001 (tagged)
//   - pipeline identity id-gsl-dev-eastus-001 + GitHub OIDC federated credentials
//   - RG-scoped role assignments for the pipeline (Contributor + conditioned RBAC admin)
//   - action group + $50 budget (50/80/100% actual, 100% forecast)
//   - policy definitions (subscription) + policy ASSIGNMENTS scoped to the GSL RG only
// Run by a subscription Owner after `az account show` confirms ca1fc4ed-...:
//   az deployment sub create -l eastus -f infra/bootstrap/github-oidc.bicep -p infra/bootstrap/bootstrap.parameters.json
// No client secret exists. GitHub exchanges its OIDC token for an Entra token.
targetScope = 'subscription'

param location string = 'eastus'
param env string = 'dev'
param instance string = '001'
param githubOrg string = 'acornsoft'
param githubRepo string = 'gsl-demo'
@description('GitHub numeric owner ID. GitHub now sends OIDC subjects as repo:<owner>@<ownerId>/<repo>@<repoId>:...')
param githubOwnerId string = '3299918'
@description('GitHub numeric repository ID.')
param githubRepoId string = '1444246506'
param githubEnvironments array = [
  'demo-preview'
  'demo'
]
@description('Email for budget and alert notices.')
param alertEmail string
@description('Monthly budget ceiling in USD. Target spend is about $12.')
param budgetAmount int = 50
param budgetStartDate string = '${utcNow('yyyy-MM')}-01'
param deployGovernance bool = true
param ownerTag string = 'gsl-cloud-platform'
param costCenterTag string = 'cc-gsl-demo-placeholder'

var workload = 'gsl'
var sfx = '${workload}-${env}-${location}-${instance}'
var tags = {
  workload: workload
  env: env
  owner: ownerTag
  costCenter: costCenterTag
  dataClass: 'synthetic'
  managedBy: 'bicep-bootstrap'
  'azd-env-name': env
}

// The only roles the pipeline may hand out (app data-plane only).
var assignableRoleIds = [
  '4633458b-17de-408a-b874-0445c86b69e6' // Key Vault Secrets User
  'b7e6dc6d-f1e8-4753-8033-0f276bb0955b' // Storage Blob Data Owner
  '974c5e8b-45b9-4653-ba55-5f855dd0fb88' // Storage Queue Data Contributor
  '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3' // Storage Table Data Contributor
  '3913510d-42f4-4e42-8a64-420c390055eb' // Monitoring Metrics Publisher
]

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-${sfx}'
  location: location
  tags: tags
}

module uami 'uami.bicep' = {
  name: 'github-uami'
  scope: rg
  params: {
    location: location
    tags: tags
    name: 'id-${sfx}'
    subjects: [for e in githubEnvironments: 'repo:${githubOrg}@${githubOwnerId}/${githubRepo}@${githubRepoId}:environment:${e}']
  }
}

module rbac 'rg-rbac.bicep' = {
  name: 'github-rg-rbac'
  scope: rg
  params: {
    principalId: uami.outputs.principalId
    assignableRoleIds: assignableRoleIds
  }
}

module actionGroup '../modules/actiongroup.bicep' = {
  name: 'actiongroup'
  scope: rg
  params: {
    tags: tags
    name: 'ag-${workload}-${env}-${instance}'
    shortName: 'gsldevcost'
    alertEmail: alertEmail
  }
}

module budget '../modules/budget.bicep' = {
  name: 'budget'
  params: {
    name: 'budget-${workload}-${env}-${instance}'
    amount: budgetAmount
    startDate: budgetStartDate
    actionGroupId: actionGroup.outputs.id
    alertEmail: alertEmail
  }
}

// Custom definitions at subscription scope (definitions only).
module policyDefs '../modules/policy-defs.bicep' = if (deployGovernance) {
  name: 'policy-defs'
}

// ALL policy assignments are scoped to the GSL resource group only, never the subscription.
// The existing subscription "Allowed locations" assignments (b666060a..., 96048b7f...) are not touched.
module policy '../modules/policy.bicep' = if (deployGovernance) {
  name: 'policy'
  scope: rg
  params: {
    allowedLocations: [ location ]
    planSkuDefinitionId: policyDefs!.outputs.planSkuDefinitionId
  }
}

// Put these in GitHub environment VARIABLES (not secrets, not the repo).
output AZURE_CLIENT_ID string = uami.outputs.clientId
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_SUBSCRIPTION_ID string = subscription().subscriptionId
output AZURE_RESOURCE_GROUP string = rg.name
