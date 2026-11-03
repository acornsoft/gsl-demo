// Custom GSL cost-guardrail policy DEFINITIONS (subscription scope; definitions only, no assignments).
// Assignments are made at GSL resource-group scope in policy.bicep.
targetScope = 'subscription'

resource defApimSku 'Microsoft.Authorization/policyDefinitions@2023-04-01' = {
  name: 'gsl-demo-apim-consumption-only'
  properties: {
    displayName: 'GSL demo: API Management must use the Consumption tier'
    description: 'Developer and higher tiers bill hourly. The demo allows only Consumption.'
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
            equals: 'Microsoft.ApiManagement/service'
          }
          {
            field: 'Microsoft.ApiManagement/service/sku.name'
            notEquals: 'Consumption'
          }
        ]
      }
      then: {
        effect: 'deny'
      }
    }
  }
}

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

output apimSkuDefinitionId string = defApimSku.id
output planSkuDefinitionId string = defPlanSku.id
