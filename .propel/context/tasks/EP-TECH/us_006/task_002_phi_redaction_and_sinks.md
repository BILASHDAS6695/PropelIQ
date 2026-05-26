# Task 002: PHI Redaction & Logging Sinks

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-006 |
| **Epic** | EP-TECH |
| **Layer** | API / Infrastructure (cross-cutting) |
| **Priority** | Critical |
| **Estimated Effort** | 1 hour |
| **Dependencies** | Task 001 (Serilog setup complete) |

## Objective

Implement PHI-safe log redaction that masks email addresses, phone numbers,
and dates of birth before they reach any sink. Configure a daily-rotating file
sink for development and a JSON console sink for production. PHI masking must
be applied at the Serilog pipeline level — not at call sites — so it cannot be
accidentally bypassed.

## Implementation Steps

### 1. Create PHI Destructuring Policy

**File:** `src/HealthPlatform.Api/Logging/PhiRedactionPolicy.cs`

```csharp
using Serilog.Core;
using Serilog.Events;
using System.Text.RegularExpressions;

namespace HealthPlatform.Api.Logging;

/// <summary>
/// Serilog destructuring policy that masks PHI fields in log events.
/// Masks: email addresses, US phone numbers, ISO-8601 date-of-birth values.
/// Applied at the pipeline level — call sites need no special treatment.
/// </summary>
public sealed partial class PhiRedactionPolicy : IDestructuringPolicy
{
    // Matches RFC 5322 simplified email pattern
    [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    // Matches common US phone formats: (555) 123-4567, 555-123-4567, +15551234567
    [GeneratedRegex(@"(\+?1?\s?)?(\(?\d{3}\)?[\s.\-]?)(\d{3}[\s.\-]?\d{4})", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    // Matches ISO date that could represent a DOB (YYYY-MM-DD surrounded by non-digits)
    [GeneratedRegex(@"\b(19|20)\d{2}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])\b", RegexOptions.Compiled)]
    private static partial Regex DobRegex();

    public bool TryDestructure(object value, ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue? result)
    {
        if (value is string str)
        {
            var redacted = DobRegex().Replace(
                PhoneRegex().Replace(
                    EmailRegex().Replace(str, "[email-redacted]"),
                    "[phone-redacted]"),
                "[dob-redacted]");

            if (!ReferenceEquals(redacted, str))
            {
                result = new ScalarValue(redacted);
                return true;
            }
        }

        result = null;
        return false;
    }
}
```

### 2. Create PHI-Aware Log Event Enricher

The destructuring policy only applies when Serilog destructures objects. To
catch PHI in plain string log message templates, also add a log event enricher
that scans rendered messages.

**File:** `src/HealthPlatform.Api/Logging/PhiSanitizingEnricher.cs`

```csharp
using Serilog.Core;
using Serilog.Events;
using System.Text.RegularExpressions;

namespace HealthPlatform.Api.Logging;

/// <summary>
/// Serilog enricher that sanitises rendered message text for PHI before the
/// event reaches any sink. Operates on MessageTemplate tokens.
/// </summary>
public sealed partial class PhiSanitizingEnricher : ILogEventEnricher
{
    [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(\+?1?\s?)?(\(?\d{3}\)?[\s.\-]?)(\d{3}[\s.\-]?\d{4})", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"\b(19|20)\d{2}-(0[1-9]|1[0-2])-(0[1-9]|[12]\d|3[01])\b", RegexOptions.Compiled)]
    private static partial Regex DobRegex();

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        foreach (var property in logEvent.Properties.ToList())
        {
            if (property.Value is ScalarValue { Value: string str })
            {
                var sanitised = Sanitise(str);
                if (sanitised != str)
                {
                    logEvent.AddOrUpdateProperty(
                        propertyFactory.CreateProperty(property.Key, sanitised));
                }
            }
        }
    }

    private static string Sanitise(string value) =>
        DobRegex().Replace(
            PhoneRegex().Replace(
                EmailRegex().Replace(value, "[email-redacted]"),
                "[phone-redacted]"),
            "[dob-redacted]");
}
```

### 3. Register PHI Redaction in Serilog Configuration (`Program.cs`)

Update the `UseSerilog` lambda (added in Task 001) to include the PHI policy
and enricher:

```csharp
builder.Host.UseSerilog((context, services, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
                 .ReadFrom.Services(services)
                 .Enrich.FromLogContext()
                 .Enrich.WithMachineName()
                 .Enrich.WithEnvironmentName()
                 .Enrich.WithThreadId()
                 .Enrich.With<PhiSanitizingEnricher>()           // PHI masking
                 .Destructure.With<PhiRedactionPolicy>());        // PHI masking for objects
```

### 4. Add Rolling File Sink Package

```xml
<PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />
```

### 5. Configure Sinks in `appsettings.Development.json`

Extend the `Serilog` configuration to add a daily-rotating file sink in
development. Files older than 31 days are automatically pruned:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft": "Information",
        "Microsoft.Hosting.Lifetime": "Information",
        "System": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/healthplatform-.json",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 31,
          "formatter": "Serilog.Formatting.Compact.CompactJsonFormatter, Serilog.Formatting.Compact"
        }
      }
    ]
  }
}
```

### 6. Add `logs/` to `.gitignore`

```
# Serilog rolling log files
src/HealthPlatform.Api/logs/
```

## Acceptance Criteria

- [ ] `PhiRedactionPolicy` and `PhiSanitizingEnricher` exist under `HealthPlatform.Api/Logging/`
- [ ] Both classes are registered in the `UseSerilog` pipeline (destructuring + enrichment)
- [ ] Logging an email address produces `[email-redacted]` in the sink output
- [ ] Logging a phone number produces `[phone-redacted]` in the sink output
- [ ] Logging a date in `YYYY-MM-DD` format produces `[dob-redacted]` in the sink output
- [ ] `appsettings.Development.json` configures a `File` sink with `rollingInterval: Day`
- [ ] `logs/` directory is in `.gitignore`
- [ ] `dotnet build` passes with `TreatWarningsAsErrors=true`

## Verification

```csharp
// Add a temporary test log call in development to verify redaction:
Log.Information("Patient email: test@example.com, DOB: 1985-03-15, Phone: 555-123-4567");
// Expected output: Patient email: [email-redacted], DOB: [dob-redacted], Phone: [phone-redacted]
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-006 AC-3 | PHI-safe log redaction (email, phone, DOB) |
| US-006 AC-4 | Rolling file sink (dev), console sink (prod) |
| TR-019 | Serilog structured logging |
