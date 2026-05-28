using HealthPlatform.Api.Authorization;
using HealthPlatform.Application.Features.Documents;
using HealthPlatform.Application.Features.Patients;
using HealthPlatform.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

/// <summary>
/// Patient management endpoints — quick-create (staff) and document upload (patient/staff).
/// </summary>
[ApiController]
[Route("api/patients")]
public sealed class PatientsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUser;

    public PatientsController(ISender sender, ICurrentUserService currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Quick-creates a patient profile for an unregistered walk-in.
    /// Creates a placeholder User (IsActive = false) and a PatientProfile.
    /// The patient cannot log in via the portal; this is a staff-only operation.
    /// </summary>
    /// <param name="request">Minimal patient details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 Created — <c>{ patientProfileId, userId }</c>.<br/>
    /// 422 Unprocessable Entity — validation failed.
    /// </returns>
    [HttpPost("quick-create")]
    [Authorize(Policy = PolicyNames.Staff)]
    [ProducesResponseType(typeof(QuickCreatePatientResult),  StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails),  StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> QuickCreate(
        [FromBody] QuickCreatePatientRequest request,
        CancellationToken                    ct)
    {
        var result = await _sender.Send(
            new QuickCreatePatientCommand(
                request.FirstName,
                request.LastName,
                request.Dob,
                request.Phone), ct);

        return CreatedAtAction(nameof(QuickCreate), new { id = result.PatientProfileId }, result);
    }

    /// <summary>
    /// Uploads a clinical document (PDF, PNG, JPG, JPEG, TIFF) for the specified patient.
    /// Files are encrypted at rest with AES-256-CBC before being written to disk.
    /// The <paramref name="patientId"/> route value is the patient's <c>User.Id</c>.
    /// </summary>
    /// <param name="patientId">Patient's User.Id (matches JWT sub for ownership check).</param>
    /// <param name="file">The document file (multipart/form-data).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 Created — <see cref="DocumentUploadResultDto"/>.<br/>
    /// 400 Bad Request — unsupported file type or missing file.<br/>
    /// 413 Payload Too Large — file exceeds 10 MB.<br/>
    /// 422 Unprocessable Entity — validation failed (magic-byte mismatch, etc.).
    /// </returns>
    [HttpPost("{patientId:guid}/documents")]
    [Authorize(Policy = PolicyNames.PatientOwnership)]
    [Consumes("multipart/form-data")]
    [RequestFormLimits(MultipartBodyLengthLimit = 10_485_760)]
    [ProducesResponseType(typeof(DocumentUploadResultDto),  StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails),           StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails),           StatusCodes.Status413RequestEntityTooLarge)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UploadDocument(
        Guid patientId,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ProblemDetails { Title = "No file provided." });

        if (file.Length > 10_485_760)
            return StatusCode(StatusCodes.Status413RequestEntityTooLarge,
                new ProblemDetails { Title = "File too large. Maximum size: 10 MB" });

        await using var stream = file.OpenReadStream();

        var result = await _sender.Send(new UploadDocumentCommand(
            patientId,
            file.FileName,
            file.ContentType,
            file.Length,
            stream), ct);

        return CreatedAtAction(nameof(GetDocuments), new { patientId }, result);
    }

    /// <summary>
    /// Returns all clinical documents uploaded by or for the specified patient,
    /// ordered by upload date descending.
    /// The <paramref name="patientId"/> route value is the patient's <c>User.Id</c>.
    /// </summary>
    /// <param name="patientId">Patient's User.Id (matches JWT sub for ownership check).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 OK — list of <see cref="DocumentSummaryDto"/>.</returns>
    [HttpGet("{patientId:guid}/documents")]
    [Authorize(Policy = PolicyNames.PatientOwnership)]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocuments(
        Guid patientId,
        CancellationToken ct)
    {
        var docs = await _sender.Send(new GetPatientDocumentsQuery(patientId), ct);
        return Ok(docs);
    }
}

/// <summary>Payload for quick-creating a walk-in patient profile.</summary>
public sealed record QuickCreatePatientRequest(
    string FirstName,
    string LastName,
    DateOnly Dob,
    string? Phone = null);
