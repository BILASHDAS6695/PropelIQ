# Task 005: AuthController Login/Refresh Endpoints and Angular LoginComponent

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-014 |
| **Epic** | EP-001 |
| **Layer** | API (endpoints) · Frontend (Angular form) |
| **Priority** | Critical |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | Task 002 (LoginCommand), Task 003 (RefreshTokenCommand) |

## Objective

1. Add `POST /api/auth/login` and `POST /api/auth/refresh` endpoints to the
   existing `AuthController`.
2. Replace the `LoginComponent` stub with a full PrimeNG reactive login form
   that calls the real API and stores the token pair.
3. Update `AuthService.login()` to issue the real HTTP call, parse the response,
   and persist tokens to `sessionStorage`.

## Acceptance Criteria Covered

- AC: Login endpoint accepts email + password, returns accessToken, refreshToken, expiresIn
- AC: Invalid credentials return 401 with generic message
- AC: Successful login → Angular stores token and navigates to dashboard

## Files to Modify

| File | Change |
|------|--------|
| `src/HealthPlatform.Api/Controllers/AuthController.cs` | Add `Login` and `Refresh` action + DTOs |
| `src/health-platform-ui/src/app/core/auth/auth.service.ts` | Replace stub `login()` with real HTTP call; add `refresh()` |
| `src/health-platform-ui/src/app/features/auth/login/login.component.ts` | Replace stub with PrimeNG reactive form |

---

## Implementation Steps

### 1. Extend `AuthController.cs`

Append inside the `AuthController` class (before the closing `}`), and add
DTOs at the bottom of the file alongside existing `RegisterRequest`/`RegisterResponse`.

#### New action methods

```csharp
/// <summary>
/// Authenticates a user and issues a JWT access token + refresh token.
/// </summary>
/// <returns>
/// 200 OK — token pair with expiresIn.<br/>
/// 401 Unauthorized — invalid credentials, inactive, or locked account.<br/>
/// 422 Unprocessable Entity — input validation failed.
/// </returns>
[HttpPost("login")]
[ProducesResponseType(typeof(AuthTokenResponse),        StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails),           StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status422UnprocessableEntity)]
public async Task<IActionResult> Login(
    [FromBody] LoginRequest request,
    CancellationToken ct)
{
    var result = await _sender.Send(
        new LoginCommand(request.Email, request.Password), ct);

    if (!result.IsSuccess)
    {
        return Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title  = "Authentication failed.",
            Detail = result.Error
        });
    }

    return Ok(new AuthTokenResponse(
        result.AccessToken!,
        result.RefreshToken!,
        result.ExpiresIn));
}

/// <summary>
/// Issues a new token pair from a valid refresh token (single-use rotation).
/// </summary>
/// <returns>
/// 200 OK — new token pair.<br/>
/// 401 Unauthorized — refresh token invalid or expired.
/// </returns>
[HttpPost("refresh")]
[ProducesResponseType(typeof(AuthTokenResponse), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails),    StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> Refresh(
    [FromBody] RefreshRequest request,
    CancellationToken ct)
{
    var result = await _sender.Send(
        new RefreshTokenCommand(request.UserId, request.RefreshToken), ct);

    if (!result.IsSuccess)
    {
        return Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title  = "Token refresh failed.",
            Detail = result.Error
        });
    }

    return Ok(new AuthTokenResponse(
        result.AccessToken!,
        result.RefreshToken!,
        result.ExpiresIn));
}
```

#### New DTOs (append after existing `RegisterResponse` at bottom of file)

```csharp
/// <summary>Payload for POST /api/auth/login.</summary>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Payload for POST /api/auth/refresh.</summary>
public sealed record RefreshRequest(Guid UserId, string RefreshToken);

/// <summary>Successful authentication response.</summary>
public sealed record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    int    ExpiresIn);
```

---

### 2. Update `AuthService` (`auth.service.ts`)

Replace the mock `login()` method body and add a `refresh()` method.

#### Updated `login()` method

```typescript
login(email: string, password: string): Observable<void> {
  return this.http
    .post<{ accessToken: string; refreshToken: string; expiresIn: number }>(
      `${environment.apiUrl}/auth/login`,
      { email, password }
    )
    .pipe(
      map((res) => {
        // Decode userId + role from the JWT payload (base64url middle segment).
        const payload = JSON.parse(atob(res.accessToken.split('.')[1]));
        const user: User = {
          id:        payload.sub,
          email:     payload.email,
          firstName: '',       // not in JWT — fetch from /api/users/me if needed
          lastName:  '',
          role:      payload.role?.toLowerCase() as User['role'],
        };
        this.currentUser.set(user);
        this.token.set(res.accessToken);
        sessionStorage.setItem('auth_token',    res.accessToken);
        sessionStorage.setItem('auth_userId',   payload.sub);
        sessionStorage.setItem('auth_user',     JSON.stringify(user));
        localStorage.setItem('refresh_token',   res.refreshToken);
      })
    );
}
```

#### New `refresh()` method (append after `login()`)

```typescript
refresh(): Observable<void> {
  const userId       = sessionStorage.getItem('auth_userId') ?? '';
  const refreshToken = localStorage.getItem('refresh_token') ?? '';

  return this.http
    .post<{ accessToken: string; refreshToken: string; expiresIn: number }>(
      `${environment.apiUrl}/auth/refresh`,
      { userId, refreshToken }
    )
    .pipe(
      map((res) => {
        const payload = JSON.parse(atob(res.accessToken.split('.')[1]));
        this.token.set(res.accessToken);
        sessionStorage.setItem('auth_token',  res.accessToken);
        localStorage.setItem('refresh_token', res.refreshToken);
      })
    );
}
```

#### Updated `loadFromStorage()` method

Ensure the existing `loadFromStorage()` correctly rehydrates from storage:

```typescript
private loadFromStorage(): void {
  const token = sessionStorage.getItem('auth_token');
  const user  = sessionStorage.getItem('auth_user');
  if (token && user) {
    this.token.set(token);
    this.currentUser.set(JSON.parse(user));
  }
}
```

#### Add missing `import`s at top of file

```typescript
import { map } from 'rxjs/operators';
```

---

### 3. Replace `LoginComponent` stub

**File:** `src/health-platform-ui/src/app/features/auth/login/login.component.ts`

```typescript
import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    InputTextModule,
    PasswordModule,
    MessageModule,
  ],
  template: `
    <div class="auth-page flex align-items-center justify-content-center min-h-screen">
      <div class="auth-card surface-card p-4 shadow-2 border-round" style="width: 100%; max-width: 420px;">
        <h1 class="text-center text-2xl font-semibold mb-4">Sign In</h1>

        <p-message
          *ngIf="registered"
          severity="success"
          text="Account created — please sign in."
          styleClass="mb-3 w-full">
        </p-message>

        <p-message
          *ngIf="serverError"
          severity="error"
          [text]="serverError"
          styleClass="mb-3 w-full">
        </p-message>

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>

          <div class="field mb-3">
            <label for="email" class="block mb-1 font-medium">Email</label>
            <input
              id="email"
              type="email"
              pInputText
              formControlName="email"
              class="w-full"
              [class.ng-invalid]="isInvalid('email')"
              autocomplete="username" />
            <small class="p-error" *ngIf="isInvalid('email')">
              Enter a valid email address.
            </small>
          </div>

          <div class="field mb-4">
            <label for="password" class="block mb-1 font-medium">Password</label>
            <p-password
              inputId="password"
              formControlName="password"
              [feedback]="false"
              [toggleMask]="true"
              styleClass="w-full"
              inputStyleClass="w-full"
              [class.ng-invalid]="isInvalid('password')"
              autocomplete="current-password">
            </p-password>
            <small class="p-error" *ngIf="isInvalid('password')">
              Password is required.
            </small>
          </div>

          <p-button
            type="submit"
            label="Sign In"
            styleClass="w-full"
            [loading]="loading"
            [disabled]="form.invalid || loading">
          </p-button>

        </form>

        <p class="text-center mt-3 text-sm">
          Don't have an account?
          <a routerLink="/register" class="text-primary font-medium">Create one</a>
        </p>
      </div>
    </div>
  `,
})
export class LoginComponent implements OnInit {
  private readonly fb      = inject(FormBuilder);
  private readonly auth    = inject(AuthService);
  private readonly router  = inject(Router);
  private readonly route   = inject(ActivatedRoute);

  form: FormGroup = this.fb.group({
    email:    ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  loading     = false;
  serverError = '';
  registered  = false;

  ngOnInit(): void {
    this.registered = this.route.snapshot.queryParamMap.get('registered') === 'true';
  }

  isInvalid(field: string): boolean {
    const c = this.form.get(field);
    return !!(c?.invalid && c.touched);
  }

  submit(): void {
    if (this.form.invalid) return;

    this.loading     = true;
    this.serverError = '';

    const { email, password } = this.form.value;

    this.auth.login(email, password).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading     = false;
        this.serverError =
          err?.error?.detail ?? 'Sign in failed. Please check your credentials.';
      },
    });
  }
}
```

---

## Design Notes

### JWT Decode in the Browser
The `atob(token.split('.')[1])` approach decodes the JWT payload without a
library. For production hardening, the `jwt-decode` npm package provides safer
base64url decoding; add it with `npm install jwt-decode` and:

```typescript
import { jwtDecode } from 'jwt-decode';
const payload = jwtDecode<{ sub: string; email: string; role: string }>(res.accessToken);
```

### `firstName` / `lastName` Not in JWT
The JWT claims include `sub`, `email`, `role`, and `sid` only. First/last names
are not stored there (payload size). The `AuthService` sets them to empty strings;
a follow-up story can add a `GET /api/users/me` call after login to hydrate
the full profile.

### Token Storage
| Token | Storage | Rationale |
|-------|---------|-----------|
| `accessToken` | `sessionStorage` | Cleared on tab close; short-lived anyway |
| `refreshToken` | `localStorage` | Must survive page refresh; 7-day lifetime |

### Registered Banner
The `?registered=true` query parameter (set by `RegisterComponent` on success)
triggers the green "Account created" banner, improving the UX flow from US-013.

---

## Acceptance Checklist

- [ ] `POST /api/auth/login` added to `AuthController`, returns 200/401/422
- [ ] `POST /api/auth/refresh` added, returns 200/401
- [ ] `LoginRequest`, `RefreshRequest`, `AuthTokenResponse` DTOs appended to file
- [ ] `AuthService.login()` issues real HTTP call and stores tokens
- [ ] `AuthService.refresh()` method added
- [ ] `LoginComponent` stub replaced with PrimeNG reactive form
- [ ] Registered-success banner shown when `?registered=true` query param present
- [ ] Solution builds with 0 errors; Angular VS Code reports 0 errors
