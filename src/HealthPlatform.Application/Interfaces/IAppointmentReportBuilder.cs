namespace HealthPlatform.Application.Interfaces;

/// <summary>
/// Builds a PDF appointment summary report from structured data.
/// Implementations live in the Infrastructure layer (QuestPDF).
/// </summary>
public interface IAppointmentReportBuilder
{
    /// <summary>Generates the PDF document bytes synchronously.</summary>
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
