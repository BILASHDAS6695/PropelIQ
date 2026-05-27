# Task 003: Surface Lockout Seconds & PasswordChangeRequired in Login Response

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-017 |
| **Epic** | EP-001 |
| **Layer** | API — Controller / DTOs |
| **Priority** | High |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 002 — `LoginResult.LockoutSecondsRemaining` and `LoginResult.PasswordChangeRequired` must be populated |

## Objective

Update `AuthController.Login` to:
1. Include `lockoutSecondsRemaining` in `ProblemDetails.Extensions` when the account is locked (401 response).
2. Pass `PasswordChangeRequired` through to the 200 `AuthTokenResponse`.

Update the `AuthTokenResponse` DTO to accept the 4th parameter.

---

## Implementation Steps

### 1. `AuthTokenResponse` DTO — Add `PasswordChangeRequired` field

In `AuthController.cs`, update the record:

```csharp
/// <summary>Successful authentication response.</summary>
public sealed record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    int    ExpiresIn,
    bool   PasswordChangeRequired = false);
```

### 2. `AuthController.Login` — Build lockout `ProblemDetails` and pass `PasswordChangeRequired`

Replace the 401/200 return block with:

```csharp
if (!result.IsSuccess)
{
    var problem = new ProblemDetails
    {
        Status = StatusCodes.Status401Unauthorized,
        Title  = "Authentication failed.",
        Detail = result.Error
    };

    if (result.LockoutSecondsRemaining.HasValue)
    {
        problem.Extensions["lockoutSecondsRemaining"] =
            result.LockoutSecondsRemaining.Value;
    }

    return Unauthorized(problem);
}

return Ok(new AuthTokenResponse(
    result.AccessToken!,
    result.RefreshToken!,
    result.ExpiresIn,
    result.PasswordChangeRequired));
```

### 3. Update XML doc on `Login` action

```csharp
/// <returns>
/// 200 OK — token pair with expiresIn; passwordChangeRequired is true when
///           the credential has expired (client must redirect to change-password).<br/>
/// 401 Unauthorized — invalid credentials, inactive account, or locked account
///                    (lockoutSecondsRemaining is included when locked).<br/>
/// 422 Unprocessable Entity — input validation failed.
/// </returns>
```

---

## API Contract

### POST /api/auth/login — Locked account (401)

```json
{
  "status": 401,
  "title": "Authentication failed.",
  "detail": "Account is locked. Try again in 843 seconds.",
  "lockoutSecondsRemaining": 843
}
```

### POST /api/auth/login — Success with expired password (200)

```json
{
  "accessToken": "eyJ...",
  "refreshToken": "abc...",
  "expiresIn": 900,
  "passwordChangeRequired": true
}
```

---

## Affected Files

| File | Change |
|------|--------|
| `src/HealthPlatform.Api/Controllers/AuthController.cs` | `Login` action + `AuthTokenResponse` DTO |

---

## Acceptance Criteria

- [ ] Locked account → 401 with `lockoutSecondsRemaining` in response extensions
- [ ] Not-locked failure → 401 **without** `lockoutSecondsRemaining`
- [ ] Successful login with expired credential → 200 with `passwordChangeRequired: true`
- [ ] Successful login with valid credential → 200 with `passwordChangeRequired: false`
- [ ] Existing `Refresh` endpoint still constructs `AuthTokenResponse` correctly (3-arg overload with default)
- [ ] `dotnet build` passes (0 errors)

## Verification

```powershell
cd src
dotnet build HealthPlatform.sln --no-restore
```
