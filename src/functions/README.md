# GSL Functions (Reg E deadline clock)

.NET 8 isolated Azure Functions app for the fictional Gotham Savings and Loan demo.
Synthetic data only. Not legal advice.

Runs on `func-gsl-dev-eastus-001` (Flex Consumption FC1, dotnet-isolated 8.0).
Auth level: **anonymous** (no keys) for the demo.

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/health` (`CheckHealth`) | `{status, version, timestamp}` |
| POST | `/api/disputes/reg-e/deadlines` (`CalculateRegEDeadlines`) | Reg E error-resolution deadlines, 12 CFR 1005.11(c) |
| Service Bus trigger | `sbq-gsl-dev-001` (`regEDisputeConsumer`) | Validates each result, logs `RegEDisputeTracked`, completes or dead-letters, skips duplicates |

### Reg E rules applied
- Investigation due: 10 business days after notice (20 for new accounts). 12 CFR 1005.11(c)(1), (c)(3)(i).
- Provisional credit due: same window, if the investigation isn't finished. 12 CFR 1005.11(c)(2)(i).
- Extended investigation: 45 calendar days (90 for new accounts, POS, or foreign-initiated). 12 CFR 1005.11(c)(2), (c)(3)(ii).
- Business days: Mon-Fri excluding US federal holidays (observed). Demo model of 12 CFR 1005.2(d).

### Input
`{"noticeDate":"yyyy-MM-dd","newAccount":false,"posOrForeign":false,"disputeId":"GSL-DSP-000123"}`. `noticeDate` is required; the
booleans default to false. `disputeId` is optional. New synthetic ids are `GSL-DSP-` plus a 26-character ULID; legacy `GSL-DSP-nnnnnn` ids are still accepted. When omitted, a new ULID-based id is made. An optional `Idempotency-Key` header makes the Service Bus `messageId` a hash of that key so a retried POST is the same message. Bad or missing input returns HTTP 400 with `{"error":"..."}`.

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
curl -s -X POST $BASE/api/disputes/reg-e/deadlines -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: demo-retry-1' \
  -d '{"noticeDate":"2026-11-09","newAccount":false,"posOrForeign":false,"disputeId":"GSL-DSP-000123"}'
```

## Build, test, deploy

```bash
cd src/functions
dotnet test
dotnet publish GslFunctions -c Release -o out && (cd out && zip -r ../app.zip .)
# Flex Consumption: config-zip works; `az functionapp deploy --type zip` returned 415 on this app.
az functionapp deployment source config-zip -g rg-gsl-dev-eastus-001 -n func-gsl-dev-eastus-001 --src app.zip
```

## Message contract: `reg-e.deadlines.computed` (schema `gsl.reg-e.deadlines/v1`)

Each HTTP result is sent with:

| Broker field | Value |
|---|---|
| `messageId` | SHA-256 hex of the `Idempotency-Key` header if present, otherwise of `disputeId` + noticeDate + flags. Deterministic so a retried POST is the same message. Service Bus Basic has no duplicate detection. |
| `correlationId` | dispute id, e.g. `GSL-DSP-000123` or `GSL-DSP-` plus a ULID (synthetic) |
| `subject` | `reg-e.deadlines.computed` |
| `contentType` | `application/json` |
| app properties | `newAccount`, `posOrForeign`, `investigationDueDate` (yyyy-MM-dd), `schema`, `synthetic=true` |

Body (camelCase): `schema, synthetic, disputeId, noticeDate, newAccount, posOrForeign, investigationBusinessDays,
investigationDueDate, provisionalCreditDueDate, extendedInvestigationCalendarDays, extendedInvestigationDueDate, citations, note`.

## Consumer: `regEDisputeConsumer`

Queue trigger on `%SERVICEBUS_QUEUE_NAME%` with the identity connection `ServiceBusConnection`
(`ServiceBusConnection__fullyQualifiedNamespace` + `ServiceBusConnection__credential=managedidentity`). The
identity needs **Azure Service Bus Data Receiver**. Settlement is explicit (`AutoCompleteMessages = false`):

- Valid and new: logs `RegEDisputeTracked {DisputeId} ... investigationDueDate=... extendedInvestigationDueDate=...`
  (EventId 4100) and completes the message.
- Valid but already tracked (same `messageId`, or `disputeId` if `messageId` is missing): logs
  `RegEDisputeDuplicateSkipped` (EventId 4102) and completes the message. Dedupe is an in-memory,
  process-local store. It is not shared across Flex instances and is empty after a recycle. Service Bus
  Basic has no broker-side duplicate detection, so this is enough for a single-instance demo, not a
  production guarantee.
- Invalid: dead-letters with a stable `DeadLetterReason` and a short `DeadLetterErrorDescription`:

| DeadLetterReason | When |
|---|---|
| `BodyNotJson` | empty body or not JSON |
| `SchemaViolation` | not an object, or a required field is missing or the wrong type |
| `MissingDisputeId` | neither `correlationId` nor `body.disputeId` is set (fix: stamp the id, then resubmit) |
| `InvalidDisputeId` | an id is present but is not `GSL-DSP-nnnnnn` or `GSL-DSP-` plus a 26-character ULID |
| `DisputeIdMismatch` | `correlationId` and `body.disputeId` differ |
| `InconsistentDeadlines` | day counts or due dates don't match the 12 CFR 1005.11 rules for the flags |

- Unexpected exceptions are not caught: the broker redelivers and, after `maxDeliveryCount` (10), dead-letters
  with `MaxDeliveryCountExceeded`.

Find tracked disputes in App Insights:

```kusto
traces
| where message startswith "RegEDisputeTracked" or message startswith "RegEDisputeDuplicateSkipped"
| project timestamp, message, customDimensions
```
