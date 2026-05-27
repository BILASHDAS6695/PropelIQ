using Microsoft.AspNetCore.Authorization;

namespace HealthPlatform.Api.Authorization;

/// <summary>
/// Requirement that enforces patient data ownership:
/// the authenticated user must be the resource owner (matching <c>patientId</c>
/// route parameter) OR hold a Staff / Admin role.
/// </summary>
public sealed class PatientOwnershipRequirement : IAuthorizationRequirement { }
