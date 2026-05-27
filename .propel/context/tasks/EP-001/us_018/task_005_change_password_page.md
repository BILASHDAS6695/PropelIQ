# Task 005: Change Password Page (`/change-password`)

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-018 |
| **Epic** | EP-001 |
| **Layer** | Angular — Feature / Auth |
| **Priority** | High |
| **Estimated Effort** | 1.5 hours |
| **Dependencies** | Task 003 — `AuthService.changePassword()` must exist; Task 004 — login redirects here on `passwordChangeRequired` |

## Objective

Create the `/change-password` route and `ChangePasswordComponent`. This page serves two entry points:
1. **Forced redirect** from login when `passwordChangeRequired: true` (90-day expiry from US-017).
2. **Self-service** — an authenticated user choosing to change their password from their profile (link to be added in a later story).

The form calls `POST /api/auth/change-password` (implemented in US-017 Task 004).

---

## Implementation Steps

### 1. Create `src/app/features/auth/change-password/change-password.component.ts`

```typescript
import { Component, inject } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../shared/services/toast.service';

function passwordsMatchValidator(control: AbstractControl): ValidationErrors | null {
  const newPwd = control.get('newPassword');
  const confirm = control.get('confirmNewPassword');
  if (!newPwd || !confirm) return null;
  return newPwd.value === confirm.value ? null : { passwordsMismatch: true };
}

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, PasswordModule, MessageModule],
  template: `
    <div class="auth-page flex align-items-center justify-content-center min-h-screen">
      <div
        class="auth-card surface-card p-4 shadow-2 border-round"
        style="width: 100%; max-width: 460px;"
      >
        <h1 class="text-2xl font-semibold mb-1 text-center">Change Password</h1>
        <p class="text-center text-color-secondary text-sm mb-4">
          Your password has expired or you chose to update it. Please set a new password.
        </p>

        @if (serverError) {
          <p-message severity="error" [text]="serverError" styleClass="w-full mb-4" />
        }

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <!-- Current Password -->
          <div class="field mb-3">
            <label for="currentPassword" class="block mb-1 font-medium">Current Password</label>
            <p-password
              inputId="currentPassword"
              formControlName="currentPassword"
              [feedback]="false"
              [toggleMask]="true"
              styleClass="w-full"
              inputStyleClass="w-full"
              [class.ng-invalid]="isInvalid('currentPassword')"
              autocomplete="current-password"
            />
            @if (isInvalid('currentPassword')) {
              <small class="p-error">Current password is required.</small>
            }
          </div>

          <!-- New Password -->
          <div class="field mb-3">
            <label for="newPassword" class="block mb-1 font-medium">New Password</label>
            <p-password
              inputId="newPassword"
              formControlName="newPassword"
              [toggleMask]="true"
              styleClass="w-full"
              inputStyleClass="w-full"
              [class.ng-invalid]="isInvalid('newPassword')"
              autocomplete="new-password"
            />
            @if (isInvalid('newPassword')) {
              <small class="p-error">{{ getPasswordError() }}</small>
            }
            <small class="text-color-secondary text-xs">
              Minimum 12 characters · uppercase · lowercase · number · special character
            </small>
          </div>

          <!-- Confirm New Password -->
          <div class="field mb-4">
            <label for="confirmNewPassword" class="block mb-1 font-medium">Confirm New Password</label>
            <p-password
              inputId="confirmNewPassword"
              formControlName="confirmNewPassword"
              [feedback]="false"
              [toggleMask]="true"
              styleClass="w-full"
              inputStyleClass="w-full"
              [class.ng-invalid]="isInvalid('confirmNewPassword') || form.hasError('passwordsMismatch')"
              autocomplete="new-password"
            />
            @if (form.hasError('passwordsMismatch') && form.get('confirmNewPassword')?.touched) {
              <small class="p-error">Passwords do not match.</small>
            }
          </div>

          <p-button
            type="submit"
            label="Change Password"
            styleClass="w-full"
            [loading]="loading"
            [disabled]="form.invalid || loading"
          />
        </form>
      </div>
    </div>
  `,
})
export class ChangePasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  loading = false;
  serverError = '';

  form: FormGroup = this.fb.group(
    {
      currentPassword:  ['', Validators.required],
      newPassword: [
        '',
        [
          Validators.required,
          Validators.minLength(12),
          Validators.pattern(/[A-Z]/),
          Validators.pattern(/[a-z]/),
          Validators.pattern(/[0-9]/),
          Validators.pattern(/[^a-zA-Z0-9]/),
        ],
      ],
      confirmNewPassword: ['', Validators.required],
    },
    { validators: passwordsMatchValidator },
  );

  isInvalid(field: string): boolean {
    const c = this.form.get(field);
    return !!(c?.invalid && c.touched);
  }

  getPasswordError(): string {
    const ctrl = this.form.get('newPassword');
    if (!ctrl) return '';
    if (ctrl.hasError('required'))    return 'New password is required.';
    if (ctrl.hasError('minlength'))   return 'Must be at least 12 characters.';
    if (ctrl.hasError('pattern'))     return 'Must include uppercase, lowercase, number, and special character.';
    return '';
  }

  submit(): void {
    if (this.form.invalid) return;

    this.loading = true;
    this.serverError = '';

    const { currentPassword, newPassword, confirmNewPassword } = this.form.value;

    this.auth.changePassword(currentPassword, newPassword, confirmNewPassword).subscribe({
      next: () => {
        this.loading = false;
        this.toast.success('Password changed successfully.');
        this.router.navigate(['/dashboard']);
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        this.serverError =
          err?.error?.detail ??
          err?.error?.errors?.NewPassword?.[0] ??
          'Password change failed. Please try again.';
      },
    });
  }
}
```

### 2. `src/app/app.routes.ts` — Add `/change-password` route (auth-protected)

This route belongs **inside** the `AppLayoutComponent` guard boundary (requires authentication). Add it to the `children` array:

```typescript
{
  path: 'change-password',
  loadComponent: () =>
    import('./features/auth/change-password/change-password.component').then(
      (m) => m.ChangePasswordComponent,
    ),
},
```

Add inside the `canActivate: [authGuard]` children — immediately after the `clinical` route and before `admin`.

> **Why inside the auth boundary?** The API endpoint requires a valid Bearer token (`[Authorize(Policy = PolicyNames.Patient)]`). The token is obtained during login (even when `passwordChangeRequired: true` — the access token is still returned in the 200 response). The `AppLayoutComponent` layout (sidebar/header) provides a consistent shell; the change-password card renders centered within the `<main>` content area.

### 3. `src/app/core/auth/auth.service.ts` — Verify `changePassword()` exists

Confirm the following method was added in Task 003:

```typescript
changePassword(currentPassword: string, newPassword: string, confirmNewPassword: string): Observable<void> {
  return this.http.post<void>(`${environment.apiUrl}/auth/change-password`, {
    currentPassword,
    newPassword,
    confirmNewPassword,
  });
}
```

If Task 003 was not completed first, add this method manually.

---

## Affected Files

| File | Change |
|------|--------|
| `src/app/features/auth/change-password/change-password.component.ts` | **Created** |
| `src/app/app.routes.ts` | +`/change-password` route inside `authGuard` boundary |

---

## UX Flow

```
Login (passwordChangeRequired: true)
  └─▶ POST /auth/login → 200 { accessToken, passwordChangeRequired: true }
        └─▶ AuthService.login() → navigate('/change-password')
              └─▶ ChangePasswordComponent
                    ├── Submit → POST /auth/change-password → 204
                    │     └─▶ Toast "Password changed" → navigate('/dashboard')
                    └── Error → 400 Bad Request → serverError displayed inline
```

---

## Validation Rules (mirrors ChangePasswordCommandValidator on backend)

| Field | Rules |
|-------|-------|
| `currentPassword` | Required |
| `newPassword` | Required · min 12 chars · ≥1 uppercase · ≥1 lowercase · ≥1 digit · ≥1 special char |
| `confirmNewPassword` | Must equal `newPassword` (cross-field validator) |

---

## Error Handling

| HTTP Status | Cause | Display |
|-------------|-------|---------|
| 400 | Wrong current password | `err.error.detail` |
| 400 | Password reused from history | `err.error.detail` |
| 422 | Validation failure | `err.error.errors.NewPassword[0]` |
| 401 | Token expired mid-session | Interceptor handles → `/login` redirect |

---

## Acceptance Criteria

- [ ] `/change-password` route exists and is auth-protected (inside `authGuard` boundary)
- [ ] Form has three fields: current password, new password, confirm new password
- [ ] Client-side validation mirrors backend rules (12 chars, upper/lower/digit/special, match)
- [ ] Correct current password + valid new password → 204 → success toast → redirect to `/dashboard`
- [ ] Wrong current password → 400 → `serverError` shown inline
- [ ] Reused password → 400 → `serverError` shown inline
- [ ] Validation errors → 422 → `serverError` shown inline
- [ ] `passwordChangeRequired: true` from login → user lands here automatically (Task 004)
- [ ] `npm run build` passes (0 errors)

## Verification

```bash
cd src/health-platform-ui
npm run build
```

Manual:
1. Log in with an account whose `CredentialExpiresAt` is in the past → redirected to `/change-password`
2. Enter wrong current password → see inline error
3. Enter same-as-current new password → see "cannot reuse" error
4. Enter valid new password → success toast → dashboard
