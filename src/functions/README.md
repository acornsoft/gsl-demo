# GSL Functions (Reg E deadline clock)

.NET 8 isolated Azure Functions app for the fictional Gotham Savings and Loan demo.
Synthetic data only. Not legal advice.

Runs on `func-gsl-dev-eastus-001` (Flex Consumption FC1, dotnet-isolated 8.0).
Auth level: **anonymous** (no keys) for the demo.

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/health` | `{status, version, timestamp}` |
| POST | `/api/disputes/reg-e/deadlines` | Reg E error-resolution deadlines, 12 CFR 1005.11(c) |

### Reg E rules applied
- Investigation due: 10 business days after notice (20 for new accounts). 12 CFR 1005.11(c)(1), (c)(3)(i).
- Provisional credit due: same window, if the investigation isn't finished. 12 CFR 1005.11(c)(2)(i).
- Extended investigation: 45 calendar days (90 for new accounts, POS, or foreign-initiated). 12 CFR 1005.11(c)(2), (c)(3)(ii).
- Business days: Mon-Fri excluding US federal holidays (observed). Demo model of 12 CFR 1005.2(d).

### Input
`{"noticeDate":"yyyy-MM-dd","newAccount":false,"posOrForeign":false}`. `noticeDate` is required; the
booleans default to false. Bad or missing input returns HTTP 400 with `{"error":"..."}`.

### Optional Service Bus send
If the app setting `ServiceBusConnection__fullyQualifiedNamespace` is set
(e.g. `sbns-gsl-dev-eastus-001.servicebus.windows.net`), each result is sent to `sbq-gsl-dev-001`
(override with `SERVICEBUS_QUEUE_NAME`) using managed identity. The identity needs
**Azure Service Bus Data Sender** on the namespace. If the setting is missing, the send is skipped and the
response shows `"serviceBus":"skipped"`.

## curl

```bash
BASE=https://func-gsl-dev-eastus-001.azurewebsites.net
curl -s $BASE/api/health
curl -s -X POST $BASE/api/disputes/reg-e/deadlines -H 'Content-Type: application/json' \
  -d '{"noticeDate":"2026-11-09","newAccount":false,"posOrForeign":false}'
curl -s -X POST $BASE/api/disputes/reg-e/deadlines -H 'Content-Type: application/json' \
  -d '{"noticeDate":"2026-11-09","newAccount":true,"posOrForeign":true}'
curl -s -i -X POST $BASE/api/disputes/reg-e/deadlines -H 'Content-Type: application/json' -d '{}'   # 400
```

## Build, test, deploy

```bash
cd src/functions
dotnet test
dotnet publish GslFunctions -c Release -o out && (cd out && zip -r ../app.zip .)
az functionapp deploy -g rg-gsl-dev-eastus-001 -n func-gsl-dev-eastus-001 --src-path app.zip --type zip
```
