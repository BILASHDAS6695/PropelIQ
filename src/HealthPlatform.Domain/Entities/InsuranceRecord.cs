using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class InsuranceRecord : BaseEntity
{
    public string ProviderName { get; set; } = string.Empty;
    public string MemberId { get; set; } = string.Empty;
    public InsuranceStatus Status { get; set; }
}
