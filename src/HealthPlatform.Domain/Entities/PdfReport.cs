using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

/// <summary>
/// Represents a generated (or in-progress) PDF appointment summary report.
/// Download tokens expire after <see cref="ExpiresAt"/>; the record is kept
/// for audit purposes but the download endpoint returns 404 after expiry.
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
