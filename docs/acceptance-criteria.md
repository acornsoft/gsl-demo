## Drafted acceptance criteria (to copy into ADO)

**AB#2776: Write Bicep modules**
- [ ] Given the repo, when `az bicep build` and `az bicep lint` run on `infra/main.bicep` and `infra/bootstrap/github-oidc.bicep`, then both compile with no errors and lint reports no warnings.
- [ ] `infra/main.bicep` has `targetScope = 'resourceGroup'`. A read-only `az deployment group what-if -g rg-gsl-dev-eastus-001` shows creates only inside that RG and nothing at subscription scope.
- [ ] Everything subscription-scoped (the RG, the identity and federated credentials, the role assignments, the action group, the budget and the policy definitions) is only in `infra/bootstrap/`. The policy assignments are at GSL RG scope.
- [ ] The bootstrap what-if (`location=eastus`) shows 0 `Microsoft.Authorization/policyAssignments` at subscription scope. The only subscription-scope creates are the RG, the budget and the 2 custom policy definitions.
- [ ] Each service has its own module, and its SKU is Consumption, free or Basic. Every name matches the CAF table and fits its type's limits.
- [ ] The RG and every resource carry the tags workload, env, owner, costCenter, dataClass and managedBy.
- [ ] No key, connection string or password is in the repo (the secret scan is clean), and parameters hold only names, IDs and emails.
- [ ] Nothing is deployed to or changed in `Azure via MCPP [P]`.

**AB#2777: Configure GitHub OIDC federated credential**
- [ ] Given the one-time Owner bootstrap, then `id-gsl-dev-eastus-001` exists in `rg-gsl-dev-eastus-001` with federated credentials whose issuer is `https://token.actions.githubusercontent.com`, audience is `api://AzureADTokenExchange`, and subjects are `repo:acornsoft@3299918/gsl-demo@1444246506:environment:demo-preview` and `repo:acornsoft@3299918/gsl-demo@1444246506:environment:demo` (GitHub's ID-based subject format; owner and repo IDs are bootstrap parameters).
- [ ] The identity has no client secret or certificate. `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` and `AZURE_RESOURCE_GROUP` are stored as GitHub environment **variables**.
- [ ] `az role assignment list --assignee <clientId> --all` shows exactly two assignments, both scoped to `/subscriptions/ca1fc4ed-…/resourceGroups/rg-gsl-dev-eastus-001`. One is Contributor. The other is Role Based Access Control Administrator, with a condition limiting it to the five app data-plane roles. There are no subscription-scope assignments.
- [ ] Given the pipeline identity, when it tries to assign Owner or Contributor, or to deploy at subscription scope or into another RG, then Azure denies it with AuthorizationFailed.
- [ ] Given a run on `main` in `demo-preview`, `azure/login@v2` signs in without a secret, and `az deployment group what-if` succeeds. Runs from forks or other environments get no token. The `demo` environment needs David as a required reviewer before `az deployment group create`.

**AB#2778: Set budget and alerts**
- [ ] Given the Owner bootstrap, then the budget `budget-gsl-dev-001` exists on subscription ca1fc4ed, is monthly, and is set to $50. The pipeline can't change it.
- [ ] It alerts on actual spend at 50%, 80% and 100% and on forecast spend at 100%. Each alert emails david@acornsoft365.com and notifies `ag-gsl-dev-001`.
- [ ] Given a test notification from the action group, then david@acornsoft365.com receives it.
- [ ] Policy assignments come from the bootstrap. `az policy assignment list --scope /subscriptions/ca1fc4ed-…` shows **no** `gsl-demo-*` assignment at subscription scope. Every `gsl-demo-*` assignment (allowed locations, allowed RG locations, blocked types, RG tags, storage, Key Vault, APIM Consumption-only, serverless plans only) is scoped to `rg-gsl-dev-<region>-001`. The existing `b666060a…`/`96048b7f…` assignments are unchanged.
- [ ] In the GSL RG, a test deploy with a non-Consumption APIM SKU (for example Developer) is denied. The same deploy in another RG is not affected by GSL policy.
- [ ] The Log Analytics daily cap is 0.1 GB, and expected spend in the first full month is no more than about $12.
