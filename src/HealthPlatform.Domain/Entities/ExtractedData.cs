using System.Text.Json;
using HealthPlatform.Domain.Common;
using HealthPlatform.Domain.Enums;

namespace HealthPlatform.Domain.Entities;

public class ExtractedData : BaseEntity
{
    public Guid DocumentId { get; set; }
    public Guid PatientId { get; set; }
    public DataCategory DataCategory { get; set; }
    public JsonDocument? DataJson { get; set; }
    public int ConfidenceScore { get; set; }
    public int PageNumber { get; set; }

    public ClinicalDocument Document { get; set; } = null!;
}
