using MediatR;

namespace HealthPlatform.Application.Features.PdfReport;

/// <summary>Requests a new (or cached) PDF appointment summary report.</summary>
public sealed record RequestAppointmentReportCommand(
    Guid           PatientProfileId,
    DateTimeOffset From,
    DateTimeOffset To) : IRequest<AppointmentReportResponseDto>;

/// <summary>
/// Response from <see cref="RequestAppointmentReportCommand"/>.
/// <see cref="IsReady"/> is <c>false</c> when the async Hangfire path was
/// taken; the client should poll <see cref="DownloadUrl"/> until the file
/// is available.
/// </summary>
public sealed record AppointmentReportResponseDto(
    Guid           Token,
    string         DownloadUrl,
    DateTimeOffset ExpiresAt,
    bool           IsReady);

/// <summary>Downloads a previously generated PDF by token.</summary>
public sealed record DownloadAppointmentReportQuery(
    Guid PatientProfileId,
    Guid Token) : IRequest<AppointmentReportFileDto>;

/// <summary>Contains the raw PDF bytes and a suggested filename.</summary>
public sealed record AppointmentReportFileDto(
    byte[] Bytes,
    string Filename);
