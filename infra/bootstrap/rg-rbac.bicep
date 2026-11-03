// RG-scoped role assignments for the GitHub pipeline identity (least privilege).
targetScope = 'resourceGroup'
param principalId string
param assignableRoleIds array

var contributor = 'b24988ac-6180-42a0-ab88-20f7382dd24c'
var rbacAdmin = 'f58310d9-a9f6-439a-9e8d-f62e7b41a168'
var assignable = join(assignableRoleIds, ', ')
// Constrained delegation: may create/delete assignments ONLY for the listed app data-plane roles.
var rbacCondition = '((!(ActionMatches{\'Microsoft.Authorization/roleAssignments/write\'})) OR (@Request[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${assignable}})) AND ((!(ActionMatches{\'Microsoft.Authorization/roleAssignments/delete\'})) OR (@Resource[Microsoft.Authorization/roleAssignments:RoleDefinitionId] ForAnyOfAnyValues:GuidEquals {${assignable}}))'

resource raContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, principalId, contributor)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributor)
    description: 'GSL pipeline: Contributor on the GSL RG only'
  }
}

resource raRbacAdmin 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, principalId, rbacAdmin)
  properties: {
    principalId: principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', rbacAdmin)
    condition: rbacCondition
    conditionVersion: '2.0'
    description: 'GSL pipeline: may assign only the five app data-plane roles, in this RG only'
  }
}
