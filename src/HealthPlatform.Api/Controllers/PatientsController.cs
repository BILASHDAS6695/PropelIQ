using HealthPlatform.Api.Authorization;
using HealthPlatform.Application.Features.Patients;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

/// <summary>
/// Patient management endpoints for front-desk staff.
/// </summary>
[ApiController]
[Route("api/patients")]
public sealed class PatientsController : ControllerBase
{
    private readonly ISender _sender;

    public PatientsController(ISender sender) => _sender = sender;

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
}

/// <summary>Payload for quick-creating a walk-in patient profile.</summary>
public sealed record QuickCreatePatientRequest(
    string   FirstName,
    string   LastName,
    DateOnly Dob,
    string?  Phone = null);
