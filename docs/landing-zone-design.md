# GSL demo landing zone: Sprint 1 design

Fictional Gotham Savings and Loan (GSL) practice model. Synthetic data only.

## Design summary
- **Scope:** subscription `Azure via PSCB [D]` (`ca1fc4ed-67c9-445b-bcf8-17fac62afe1d`), tenant acornsoft365.com (`c21f4003-8ff9-4fdb-b804-5b2b371717f4`). The other subscription, `Azure via MCPP [P]`, must never be touched. Tenant and subscription IDs are not secrets.
- **One dedicated, tagged resource group**, `rg-gsl-dev-eastus2-001`, in eastus2. Every resource lives in it. The only subscription-scope objects are the cost budget, the bootstrap role assignments, and the optional policy (run by an admin only, `deployGovernance=false` by default).
- **Tags:** workload, env, owner, costCenter, dataClass=synthetic, managedBy, azd-env-name.
- **Consumption or free tiers only:** APIM Consumption, Service Bus Basic, Functions Flex Consumption (FC1, scales to zero, max 40 instances), Key Vault Standard (RBAC), Log Analytics with a 0.1 GB/day cap, workspace-based App Insights, Storage Standard_LRS (needed by Functions).
- **Identity:** no keys anywhere. Functions use managed identity for Storage, App Insights and Key Vault. Service Bus has local/SAS auth turned off. GitHub Actions signs in through OIDC to a user-assigned managed identity (`infra/bootstrap/`, a one-time owner run).
- **Guardrails:** a $50 monthly budget with alerts at 50/80/100% (actual) plus 100% (forecast), sent to david@acornsoft365.com and the action group. A deny policy blocks any APIM SKU other than Consumption, and another allows only serverless plans.
- **Service Bus note:** Basic supports queues only. Move to Standard (`serviceBusSku=Standard`, about $10/month base) when ADO 2791/2793 need topics.

## Names (CAF, `<abbr>-<workload>-<env>-<region>-<instance>`)
Abbreviations come from the [CAF resource abbreviations page](https://learn.microsoft.com/azure/cloud-adoption-framework/ready/azure-best-practices/resource-abbreviations). Length limits come from the [naming rules page](https://learn.microsoft.com/azure/azure-resource-manager/management/resource-name-rules).

| Resource | Type | Name | Length | Limit |
|---|---|---|---|---|
| Resource group | Microsoft.Resources/resourceGroups | rg-gsl-dev-eastus2-001 | 22 | 1–90 |
| Log Analytics | Microsoft.OperationalInsights/workspaces | log-gsl-dev-eastus2-001 | 23 | 4–63 |
| App Insights | Microsoft.Insights/components | appi-gsl-dev-eastus2-001 | 24 | 1–260 |
| Key Vault | Microsoft.KeyVault/vaults | kv-gsl-dev-eus2-001 | 19 | 3–24, global |
| Storage (Functions) | Microsoft.Storage/storageAccounts | stgsldeveus2001 | 15 | 3–24, a-z0-9, global |
| Flex plan | Microsoft.Web/serverfarms | asp-gsl-dev-eastus2-001 | 23 | 1–40 |
| Function App | Microsoft.Web/sites | func-gsl-dev-eastus2-001 | 24 | 2–60, global |
| API Management | Microsoft.ApiManagement/service | apim-gsl-dev-eastus2-001 | 24 | 1–50, global |
| Service Bus namespace | Microsoft.ServiceBus/namespaces | sbns-gsl-dev-eastus2-001 | 24 | 6–50, global |
| Service Bus queue | Microsoft.ServiceBus/namespaces/queues | sbq-gsl-dev-001 | 15 | 1–260 |
| Managed identity (GitHub) | Microsoft.ManagedIdentity/userAssignedIdentities | id-gsl-dev-eastus2-001 | 22 | 3–128 |
| Action group | Microsoft.Insights/actionGroups | ag-gsl-dev-001 (short: gsldevcost) | 14 / 10 | 1–260 / short 12 |
| Budget | Microsoft.Consumption/budgets | budget-gsl-dev-001 | 18 | 1–63 |

Global uniqueness is **not yet checked**: the box's Azure sign-in needs MFA again. See the open items.

## Monthly cost estimate (USD, eastus2, list prices)
| Service | Expected (demo load) | Notes / source |
|---|---|---|
| APIM Consumption | $0 | First 1M calls/month free, then $0.042 per 10k ([pricing](https://azure.microsoft.com/pricing/details/api-management/)) |
| Functions Flex Consumption | $0–1 | Free grant of 100k GB-s and 250k executions on-demand ([pricing](https://azure.microsoft.com/pricing/details/functions/)) |
| Service Bus Basic | <$0.10 | $0.05 per million operations ([pricing](https://azure.microsoft.com/pricing/details/service-bus/)); Standard would be about $10 base |
| Key Vault Standard | <$0.10 | $0.03 per 10k operations ([pricing](https://azure.microsoft.com/pricing/details/key-vault/)) |
| Log Analytics + App Insights | $0–3 | 5 GB/month free, then about $2.30/GB; daily cap of 0.1 GB ([pricing](https://azure.microsoft.com/pricing/details/monitor/)) |
| Storage Standard_LRS | $0.50–2 | About $0.02/GB plus transactions ([pricing](https://azure.microsoft.com/pricing/details/storage/blobs/)) |
| **Total** | **about $2–12** | **Ceiling $50** (budget alerts at 50/80/100% plus forecast) |

These are estimates from list prices. Check them with the Azure pricing calculator before deploying.
