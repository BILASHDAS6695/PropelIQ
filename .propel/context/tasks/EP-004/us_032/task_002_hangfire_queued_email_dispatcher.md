# Task 002: Hangfire Queued Email Dispatcher

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-032 |
| **Epic** | EP-004 |
| **Layer** | Infrastructure — Messaging + DI wiring |
| **Priority** | High |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 001 (`MailKitEmailSender`, `SmtpSettings`) |

## Objective

Ensure all emails are dispatched **asynchronously** — the HTTP request enqueues a
Hangfire job and returns immediately; delivery happens on a background worker.
Hangfire handles retry (3 attempts, exponential backoff) and job persistence.

Two deliverables:

1. **`SendEmailJob`** — Hangfire job class; injects `MailKitEmailSender` directly
   and calls `SendAsync`. This is the only caller of `MailKitEmailSender`.
2. **`HangfireEmailDispatcher`** — implements `IEmailSender`; enqueues
   `SendEmailJob` instead of sending inline. This replaces `NoOpEmailSender` as
   the DI-registered `IEmailSender`.

---

## Acceptance Criteria Covered

- AC: Emails queued via Hangfire (not sent synchronously in request)
- AC: Failed email delivery retried 3 times with exponential backoff
- AC: Permanently failed emails logged with error details
- AC: SMTP server unavailable → queue retries, alert admin after 3 failures

---

## Implementation Steps

### 1. Create `SendEmailJob`

Create `src/HealthPlatform.Infrastructure/Messaging/SendEmailJob.cs`:

```csharp
using Microsoft.Extensions.Logging;

namespace HealthPlatform.Infrastructure.Messaging;

/// <summary>
/// Hangfire background job that delivers a single email via MailKit.
/// Hangfire retries this job up to 3 times with exponential backoff on failure.
/// </summary>
public sealed class SendEmailJob
{
    private readonly MailKitEmailSender          _sender;
    private readonly ILogger<SendEmailJob>       _logger;

    public SendEmailJob(
        MailKitEmailSender    sender,
        ILogger<SendEmailJob> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>
    /// Executes the email send. Called by Hangfire on the background worker.
    /// </summary>
    public async Task ExecuteAsync(string toAddress, string subject, string body)
    {
        _logger.LogInformation(
            "SendEmailJob: delivering email to '{ToAddress}' Subject='{Subject}'.",
            toAddress, subject);

        await _sender.SendAsync(toAddress, subject, body);
    }
}
```

### 2. Configure Hangfire Retry Policy

Add a `JobFilterAttribute` for retry with exponential backoff. Add a new file
`src/HealthPlatform.Infrastructure/Jobs/EmailRetryAttribute.cs`:

```csharp
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;

namespace HealthPlatform.Infrastructure.Jobs;

/// <summary>
/// Configures automatic retry for email jobs: 3 attempts, exponential back-off.
/// Attempt 1: immediate, Attempt 2: ~5 min, Attempt 3: ~25 min.
/// After the 3rd failure the job moves to the Failed state and is logged.
/// </summary>
public sealed class EmailRetryAttribute : JobFilterAttribute, IElectStateFilter
{
    private const int MaxAttempts = 3;

    public void OnStateElection(ElectStateContext context)
    {
        if (context.CandidateState is not FailedState failedState)
            return;

        var retryAttempt = context.GetJobParameter<int>("RetryCount");

        if (retryAttempt < MaxAttempts)
        {
            // Exponential back-off: 5^attempt minutes (5, 25, 125 min)
            var delayMinutes = (int)Math.Pow(5, retryAttempt + 1);
            context.CandidateState = new ScheduledState(TimeSpan.FromMinutes(delayMinutes));
            context.SetJobParameter("RetryCount", retryAttempt + 1);
        }
        // else: allow FailedState — permanently failed, already logged by MailKitEmailSender
    }
}
```

> **Alternative**: Hangfire's built-in `[AutomaticRetry(Attempts = 3)]` attribute
> also works and is simpler. Use `EmailRetryAttribute` only if the project requires
> custom exponential back-off; otherwise prefer `AutomaticRetry` to reduce code.
> Both are shown below — pick one and remove the other.

Apply the retry attribute to `SendEmailJob.ExecuteAsync`:

```csharp
// Option A — built-in attribute (simpler, recommended)
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 300, 1500, 7500 })]
public async Task ExecuteAsync(string toAddress, string subject, string body)

// Option B — custom attribute (more control)
[EmailRetry]
public async Task ExecuteAsync(string toAddress, string subject, string body)
```

Add required using for Option A:

```csharp
using Hangfire;
```

### 3. Create `HangfireEmailDispatcher`

Create `src/HealthPlatform.Infrastructure/Messaging/HangfireEmailDispatcher.cs`:

```csharp
using Hangfire;
using HealthPlatform.Application.Interfaces;

namespace HealthPlatform.Infrastructure.Messaging;

/// <summary>
/// IEmailSender implementation that enqueues a Hangfire background job
/// instead of sending inline — satisfies the AC requirement that emails
/// are never sent synchronously within an HTTP request.
/// </summary>
internal sealed class HangfireEmailDispatcher : IEmailSender
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireEmailDispatcher(IBackgroundJobClient jobs) => _jobs = jobs;

    public Task SendAsync(
        string toAddress,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        // Enqueue is synchronous (queues to Hangfire DB); we return a
        // completed task so callers need not await anything.
        _jobs.Enqueue<SendEmailJob>(job => job.ExecuteAsync(toAddress, subject, body));
        return Task.CompletedTask;
    }
}
```

### 4. Update DI Registration in `DependencyInjection.cs`

Replace the `NoOpEmailSender` registration and add new registrations:

**Remove:**
```csharp
services.AddScoped<IEmailSender, NoOpEmailSender>();
```

**Add:**
```csharp
// MailKitEmailSender registered directly (not via IEmailSender) so
// SendEmailJob can inject it without going through IBackgroundJobClient.
services.AddScoped<MailKitEmailSender>();

// HangfireEmailDispatcher is the public IEmailSender — all Application
// handlers call this, which enqueues the job rather than sending inline.
services.AddScoped<IEmailSender, HangfireEmailDispatcher>();

// Register Hangfire's built-in IBackgroundJobClient (already provided by
// AddHangfire, but explicit registration ensures availability in tests).
services.AddScoped<IBackgroundJobClient, BackgroundJobClient>();
```

Add the using if not already present:

```csharp
using Hangfire;
```

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Infrastructure/Messaging/SendEmailJob.cs` | New — Hangfire job class |
| `src/HealthPlatform.Infrastructure/Messaging/HangfireEmailDispatcher.cs` | New — queuing `IEmailSender` |
| `src/HealthPlatform.Infrastructure/Jobs/EmailRetryAttribute.cs` | New — custom retry filter (optional) |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Swap `NoOpEmailSender` → `HangfireEmailDispatcher` |

---

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --configuration Release
# Expect: 0 errors, 0 warnings
```

Manually confirm with Hangfire dashboard:
1. Start the API (`dotnet run`)
2. Book an appointment via the booking UI
3. Navigate to `/hangfire` (Admin role required)
4. Confirm a `SendEmailJob` job appears in the Succeeded or Enqueued queue

---

## Notes

- `HangfireEmailDispatcher` uses `IBackgroundJobClient` (Hangfire's own interface)
  which is already registered by `services.AddHangfireServer()` in `Program.cs`.
  The explicit `AddScoped<IBackgroundJobClient, BackgroundJobClient>()` is a
  belt-and-suspenders guard that makes tests easier to mock.
- `NoOpEmailSender` is no longer registered in DI but the file is kept — it remains
  useful as a test double throughout the test project.
- `SendEmailJob` is `public` (not `internal`) because Hangfire uses serialization
  and reflection to invoke the method; `internal` jobs fail to deserialize in
  some configurations.
- In production, override SMTP credentials using environment variables:
  `Smtp__Host`, `Smtp__Port`, `Smtp__UserName`, `Smtp__Password`, `Smtp__UseSsl`.
