# GSL demo landing zone: Sprint 1 design

Fictional Gotham Savings and Loan (GSL) practice model. Synthetic data only.

## Design summary
- **Scope:** subscription `Azure via PSCB [D]` (`ca1fc4ed-67c9-445b-bcf8-17fac62afe1d`), tenant acornsoft365.com (`c21f4003-8ff9-4fdb-b804-5b2b371717f4`). The other subscription, `Azure via MCPP [P]`, must never be touched. Tenant and subscription IDs are not secrets.
- **One dedicated, tagged resource group**, `rg-gsl-dev-eastus-001`, in eastus. Every workload resource lives in it.
- **Two deployment layers (least privilege):**
  - **Bootstrap** (`infra/bootstrap/github-oidc.bicep`, subscription scope, run once by an Owner) creates everything that touches the subscription. That is the RG; the pipeline identity `id-gsl-dev-eastus-001` and its GitHub OIDC federated credentials; the RG-scoped role assignments; the action group `ag-gsl-dev-001`; the $50 budget; the policy definitions (subscription scope), and the policy assignments. **Every policy assignment is scoped to the GSL resource group only, never the subscription.** The subscription's existing "Allowed locations" assignments (`b666060a…`, `96048b7f…`) are left untouched.
  - **Pipeline** (`infra/main.bicep`, resource-group scope, `az deployment group`) deploys only the workload resources into the RG.
- **Pipeline identity rights:** Contributor on `rg-gsl-dev-eastus-001` only, plus Role Based Access Control Administrator on that same RG. The second role carries an ABAC condition that allows it to create or delete assignments only for five app data-plane roles: Key Vault Secrets User, Storage Blob Data Owner, Storage Queue Data Contributor, Storage Table Data Contributor and Monitoring Metrics Publisher. It has no rights at subscription scope.
- **Tags:** workload, env, owner, costCenter, dataClass=synthetic, managedBy, azd-env-name.
- **Consumption or free tiers only:** Service Bus Basic, Functions Flex Consumption (FC1, scales to zero, max 40 instances), Key Vault Standard (RBAC), Log Analytics with a 0.1 GB/day cap, workspace-based App Insights, Storage Standard_LRS (needed by Functions).
- **No API Management.** David (2026-11-09): remove APIM entirely; the demo target is the Function App only. There is no APIM module, parameter, output or name in the templates. A Consumption-only APIM policy definition and assignment from the original bootstrap still exist in Azure; this PR does not delete them.
- **Identity:** no keys anywhere. Functions use managed identity for Storage, App Insights and Key Vault. Service Bus has local/SAS auth turned off. GitHub Actions signs in through OIDC to the user-assigned managed identity.
- **Guardrails:** a $50 monthly budget with alerts at 50/80/100% (actual) plus 100% (forecast), sent to david@acornsoft365.com and the action group. A deny policy allows only serverless App Service plans, assigned at GSL resource-group scope.
- **Service Bus note:** Basic supports queues only. Move to Standard (`serviceBusSku=Standard`, about $10/month base) when ADO 2791/2793 need topics.

## Names (CAF, `<abbr>-<workload>-<env>-<region>-<instance>`)
Abbreviations come from the [CAF resource abbreviations page](https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations). Length limits come from the [naming rules page](https://learn.microsoft.com/azure/azure-resource-manager/management/resource-name-rules).

| Resource | Type | Name | Length | Limit |
|---|---|---|---|---|
| Resource group | Microsoft.Resources/resourceGroups | rg-gsl-dev-eastus-001 | 21 | 1–90 |
| Log Analytics | Microsoft.OperationalInsights/workspaces | log-gsl-dev-eastus-001 | 22 | 4–63 |
| App Insights | Microsoft.Insights/components | appi-gsl-dev-eastus-001 | 23 | 1–260 |
| Key Vault | Microsoft.KeyVault/vaults | kv-gsl-dev-eus-001 | 18 | 3–24, global |
| Storage (Functions) | Microsoft.Storage/storageAccounts | stgsldeveus001 | 14 | 3–24, a-z0-9, global |
| Flex plan | Microsoft.Web/serverfarms | asp-gsl-dev-eastus-001 | 22 | 1–40 |
| Function App | Microsoft.Web/sites | func-gsl-dev-eastus-001 | 23 | 2–60, global |
| Service Bus namespace | Microsoft.ServiceBus/namespaces | sbns-gsl-dev-eastus-001 | 23 | 6–50, global |
| Service Bus queue | Microsoft.ServiceBus/namespaces/queues | sbq-gsl-dev-001 | 15 | 1–260 |
| Managed identity (GitHub) | Microsoft.ManagedIdentity/userAssignedIdentities | id-gsl-dev-eastus-001 | 21 | 3–128 |
| Action group | Microsoft.Insights/actionGroups | ag-gsl-dev-001 (short: gsldevcost) | 14 / 10 | 1–260 / short 12 |
| Budget | Microsoft.Consumption/budgets | budget-gsl-dev-001 | 18 | 1–63 |

Global uniqueness was checked on 2026-11-03 (read-only): Key Vault, Storage, Service Bus and the Function App names are all available, and the Key Vault name is not in the soft-deleted list.

## Monthly cost estimate (USD, eastus, list prices)
| Service | Expected (demo load) | Notes / source |
|---|---|---|
| Functions Flex Consumption | $0–1 | Free grant of 100k GB-s and 250k executions on-demand ([pricing](https://azure.microsoft.com/pricing/details/functions/)) |
| Service Bus Basic | <$0.10 | $0.05 per million operations ([pricing](https://azure.microsoft.com/pricing/details/service-bus/)); Standard would be about $10 base |
| Key Vault Standard | <$0.10 | $0.03 per 10k operations ([pricing](https://azure.microsoft.com/pricing/details/key-vault/)) |
| Log Analytics + App Insights | $0–3 | 5 GB/month free, then about $2.30/GB; daily cap of 0.1 GB ([pricing](https://azure.microsoft.com/pricing/details/monitor/)) |
| Storage Standard_LRS | $0.50–2 | About $0.02/GB plus transactions ([pricing](https://azure.microsoft.com/pricing/details/storage/blobs/)) |
| **Total** | **about $2–12** (unchanged vs the earlier range; APIM Consumption would have been $0 at demo load) | **Ceiling $50** (budget alerts at 50/80/100% plus forecast) |

Dropping APIM does not change the expected demo total. The Consumption tier has no base fee; the first 1 million API operations per subscription per month are included, then $0.035 per 10,000 operations in eastus ([Microsoft API Management pricing](https://azure.microsoft.com/pricing/details/api-management/), confirmed via the [Azure Retail Prices API](https://prices.azure.com/api/retail/prices?$filter=serviceName%20eq%20'API%20Management'%20and%20armRegionName%20eq%20'eastus'%20and%20skuName%20eq%20'Consumption') on 2026-11-09). Demo traffic stays under the free grant, so the monthly cost change is **$0**. The range stays about **$2–12**, with the $50 ceiling unchanged.

These are estimates from list prices. Check them with the Azure pricing calculator before deploying.

## Region
eastus (David's decision, 2026-11-03). The subscription's existing Allowed-locations policies (`b666060a…`, `96048b7f…`) allow eastus, centralus and westus.

## GitHub OIDC subject format
GitHub sends OIDC subjects in the ID-based form `repo:acornsoft@3299918/gsl-demo@1444246506:environment:<env>`. The federated credentials match this. The owner and repo IDs are the bootstrap parameters `githubOwnerId` and `githubRepoId`; they are public identifiers, not secrets.
