using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class DataConflict : BaseEntity
{
    public Guid PatientViewId { get; set; }
    public string Field { get; set; } = string.Empty;
    public string ValueA { get; set; } = string.Empty;
    public string ValueB { get; set; } = string.Empty;
    public Guid SourceDocA { get; set; }
    public Guid SourceDocB { get; set; }
    public DataConflictSeverity Severity { get; set; }
    public ResolutionStatus ResolutionStatus { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    public PatientView360 PatientView { get; set; } = null!;
}
