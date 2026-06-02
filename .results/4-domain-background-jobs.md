# Domain Deep Dive: Background Jobs (Hangfire)

## Overview
Background job scheduling is handled exclusively by Hangfire 1.8.3, backed by a dedicated SQL Server database. Job definitions live in `IJobService`; orchestration (type + scheduling) is the controller's responsibility.

---

## Infrastructure Setup

Two Hangfire registrations in `Program.cs`:
```csharp
builder.Services.AddHangfire(x =>
    x.UseSqlServerStorage(builder.Configuration.GetConnectionString("HFDBConString")));
builder.Services.AddHangfireServer();
// ...
app.UseHangfireDashboard();
```

Hangfire uses a separate database (`HFDBConString`) from the main application database (`DBConString`).

---

## IJobService Interface

```csharp
public interface IJobService
{
    void FireAndForgetJob();
    void ReccuringJob();
    void DelayedJob();
    void ContinuationJob();
}
```

Job methods are synchronous `void` — they represent units of work that Hangfire serialises and executes independently.

---

## Four Job Types

All four Hangfire job types are demonstrated in `JobTestController`:

| Job Type        | Hangfire API                                                      | Description                              |
|-----------------|-------------------------------------------------------------------|------------------------------------------|
| Fire-and-forget | `BackgroundJobClient.Enqueue(() => JobService.FireAndForgetJob())` | Executes once immediately in background  |
| Delayed         | `BackgroundJobClient.Schedule(() => ..., TimeSpan.FromSeconds(60))` | Executes once after a delay             |
| Recurring       | `RecurringJobManager.AddOrUpdate("jobId", () => ..., Cron.Minutely)` | Runs on a cron schedule               |
| Continuation    | `BackgroundJobClient.ContinueJobWith(parentJobId, () => JobService.ContinuationJob())` | Runs after parent completes |

---

## Controller Pattern for Jobs

```csharp
public class JobTestController : ControllerBase
{
    private readonly IJobService JobService;
    private readonly IBackgroundJobClient BackgroundJobClient;
    private readonly IRecurringJobManager RecurringJobManager;

    [HttpGet("continuationJob")]
    public ActionResult CreateContinuationJob()
    {
        string parentJobId = BackgroundJobClient.Enqueue(() => JobService.FireAndForgetJob());
        BackgroundJobClient.ContinueJobWith(parentJobId, () => JobService.ContinuationJob());
        return Ok();
    }
}
```

---

## Key Constraints
- Job logic must live in `IJobService` / `JobService` — do not inline lambda bodies in controllers.
- Hangfire is the only background-job mechanism; do not introduce `IHostedService` or `BackgroundService`.
- The Hangfire dashboard is accessible at `/hangfire` with no auth guard (intended for development only).
