using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class MedicalCode : BaseEntity
{
    public Guid PatientViewId { get; set; }
    public MedicalCodeType CodeType { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public Guid? VerifiedBy { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }

    public PatientView360 PatientView { get; set; } = null!;
}
