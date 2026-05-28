namespace HealthPlatform.Domain.ValueObjects;

/// <summary>
/// Typed representation of the JSONB intake payload stored in IntakeRecord.Data.
/// </summary>
public sealed record IntakeData
{
    public string ChiefComplaint { get; init; } = string.Empty;
    public List<string> Symptoms { get; init; } = [];
    public string Duration { get; init; } = string.Empty;
    public int Severity { get; init; } = 5; // 1–10
    public List<string> Medications { get; init; } = [];
    public List<string> Allergies { get; init; } = [];
    public string MedicalHistory { get; init; } = string.Empty;
}
