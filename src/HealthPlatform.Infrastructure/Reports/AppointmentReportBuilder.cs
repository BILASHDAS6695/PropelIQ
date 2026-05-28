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
                            c.Background(Colors.Blue.Darken2).Padding(5);

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

                        // Data rows (zebra striping)
                        var even = false;
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
