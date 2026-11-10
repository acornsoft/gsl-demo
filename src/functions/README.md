# GSL Functions (placeholder)

Minimal .NET 8 isolated Azure Functions app for `func-gsl-dev-eastus-001` (Flex Consumption, `dotnet-isolated` 8.0).
Synthetic data only. Ted will replace this from `feat/fn-reg-e-clock`.

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/health` | `{status:'ok', service:'gsl-demo', time}` |
| GET | `/api/queue-check` | Uses the Function App managed identity to peek `sbq-gsl-dev-001` and confirm it is reachable. No connection string. |

Deploy (Flex, package goes to the `app-package` blob container):

```bash
dotnet publish src/functions/GslFunctions/GslFunctions.csproj -c Release -o /tmp/gsl-fn-out
(cd /tmp/gsl-fn-out && zip -r /tmp/gsl-fn.zip .)
az functionapp deployment source config-zip -g rg-gsl-dev-eastus-001 -n func-gsl-dev-eastus-001 --src /tmp/gsl-fn.zip
```
