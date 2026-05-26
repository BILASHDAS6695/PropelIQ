using System.Text.Json;
using HealthPlatform.Domain.Common;

namespace HealthPlatform.Domain.Entities;

public class PatientView360 : BaseEntity
{
    public Guid PatientId { get; set; }
    public JsonDocument? ConsolidatedDataJson { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
    public int ConflictCount { get; set; }

    public PatientProfile Patient { get; set; } = null!;
    public ICollection<DataConflict> DataConflicts { get; set; } = [];
    public ICollection<MedicalCode> MedicalCodes { get; set; } = [];
}
