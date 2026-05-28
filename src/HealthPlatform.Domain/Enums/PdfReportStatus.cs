namespace HealthPlatform.Domain.Enums;

public enum PdfReportStatus
{
    Pending = 0,   // Hangfire job queued; bytes not yet available
    Ready   = 1,   // PDF bytes stored; download link is valid
    Failed  = 2,   // Job failed; report is unusable
}
