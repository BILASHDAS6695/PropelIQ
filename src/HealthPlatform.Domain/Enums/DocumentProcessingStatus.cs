namespace HealthPlatform.Domain.Enums;

public enum DocumentProcessingStatus
{
    /// <summary>File received, written to disk, DB record created.</summary>
    Uploaded,

    /// <summary>AI / NER extraction in progress.</summary>
    Processing,

    /// <summary>Extraction complete; awaiting clinician verification.</summary>
    Processed,

    /// <summary>Clinician has verified the extracted data.</summary>
    Verified,

    /// <summary>Upload, extraction, or verification failed.</summary>
    Failed,
}
