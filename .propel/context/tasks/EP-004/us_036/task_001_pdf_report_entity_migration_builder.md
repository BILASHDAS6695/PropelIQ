# Task 001: PdfReport Entity, EF Migration & QuestPDF Builder

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-036 |
| **Epic** | EP-004 |
| **Layer** | Domain + Application (interface) + Infrastructure (QuestPDF + EF migration) |
| **Priority** | Low |
| **Estimated Effort** | 35 minutes |
| **Dependencies** | US-035 complete — `NotificationPreferences` migration applied; 43/43 tests green |

## Objective

1. **Install QuestPDF** NuGet package into the Infrastructure project.
2. **Add `PdfReportStatus` enum** to the Domain layer.
3. **Add `PdfReport` entity** to the Domain layer — persists generated report
   bytes with a download token and expiry timestamp.
4. **Register `PdfReport`** in `ApplicationDbContext` and add an
   `IEntityTypeConfiguration<PdfReport>` for EF Core.
5. **Apply EF Core migration** to create the `pdf_reports` table.
6. **Define `IAppointmentReportBuilder`** Application interface and supporting
   data records.
7. **Implement `AppointmentReportBuilder`** in the Infrastructure layer using
   QuestPDF fluent API.

---

## Acceptance Criteria Covered

- AC: PDF generated using QuestPDF library
- AC: Report header: clinic name, generation date, patient name
- AC: Report includes: patient name, appointment list (date, provider, status, visit reason)
- AC: No appointments in range → PDF with "No appointments found" message

---

## Implementation Steps

### 1. Install QuestPDF

Edit `src/HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj`.

Add inside the `<ItemGroup>` with other `<PackageReference>` elements:

```xml
<PackageReference Include="QuestPDF" Version="2024.10.4" />
```

---

### 2. Create `PdfReportStatus` enum

Create `src/HealthPlatform.Domain/Enums/PdfReportStatus.cs`:

```csharp
namespace HealthPlatform.Domain.Enums;

public enum PdfReportStatus
{
    Pending = 0,   // Hangfire job queued; bytes not yet available
    Ready   = 1,   // PDF bytes stored; download link is valid
    Failed  = 2,   // Job failed; report is unusable
}
```

---

### 3. Create `PdfReport` entity

Create `src/HealthPlatform.Domain/Entities/PdfReport.cs`:

```csharp
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

/// <summary>
/// Represents a generated (or in-progress) PDF appointment summary report.
/// Download tokens expire after <see cref="ExpiresAt"/>; the record is kept
/// for audit purposes but the download endpoint returns 410 Gone after expiry.
/// </summary>
public class PdfReport : AuditableEntity
{
    /// <summary>PatientProfile.Id for whom this report was generated.</summary>
    public Guid PatientId { get; set; }

    /// <summary>
    /// Unique opaque download token — included in the download URL.
    /// Initialised to a new GUID by the command handler before persisting.
    /// </summary>
    public Guid Token { get; set; } = Guid.NewGuid();

    /// <summary>Inclusive start of the date range used to filter appointments.</summary>
    public DateTimeOffset DateFrom { get; set; }

    /// <summary>Inclusive end of the date range used to filter appointments.</summary>
    public DateTimeOffset DateTo { get; set; }

    /// <summary>
    /// Raw PDF bytes. <c>null</c> while <see cref="Status"/> is
    /// <see cref="PdfReportStatus.Pending"/>.
    /// </summary>
    public byte[]? FileBytes { get; set; }

    public PdfReportStatus Status { get; set; } = PdfReportStatus.Pending;

    /// <summary>UTC timestamp after which the download link is invalid.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Number of appointments included in this report (capped at 100).
    /// Set by the handler before persisting.
    /// </summary>
    public int AppointmentCount { get; set; }

    public PatientProfile Patient { get; set; } = null!;
}
```

---

### 4. Add EF configuration

Create `src/HealthPlatform.Infrastructure/Persistence/Configurations/PdfReportConfiguration.cs`:

```csharp
using HealthPlatform.Domain.Entities;
using HealthPlatform.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HealthPlatform.Infrastructure.Persistence.Configurations;

internal sealed class PdfReportConfiguration : IEntityTypeConfiguration<PdfReport>
{
    public void Configure(EntityTypeBuilder<PdfReport> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Token)
            .IsRequired();

        builder.HasIndex(r => r.Token)
            .IsUnique();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.FileBytes)
            .HasColumnType("bytea");

        builder.Property(r => r.ExpiresAt)
            .IsRequired();

        builder.HasOne(r => r.Patient)
            .WithMany()
            .HasForeignKey(r => r.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

---

### 5. Register `PdfReport` in `ApplicationDbContext`

File: `src/HealthPlatform.Infrastructure/Persistence/ApplicationDbContext.cs`

Add after the existing `DbSet<SlotSwapRequest>` line:

```csharp
public DbSet<PdfReport> PdfReports => Set<PdfReport>();
```

---

### 6. Apply EF Core migration

```bash
cd src
dotnet ef migrations add AddPdfReports \
    --project HealthPlatform.Infrastructure \
    --startup-project HealthPlatform.Api \
    --output-dir Persistence/Migrations
```

Verify the generated migration creates:
- `pdf_reports` table with columns: `id`, `patient_id`, `token`, `date_from`,
  `date_to`, `file_bytes`, `status`, `expires_at`, `appointment_count`,
  plus `AuditableEntity` columns (`created_at`, `updated_at`, etc.)
- Unique index on `token`

---

### 7. Define `IAppointmentReportBuilder` interface

Create `src/HealthPlatform.Application/Interfaces/IAppointmentReportBuilder.cs`:

```csharp
namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Builds a PDF appointment summary report from structured data.
/// Implementations live in the Infrastructure layer (QuestPDF).
/// </summary>
public interface IAppointmentReportBuilder
{
    /// <summary>
    /// Generates the PDF document bytes synchronously.
    /// </summary>
    byte[] Build(AppointmentReportData data);
}

/// <summary>All data required to render one appointment summary PDF.</summary>
public sealed record AppointmentReportData(
    string PatientName,
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<AppointmentReportRow> Appointments);

/// <summary>One row in the appointment table within the PDF.</summary>
public sealed record AppointmentReportRow(
    DateTimeOffset SlotTime,
    string ProviderName,
    string Status,
    string? VisitReason);
```

---

### 8. Implement `AppointmentReportBuilder`

Create `src/HealthPlatform.Infrastructure/Reports/AppointmentReportBuilder.cs`:

```csharp
using HealthPlatform.Application.Interfaces;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HealthPlatform.Infrastructure.Reports;

/// <summary>
/// QuestPDF implementation of <see cref="IAppointmentReportBuilder"/>.
/// Generates a single A4 PDF containing the appointment history table.
/// </summary>
internal sealed class AppointmentReportBuilder : IAppointmentReportBuilder
{
    private const string ClinicName = "HealthPlatform Clinic";

    public byte[] Build(AppointmentReportData data)
    {
        // QuestPDF community licence (MIT) — set once per process
        QuestPDF.Settings.License = LicenseType.Community;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(10));

                // ── Header ────────────────────────────────────────────────
                page.Header().Column(col =>
                {
                    col.Item().Text(ClinicName)
                        .FontSize(16).Bold().FontColor(Colors.Blue.Darken2);

                    col.Item().Text(
                        $"Appointment Summary — {data.PatientName}")
                        .FontSize(12).SemiBold();

                    col.Item().Text(
                        $"Period: {data.From:dd MMM yyyy} – {data.To:dd MMM yyyy}   " +
                        $"Generated: {DateTimeOffset.UtcNow:dd MMM yyyy HH:mm} UTC")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);

                    col.Item().PaddingTop(4).LineHorizontal(1)
                        .LineColor(Colors.Blue.Darken2);
                });

                // ── Content ───────────────────────────────────────────────
                page.Content().PaddingVertical(8).Column(col =>
                {
                    if (data.Appointments.Count == 0)
                    {
                        col.Item().PaddingTop(20)
                            .AlignCenter()
                            .Text("No appointments found for the selected period.")
                            .FontColor(Colors.Grey.Darken1).Italic();
                        return;
                    }

                    col.Item().Table(table =>
                    {
                        // Column definitions
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2.5f); // Date/Time
                            cols.RelativeColumn(3f);   // Provider
                            cols.RelativeColumn(2f);   // Status
                            cols.RelativeColumn(4f);   // Visit Reason
                        });

                        // Header row
                        static IContainer HeaderCell(IContainer c) =>
                            c.Background(Colors.Blue.Darken2)
                             .Padding(5);

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell)
                                .Text("Date / Time").FontColor(Colors.White).Bold();
                            header.Cell().Element(HeaderCell)
                                .Text("Provider").FontColor(Colors.White).Bold();
                            header.Cell().Element(HeaderCell)
                                .Text("Status").FontColor(Colors.White).Bold();
                            header.Cell().Element(HeaderCell)
                                .Text("Visit Reason").FontColor(Colors.White).Bold();
                        });

                        // Data rows
                        bool even = false;
                        foreach (var row in data.Appointments)
                        {
                            even = !even;
                            var bg = even ? Colors.Grey.Lighten4 : Colors.White;

                            static IContainer DataCell(IContainer c, string colour) =>
                                c.Background(colour).Padding(5);

                            table.Cell().Element(c => DataCell(c, bg))
                                .Text(row.SlotTime.ToString("dd MMM yyyy HH:mm"));
                            table.Cell().Element(c => DataCell(c, bg))
                                .Text(row.ProviderName);
                            table.Cell().Element(c => DataCell(c, bg))
                                .Text(row.Status);
                            table.Cell().Element(c => DataCell(c, bg))
                                .Text(row.VisitReason ?? "—");
                        }
                    });

                    col.Item().PaddingTop(8)
                        .Text($"Total: {data.Appointments.Count} appointment(s)")
                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                // ── Footer ────────────────────────────────────────────────
                page.Footer().AlignCenter()
                    .Text(x =>
                    {
                        x.Span("Page ").FontSize(9);
                        x.CurrentPageNumber().FontSize(9);
                        x.Span(" of ").FontSize(9);
                        x.TotalPages().FontSize(9);
                    });
            });
        }).GeneratePdf();
    }
}
```

---

### 9. Register in DI

File: `src/HealthPlatform.Infrastructure/DependencyInjection.cs`

Add the following line after the `AddScoped<INotificationPreferenceChecker, ...>()` registration:

```csharp
services.AddTransient<IAppointmentReportBuilder, AppointmentReportBuilder>();
```

Add the `using` directive at the top if not already present:

```csharp
using HealthPlatform.Infrastructure.Reports;
```

---

## Verification

```bash
cd src
dotnet build HealthPlatform.sln --no-incremental -v q 2>&1 | Select-String "error|Error" | Select-Object -First 10
dotnet test HealthPlatform.Tests/HealthPlatform.Tests.csproj -v q 2>&1 | tail -3
```

Expected: build clean, 43/43 tests pass (no new tests in this task).

---

## Notes

- QuestPDF `LicenseType.Community` is set once per process — calling `Build()`
  multiple times is safe (subsequent calls are no-ops for the license line).
- `file_bytes` uses PostgreSQL `bytea` type, which has no size limit by default.
  Reports up to ~20 MB are practical; the 100-appointment cap ensures this stays
  well under 1 MB in practice.
- The `PdfReport` entity uses `AuditableEntity` (has `IsDeleted` soft-delete).
  The soft-delete query filter means expired/deleted reports are invisible to
  normal queries — no clean-up job needed for expired entries.
