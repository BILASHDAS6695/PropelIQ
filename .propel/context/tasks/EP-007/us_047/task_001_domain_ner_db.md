# Task 001: Domain Extension + Database Migration (NER Entities)

## Context

| Field                | Value                                                                                   |
|----------------------|-----------------------------------------------------------------------------------------|
| **User Story**       | US-047                                                                                  |
| **Epic**             | EP-007                                                                                  |
| **Layer**            | Domain / Database                                                                       |
| **Priority**         | Critical                                                                                |
| **Estimated Effort** | 30 minutes                                                                              |
| **Dependencies**     | US-046 complete — `ClinicalDocument` entity with `ExtractedText` + `OcrConfidenceScore` must exist |

## Objective

Extend `ClinicalDocument` with an `Entities` JSONB column that stores the NER
output (diagnosed conditions, medications, procedures, lab tests, etc.).
Define the `NerEntity` value record in the Application layer. Map the new
column in EF Core and create an idempotent SQL migration.

## Acceptance Criteria Covered

- AC: Each entity stored with text, type, start_offset, end_offset, confidence_score
- AC: Entities below threshold flagged as `low_confidence`
- AC: Entities stored as JSONB array in document record (`entities` field)
- AC: No entities found → empty array, document still marked "Processed"

---

## Implementation Steps

### 1. Create `NerEntity` Value Record

Create `src/HealthPlatform.Application/Features/Documents/NerEntity.cs`:

```csharp
namespace HealthPlatform.Application.Features.Documents;

/// <summary>
/// A single clinical named entity extracted from a document page.
/// Serialised as part of a JSONB array in <c>clinical_documents.entities</c>.
/// </summary>
public sealed record NerEntity(
    /// <summary>Surface text of the entity as it appears in the source.</summary>
    string Text,

    /// <summary>
    /// Normalised entity type: DIAGNOSIS | MEDICATION | PROCEDURE |
    /// LAB_TEST | LAB_VALUE | ANATOMY | SYMPTOM.
    /// </summary>
    string Type,

    /// <summary>Zero-based character start offset within the page text.</summary>
    int StartOffset,

    /// <summary>Zero-based character end offset (exclusive) within the page text.</summary>
    int EndOffset,

    /// <summary>Model confidence score 0.0–1.0.</summary>
    double ConfidenceScore,

    /// <summary>
    /// True when the confidence score is below the configured minimum threshold.
    /// Low-confidence entities are stored but should be treated as unverified.
    /// </summary>
    bool LowConfidence
);
```

---

### 2. Add `Entities` Property to `ClinicalDocument`

Edit `src/HealthPlatform.Domain/Entities/ClinicalDocument.cs`.

Add after `OcrConfidenceScore`:

```csharp
/// <summary>
/// JSON array of <see cref="HealthPlatform.Application.Features.Documents.NerEntity"/> objects.
/// Null until NER job completes. Stored as a PostgreSQL JSONB column.
/// </summary>
public string? Entities { get; set; }
```

Final property list (for reference):

```csharp
public string? ExtractedText { get; set; }       // from US-046
public double? OcrConfidenceScore { get; set; }   // from US-046
public string? Entities { get; set; }             // US-047 — NER output
```

---

### 3. Map `Entities` in EF Core Configuration

Edit `src/HealthPlatform.Infrastructure/Persistence/Configurations/ClinicalDocumentConfiguration.cs`.

Add after the `OcrConfidenceScore` mapping:

```csharp
builder.Property(cd => cd.Entities).HasColumnType("jsonb");
```

---

### 4. Add SQL Migration

Edit `infra/postgres/migrations.sql`.

Append at the end of the file:

```sql
-- US-047: NER Pipeline — add entities (JSONB) to clinical_documents
DO $EF$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'clinical_documents' AND column_name = 'entities'
    ) THEN
        ALTER TABLE clinical_documents
            ADD COLUMN entities jsonb NULL;
    END IF;
END $EF$;
```

---

## File Checklist

| File                                                                                    | Action   |
|-----------------------------------------------------------------------------------------|----------|
| `src/HealthPlatform.Application/Features/Documents/NerEntity.cs`                        | Create   |
| `src/HealthPlatform.Domain/Entities/ClinicalDocument.cs`                                | Modify   |
| `src/HealthPlatform.Infrastructure/Persistence/Configurations/ClinicalDocumentConfiguration.cs` | Modify   |
| `infra/postgres/migrations.sql`                                                         | Modify   |

## Verification

```bash
# .NET build should pass with 0 errors
dotnet build src/HealthPlatform.sln --configuration Release

# Inspect the migration block was appended
Select-String "entities" infra/postgres/migrations.sql
```
