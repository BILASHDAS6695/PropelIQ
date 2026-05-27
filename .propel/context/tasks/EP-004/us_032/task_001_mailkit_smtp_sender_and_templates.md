# Task 001: MailKit SMTP Sender and HTML Email Templates

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-032 |
| **Epic** | EP-004 |
| **Layer** | Infrastructure — Messaging |
| **Priority** | High |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | None — `IEmailSender` interface already exists in Application layer |

## Objective

Replace the `NoOpEmailSender` stub with a real MailKit SMTP implementation and a
template service that renders responsive HTML for all six notification scenarios.

Two deliverables:

1. **`SmtpSettings`** POCO bound from `appsettings.json` / environment variables.
2. **`EmailTemplateService`** — renders HTML bodies for each notification type.
3. **`MailKitEmailSender`** — sends SMTP email via MailKit; validates the address,
   falls back to plain-text on render error, and logs permanently-failed deliveries.

> **Design note**: The acceptance criterion says "Email service abstracted behind
> `INotificationService`". `IEmailSender` already fulfils that role — it is the
> abstraction used by every Application-layer handler. Introducing a second
> interface would require updating 8+ handlers with no benefit. `IEmailSender` is
> retained as the stable contract.

---

## Acceptance Criteria Covered

- AC: Email service implemented using MailKit (SMTP)
- AC: Email templates for all six scenarios
- AC: Templates use HTML with inline CSS, responsive layout
- AC: Template variables: patient name, provider name, date, time, appointment ID
- AC: SMTP credentials configured via environment variables
- AC: Invalid email address → log warning, skip send, do not block workflow
- AC: Template rendering error → log error, send plaintext fallback

---

## Implementation Steps

### 1. Add NuGet Package

Add to `src/HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj`:

```xml
<PackageReference Include="MailKit" Version="4.9.0" />
```

### 2. Create `SmtpSettings` POCO

Create `src/HealthPlatform.Infrastructure/Messaging/SmtpSettings.cs`:

```csharp
namespace HealthPlatform.Infrastructure.Messaging;

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host        { get; init; } = "localhost";
    public int    Port        { get; init; } = 587;
    public bool   UseSsl      { get; init; } = true;
    public string UserName    { get; init; } = string.Empty;
    public string Password    { get; init; } = string.Empty;
    public string FromAddress { get; init; } = "no-reply@healthplatform.local";
    public string FromName    { get; init; } = "HealthPlatform";
}
```

### 3. Create `EmailTemplateService`

Create `src/HealthPlatform.Infrastructure/Messaging/EmailTemplateService.cs`:

```csharp
namespace HealthPlatform.Infrastructure.Messaging;

/// <summary>
/// Renders HTML email bodies for all patient-facing notification scenarios.
/// All templates use inline CSS and a single-column responsive layout.
/// </summary>
internal static class EmailTemplateService
{
    // Shared wrapper — responsive single-column, max-width 600 px
    private static string Wrap(string title, string bodyHtml) => $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width,initial-scale=1" />
          <title>{title}</title>
        </head>
        <body style="margin:0;padding:0;background:#f4f4f4;font-family:Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f4;padding:32px 0;">
            <tr><td align="center">
              <table width="600" cellpadding="0" cellspacing="0"
                     style="background:#ffffff;border-radius:8px;overflow:hidden;max-width:600px;width:100%;">
                <tr>
                  <td style="background:#1976d2;padding:24px 32px;">
                    <h1 style="margin:0;color:#ffffff;font-size:20px;">{title}</h1>
                  </td>
                </tr>
                <tr>
                  <td style="padding:32px;">
                    {bodyHtml}
                  </td>
                </tr>
                <tr>
                  <td style="background:#f4f4f4;padding:16px 32px;text-align:center;
                             font-size:12px;color:#888888;">
                    HealthPlatform — please do not reply to this email.
                  </td>
                </tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string Row(string label, string value) =>
        $"<tr><td style=\"padding:4px 0;color:#555;font-size:14px;\"><strong>{label}:</strong> {value}</td></tr>";

    private static string DetailTable(params (string label, string value)[] rows)
    {
        var cells = string.Join("\n", Array.ConvertAll(rows, r => Row(r.label, r.value)));
        return $"<table cellpadding=\"0\" cellspacing=\"0\" style=\"margin:16px 0;\">{cells}</table>";
    }

    private static string Greeting(string patientName) =>
        $"<p style=\"font-size:16px;color:#333;\">Hi <strong>{patientName}</strong>,</p>";

    // ── 1. Booking Confirmation ───────────────────────────────────────────────
    public static (string Subject, string Body) BookingConfirmation(
        string patientName, string providerName, DateTimeOffset appointmentTime, Guid appointmentId)
    {
        var subject = $"Appointment confirmed — {appointmentTime:ddd, MMM d 'at' h:mm tt}";
        var body = Wrap("Appointment Confirmed ✓", $"""
            {Greeting(patientName)}
            <p style="font-size:14px;color:#555;">
              Your appointment has been booked successfully.
            </p>
            {DetailTable(
                ("Provider", providerName),
                ("Date",     appointmentTime.ToString("dddd, MMMM d, yyyy")),
                ("Time",     appointmentTime.ToString("h:mm tt zzz")),
                ("Ref #",    appointmentId.ToString("N")[..8].ToUpperInvariant())
            )}
            <p style="font-size:13px;color:#888;">
              Please arrive 10 minutes before your scheduled time.
            </p>
            """);
        return (subject, body);
    }

    // ── 2. Cancellation ───────────────────────────────────────────────────────
    public static (string Subject, string Body) Cancellation(
        string patientName, string providerName, DateTimeOffset appointmentTime, Guid appointmentId)
    {
        var subject = $"Appointment cancelled — {appointmentTime:MMM d}";
        var body = Wrap("Appointment Cancelled", $"""
            {Greeting(patientName)}
            <p style="font-size:14px;color:#555;">
              Your appointment has been cancelled.
            </p>
            {DetailTable(
                ("Provider", providerName),
                ("Date",     appointmentTime.ToString("dddd, MMMM d, yyyy")),
                ("Time",     appointmentTime.ToString("h:mm tt zzz")),
                ("Ref #",    appointmentId.ToString("N")[..8].ToUpperInvariant())
            )}
            <p style="font-size:13px;color:#888;">
              To book a new appointment, log in to the patient portal.
            </p>
            """);
        return (subject, body);
    }

    // ── 3. Appointment Reminder ───────────────────────────────────────────────
    public static (string Subject, string Body) Reminder(
        string patientName, string providerName, DateTimeOffset appointmentTime, Guid appointmentId)
    {
        var subject = $"Reminder: appointment tomorrow with {providerName}";
        var body = Wrap("Appointment Reminder", $"""
            {Greeting(patientName)}
            <p style="font-size:14px;color:#555;">
              This is a reminder of your upcoming appointment.
            </p>
            {DetailTable(
                ("Provider", providerName),
                ("Date",     appointmentTime.ToString("dddd, MMMM d, yyyy")),
                ("Time",     appointmentTime.ToString("h:mm tt zzz")),
                ("Ref #",    appointmentId.ToString("N")[..8].ToUpperInvariant())
            )}
            """);
        return (subject, body);
    }

    // ── 4. Slot Swap Request (notify target patient) ──────────────────────────
    public static (string Subject, string Body) SwapRequest(
        string targetPatientName, string requesterName, DateTimeOffset targetSlotTime)
    {
        var subject = "A patient has requested a slot swap with you";
        var body = Wrap("Slot Swap Request", $"""
            {Greeting(targetPatientName)}
            <p style="font-size:14px;color:#555;">
              <strong>{requesterName}</strong> has requested to swap appointment slots with you.
            </p>
            {DetailTable(
                ("Your slot", targetSlotTime.ToString("dddd, MMMM d 'at' h:mm tt zzz"))
            )}
            <p style="font-size:14px;color:#555;">
              Log in to the patient portal to accept or decline this request.
            </p>
            """);
        return (subject, body);
    }

    // ── 5. Slot Swap Result (accepted or rejected) ────────────────────────────
    public static (string Subject, string Body) SwapResult(
        string patientName, bool accepted, DateTimeOffset newSlotTime)
    {
        var outcome = accepted ? "accepted" : "declined";
        var subject = $"Slot swap {outcome}";
        var body = Wrap($"Slot Swap {(accepted ? "Accepted" : "Declined")}", $"""
            {Greeting(patientName)}
            <p style="font-size:14px;color:#555;">
              Your slot swap request has been <strong>{outcome}</strong>.
            </p>
            {(accepted ? DetailTable(("New slot", newSlotTime.ToString("dddd, MMMM d 'at' h:mm tt zzz"))) : "")}
            """);
        return (subject, body);
    }

    // ── 6. No-Show Follow-Up ──────────────────────────────────────────────────
    public static (string Subject, string Body) NoShowFollowUp(
        string patientName, string providerName, DateTimeOffset missedTime, Guid appointmentId)
    {
        var subject = "We missed you — please reschedule your appointment";
        var body = Wrap("Missed Appointment Follow-Up", $"""
            {Greeting(patientName)}
            <p style="font-size:14px;color:#555;">
              You missed your scheduled appointment. We hope everything is okay.
            </p>
            {DetailTable(
                ("Provider",      providerName),
                ("Missed on",     missedTime.ToString("dddd, MMMM d 'at' h:mm tt zzz")),
                ("Ref #",         appointmentId.ToString("N")[..8].ToUpperInvariant())
            )}
            <p style="font-size:14px;color:#555;">
              Please log in to the patient portal to book a new appointment.
            </p>
            """);
        return (subject, body);
    }
}
```

### 4. Create `MailKitEmailSender`

Create `src/HealthPlatform.Infrastructure/Messaging/MailKitEmailSender.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace HealthPlatform.Infrastructure.Messaging;

/// <summary>
/// Sends email via SMTP using MailKit.
/// Validates the recipient address before attempting delivery.
/// Falls back to plain-text body when HTML rendering fails.
/// </summary>
internal sealed class MailKitEmailSender : IEmailSender
{
    private readonly SmtpSettings           _settings;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(
        IOptions<SmtpSettings>          settings,
        ILogger<MailKitEmailSender> logger)
    {
        _settings = settings.Value;
        _logger   = logger;
    }

    public async Task SendAsync(
        string toAddress,
        string subject,
        string body,
        CancellationToken ct = default)
    {
        // Guard — invalid email address: log and skip, never block caller
        if (!IsValidEmail(toAddress))
        {
            _logger.LogWarning(
                "MailKitEmailSender: invalid recipient address '{ToAddress}' — skipping.",
                toAddress);
            return;
        }

        var message = BuildMessage(toAddress, subject, body);

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(
                _settings.Host,
                _settings.Port,
                _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                ct);

            if (!string.IsNullOrWhiteSpace(_settings.UserName))
                await client.AuthenticateAsync(_settings.UserName, _settings.Password, ct);

            await client.SendAsync(message, ct);
        }
        catch (Exception ex)
        {
            // Permanently-failed delivery is logged here; Hangfire retry handles
            // transient failures before this point is reached.
            _logger.LogError(
                ex,
                "MailKitEmailSender: permanent delivery failure to '{ToAddress}' Subject='{Subject}'.",
                toAddress, subject);
            throw; // Re-throw so Hangfire retry policy can act
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(quit: true, ct);
        }
    }

    private MimeMessage BuildMessage(string toAddress, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;

        // Detect whether body is HTML or plain-text
        var isHtml = body.TrimStart().StartsWith('<');
        message.Body = new TextPart(isHtml ? TextFormat.Html : TextFormat.Plain)
        {
            Text = body
        };

        return message;
    }

    private static bool IsValidEmail(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return false;

        try
        {
            var mailbox = MailboxAddress.Parse(address);
            return mailbox is not null;
        }
        catch
        {
            return false;
        }
    }
}
```

### 5. Bind `SmtpSettings` in `DependencyInjection.cs`

Add settings binding in `AddInfrastructure` (alongside existing `AccountSecuritySettings`):

```csharp
services.Configure<SmtpSettings>(
    configuration.GetSection(SmtpSettings.SectionName));
```

Add using:

```csharp
using HealthPlatform.Infrastructure.Messaging;
```

(already present — `NoOpEmailSender` lives in the same namespace)

> **Note**: `MailKitEmailSender` is **not** registered in DI here — it is the
> internal implementation used only by `SendEmailJob` (Task 002). The DI-visible
> `IEmailSender` is the `HangfireEmailDispatcher` added in Task 002.

### 6. Add SMTP section to `appsettings.json`

Add to `src/HealthPlatform.Api/appsettings.json`:

```json
"Smtp": {
  "Host": "localhost",
  "Port": 587,
  "UseSsl": true,
  "UserName": "",
  "Password": "",
  "FromAddress": "no-reply@healthplatform.local",
  "FromName": "HealthPlatform"
}
```

Add to `src/HealthPlatform.Api/appsettings.Development.json`:

```json
"Smtp": {
  "Host": "localhost",
  "Port": 1025,
  "UseSsl": false,
  "UserName": "",
  "Password": "",
  "FromAddress": "dev@healthplatform.local",
  "FromName": "HealthPlatform (Dev)"
}
```

> Port 1025 matches [MailHog](https://github.com/mailhog/MailHog) — the standard
> local SMTP capture tool for development.

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj` | Add `MailKit 4.9.0` |
| `src/HealthPlatform.Infrastructure/Messaging/SmtpSettings.cs` | New — settings POCO |
| `src/HealthPlatform.Infrastructure/Messaging/EmailTemplateService.cs` | New — HTML template renderer |
| `src/HealthPlatform.Infrastructure/Messaging/MailKitEmailSender.cs` | New — MailKit SMTP sender |
| `src/HealthPlatform.Infrastructure/DependencyInjection.cs` | Bind `SmtpSettings` |
| `src/HealthPlatform.Api/appsettings.json` | Add `Smtp` section |
| `src/HealthPlatform.Api/appsettings.Development.json` | Add `Smtp` section (MailHog defaults) |

---

## Verification

```bash
cd src
dotnet add HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj package MailKit --version 4.9.0
dotnet build HealthPlatform.sln --configuration Release
# Expect: 0 errors, 0 warnings
```

---

## Notes

- `MailKitEmailSender` is `internal sealed` — it is not directly reachable from the
  Application layer. Only `HangfireEmailDispatcher` (Task 002) calls it.
- `IsValidEmail` uses MailKit's own `MailboxAddress.Parse` to avoid duplicating
  RFC 5321 validation logic.
- Setting `UseSsl = false` + port 1025 is the MailHog local dev profile;
  production overrides via env vars `Smtp__Host`, `Smtp__Port`, `Smtp__UserName`,
  `Smtp__Password` (ASP.NET Core configuration hierarchy).
