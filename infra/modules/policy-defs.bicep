// Custom GSL cost-guardrail policy DEFINITIONS (subscription scope; definitions only, no assignments).
// Assignments are made at GSL resource-group scope in policy.bicep.
targetScope = 'subscription'

resource defPlanSku 'Microsoft.Authorization/policyDefinitions@2023-04-01' = {
  name: 'gsl-demo-serverless-plans-only'
  properties: {
    displayName: 'GSL demo: App Service plans must be Flex Consumption or Consumption'
    description: 'Blocks dedicated and Premium plans that bill while idle.'
    policyType: 'Custom'
    mode: 'All'
    metadata: {
      category: 'Cost'
    }
    policyRule: {
      if: {
        allOf: [
          {
            field: 'type'
            equals: 'Microsoft.Web/serverfarms'
          }
          {
            field: 'Microsoft.Web/serverfarms/sku.tier'
            notIn: [
              'FlexConsumption'
              'Dynamic'
            ]
          }
        ]
      }
      then: {
        effect: 'deny'
      }
    }
  }
}

output planSkuDefinitionId string = defPlanSku.id
