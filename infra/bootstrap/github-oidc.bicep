// ONE-TIME BOOTSTRAP, run by a subscription Owner (or User Access Administrator + Contributor).
// Creates a user-assigned managed identity for GitHub Actions, with federated credentials.
// No client secret exists. GitHub exchanges its OIDC token for an Entra token.
//
//   az deployment sub create -l eastus2 -f infra/bootstrap/github-oidc.bicep \
//     -p githubOrg=<github-org> githubRepo=<repo>
targetScope = 'subscription'

param location string = 'eastus2'

@description('GitHub org or user that owns the public repo. Public repo acornsoft/gsl-demo.')
param githubOrg string = 'acornsoft'

@description('GitHub repo name.')
param githubRepo string = 'gsl-demo'

@description('GitHub environments allowed to get a token. One for what-if, one for deploy.')
param githubEnvironments array = [
  'demo-preview'
  'demo'
]

param tags object = {
  workload: 'gsl'
  env: 'dev'
  owner: 'gsl-cloud-platform'
  costCenter: 'cc-gsl-demo-placeholder'
  managedBy: 'bicep-bootstrap'
}

var roles = {
  contributor: 'b24988ac-6180-42a0-ab88-20f7382dd24c'
  rbacAdmin: 'f58310d9-a9f6-439a-9e8d-f62e7b41a168'
  // Roles the pipeline may hand out to app identities, and nothing else.
  keyVaultSecretsUser: '4633458b-17de-408a-b874-0445c86b69e6'
  blobDataOwner: 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
  queueDataContributor: '974c5e8b-45b9-4653-ba55-5f855dd0fb88'
  tableDataContributor: '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
  monitoringMetricsPublisher: '3913510d-42f4-4e42-8a64-420c390055eb'
}

var assignable = '${roles.keyVaultSecretsUser}, ${roles.blobDataOwner}, ${roles.queueDataContributor}, ${roles.tableDataContributor}, ${roles.monitoringMetricsPublisher}'

// Constrained delegation: the pipeline can only create or delete assignments for the five roles above.
var rbacCondition = '((!(ActionMatches{\'Microsoft.Authorization/roleAssignments/write\'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${assignable}})) AND ((!(ActionMatches{\'Microsoft.Authorization/roleAssignments/delete\'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${assignable}}))'

resource rgShared 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'rg-gsl-dev-eastus2-001'
  location: location
  tags: tags
}

module uami 'uami.bicep' = {
  name: 'github-uami'
  scope: rgShared
  params: {
    location: location
    tags: tags
    name: 'id-gsl-dev-eastus2-001'
    subjects: [for e in githubEnvironments: 'repo:${githubOrg}/${githubRepo}:environment:${e}']
  }
}

// Contributor on the dedicated demo subscription only. Blast radius = the demo subscription.
resource raContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, 'gsl-demo-github', roles.contributor)
  properties: {
    principalId: uami.outputs.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.contributor)
    description: 'GSL demo GitHub Actions deploy identity'
  }
}

resource raRbacAdmin 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(subscription().id, 'gsl-demo-github', roles.rbacAdmin)
  properties: {
    principalId: uami.outputs.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roles.rbacAdmin)
    condition: rbacCondition
    conditionVersion: '2.0'
    description: 'Constrained: may assign only app data-plane roles'
  }
}

// Put these three values in GitHub environment VARIABLES (not secrets, not the repo).
output AZURE_CLIENT_ID string = uami.outputs.clientId
output AZURE_TENANT_ID string = tenant().tenantId
output AZURE_SUBSCRIPTION_ID string = subscription().subscriptionId
