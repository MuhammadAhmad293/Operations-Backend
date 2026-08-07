# 003 – Reliable Email Delivery Pipeline

## Status

`completed`

## Goal

Implement a production-grade, asynchronous email delivery pipeline using:

- Transactional Outbox Pattern
- RabbitMQ
- BackgroundService Publisher
- Email Consumer
- Retry Policy (Polly v8)
- Circuit Breaker
- Dead Letter Queue (DLQ)
- Inbox Pattern (idempotency)
- Publisher Confirms
- Health Checks
- Metrics

The solution must guarantee that no committed email is lost, tolerate temporary SMTP outages, and avoid overwhelming the SMTP provider during extended failures.

---

## Architecture

```
API
 │
 ▼
Create User + Mail + OutboxMessage
 │
 ▼
Commit Transaction (atomic)
 │
 ▼
OutboxPublisherService (500ms poll)
 │
 ▼
RabbitPublisher (with Publisher Confirms)
 │
 ▼
RabbitMQ email.exchange
 │
 ▼
EmailConsumer
 │
 ▼
Inbox deduplication check
 │
 ▼
EmailResiliencePipeline (Polly v8)
 │
 ▼
SMTP Provider
```

---

## Key Architecture Decisions

| #   | Decision                                                                                                                                                  |
| --- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| A   | **Outbox Publisher** = `BackgroundService` with 500ms poll — NOT Hangfire (continuous infrastructure, not scheduled work)                                 |
| B   | **RabbitMQ connection lifecycle** separated into `RabbitConnectionManager : IHostedService`; `RabbitPublisher` uses it                                    |
| C   | **Two status dimensions on Mail**: `MailStatusId` (business: Draft/Sent/Failed) + `DeliveryStatus` (infra: Pending/Queued/Processing/Retrying/DeadLetter) |
| D   | **Outbox status has no Failed state**: Pending → Publishing → Published; failure resets to Pending                                                        |
| E   | **Retry delegated to RabbitMQ DLX**: Consumer NACKs transient failures → DLX routes to `email.retry` → TTL → back to `email.send`                         |
| F   | **Inbox Pattern** via `ProcessedMessage { MessageId, ProcessedAt }` — more robust than checking Mail status                                               |
| G   | **Publisher Confirms**: `WaitForConfirmsOrDieAsync()` before marking OutboxMessage as Published                                                           |
| H   | **Message envelope**: `{ MessageId: GUID, MailId: N, OccurredAt: UTC }`                                                                                   |
| I   | **All thresholds in config** — zero hardcoded constants                                                                                                   |

---

## New NuGet Packages

| Package                            | Project                     |
| ---------------------------------- | --------------------------- |
| `RabbitMQ.Client` v7.x             | `Meezan`, `Meezan.Services` |
| `Microsoft.Extensions.Resilience`  | `Meezan.Services`           |
| `AspNetCore.HealthChecks.RabbitMQ` | `Meezan`                    |

---

## Sub-Tasks

| #   | Description                                                                                                                                                                                                | Status  |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------- |
| 1   | `EmailDeliverySettings` POCO + `appsettings.json` `"EmailDelivery"` section                                                                                                                                | ✅ done |
| 2   | `OutboxMessage` + `ProcessedMessage` entities, EF configs, repositories, UoW wiring, migration `AddOutboxAndInboxTables`                                                                                   | ✅ done |
| 3   | Extend `Mail` entity (`DeliveryStatus`, `RetryCount`, `LastAttemptAt`, `SentAt`, `LastError`); trim `MailStatusEnum` to business states; add `DeliveryStatus` enum; migration `ExtendMailDeliveryTracking` | ✅ done |
| 4   | `RabbitConnectionManager : IHostedService` + `IRabbitPublisher` / `RabbitPublisher` (with Publisher Confirms)                                                                                              | ✅ done |
| 5   | RabbitMQ topology declaration (`email.exchange`, `email.send`, `email.retry`, `email.deadletter`)                                                                                                          | ✅ done |
| 6   | `OutboxPublisherService : BackgroundService` (500ms poll, batch publish, startup recovery)                                                                                                                 | ✅ done |
| 7   | `ISmtpEmailSender` / `SmtpEmailSender` (wraps `IMailSender`, re-throws on failure)                                                                                                                         | ✅ done |
| 8   | `EmailResiliencePipeline` (Polly v8: retry + circuit breaker, transient/permanent classification)                                                                                                          | ✅ done |
| 9   | `EmailConsumer : BackgroundService` (consume `email.send`, Inbox check, circuit-open handling)                                                                                                             | ✅ done |
| 10  | `DeadLetterHandler : BackgroundService` (consume `email.deadletter`, update Mail status)                                                                                                                   | ✅ done |
| 11  | Update `AuthService` + `UserService`: remove `MailSender.SendMail()`, add `OutboxRepository.Create()`                                                                                                      | ✅ done |
| 12  | Health Checks (`/health`: RabbitMQ, SMTP, outbox backlog, consumer alive)                                                                                                                                  | ✅ done |
| 13  | Metrics via `System.Diagnostics.Metrics` (emails.sent/failed/retry/deadletter, outbox.pending, durations, circuit.open.count)                                                                              | ✅ done |

---

## Approval Log

| Sub-Task | Approved By | Date       |
| -------- | ----------- | ---------- |
| Plan     | User        | 2026-07-02 |
