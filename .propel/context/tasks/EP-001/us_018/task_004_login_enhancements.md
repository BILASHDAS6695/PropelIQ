# Task 004: Login Enhancements — Lockout Feedback, Password-Expired Redirect & Forgot Password Link

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-018 |
| **Epic** | EP-001 |
| **Layer** | Angular — Feature / Auth |
| **Priority** | High |
| **Estimated Effort** | 1.5 hours |
| **Dependencies** | Task 003 — `AuthService.login()` must return `{ passwordChangeRequired }` |

## Objective

Three login-flow gaps to close:
1. **AC-10 — "Forgot Password" link** on the login form (route stub `/forgot-password`).
2. **US-017 integration** — when the API returns `passwordChangeRequired: true` after login, redirect to `/change-password` instead of `/dashboard`.
3. **US-017 integration** — when the API returns 401 with `lockoutSecondsRemaining`, display a specific countdown message ("Account locked. Try again in X minutes Y seconds.").

---

## Gap Analysis

| Gap | Current `login.component.ts` | Required |
|-----|------------------------------|---------|
| Lockout message | Shows generic `err?.error?.detail` | Detects `lockoutSecondsRemaining` in error extensions → formats countdown |
| `passwordChangeRequired` | No handling — always navigates `/dashboard` | On `true` → navigate `/change-password` |
| "Forgot Password" | Not present in template | Link below the sign-in button |

---

## Implementation Steps

### 1. `src/app/features/auth/login/login.component.ts` — Three targeted changes

#### Change A — Update `submit()` to handle `passwordChangeRequired`

The `AuthService.login()` now returns `Observable<{ passwordChangeRequired: boolean }>` (Task 003).

```typescript
submit(): void {
  if (this.form.invalid) return;
  this.loading = true;
  this.serverError = '';
  this.lockoutSeconds = null;

  const { email, password } = this.form.value;

  this.auth.login(email, password).subscribe({
    next: (result) => {
      this.loading = false;
      if (result.passwordChangeRequired) {
        this.router.navigate(['/change-password']);
      } else {
        this.router.navigate(['/dashboard']);
      }
    },
    error: (err: HttpErrorResponse) => {
      this.loading = false;
      const lockoutSecs = err.error?.lockoutSecondsRemaining as number | undefined;
      if (lockoutSecs != null && lockoutSecs > 0) {
        this.lockoutSeconds = lockoutSecs;
        this.serverError = this.formatLockout(lockoutSecs);
      } else {
        this.serverError = err?.error?.detail ?? 'Sign in failed. Please check your credentials.';
      }
    },
  });
}

private formatLockout(seconds: number): string {
  const m = Math.floor(seconds / 60);
  const s = seconds % 60;
  return m > 0
    ? `Account is locked. Try again in ${m} min ${s} sec.`
    : `Account is locked. Try again in ${s} seconds.`;
}
```

Add `lockoutSeconds = signal<number | null>(null)` (or plain property) to the component class.

Add `HttpErrorResponse` import:
```typescript
import { HttpErrorResponse } from '@angular/common/http';
```

#### Change B — Add "Forgot Password" link to template

After the sign-in button and before the "Don't have an account?" paragraph:

```html
<div class="text-center mt-2">
  <a routerLink="/forgot-password" class="text-sm text-primary">Forgot password?</a>
</div>

<p class="text-center mt-3 text-sm">
  Don't have an account?
  <a routerLink="/register" class="text-primary font-medium">Create one</a>
</p>
```

#### Change C — Show lockout-specific error banner

The existing `@if (serverError)` block already renders the error — no additional template change needed. The `formatLockout()` method provides the human-readable string.

Optionally add a `lockoutSeconds` display for a countdown (not required by AC — the message is sufficient).

### 2. Create `src/app/features/auth/forgot-password/forgot-password.component.ts` (stub)

The backend for password reset is not yet implemented. Create a minimal page that informs the user:

```typescript
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [RouterLink, ButtonModule],
  template: `
    <div class="auth-page flex align-items-center justify-content-center min-h-screen">
      <div class="auth-card surface-card p-4 shadow-2 border-round" style="width: 100%; max-width: 420px; text-align: center;">
        <h1 class="text-2xl font-semibold mb-3">Forgot Password</h1>
        <p class="mb-4 text-color-secondary">
          Password reset via email is coming soon.<br />
          Please contact your administrator to reset your password.
        </p>
        <p-button label="Back to Sign In" routerLink="/login" styleClass="w-full" />
      </div>
    </div>
  `,
})
export class ForgotPasswordComponent {}
```

### 3. `src/app/app.routes.ts` — Add `/forgot-password` route

```typescript
{
  path: 'forgot-password',
  loadComponent: () =>
    import('./features/auth/forgot-password/forgot-password.component').then(
      (m) => m.ForgotPasswordComponent,
    ),
},
```

Add this entry alongside the existing `/login` and `/register` routes (outside the `AppLayoutComponent` guard boundary).

---

## Full Updated `login.component.ts` Summary

| Section | Change |
|---------|--------|
| Imports | Add `HttpErrorResponse` |
| Class properties | Add `lockoutSeconds: number \| null = null` |
| `submit()` | Handle `result.passwordChangeRequired`; parse `lockoutSecondsRemaining` from 401 |
| `formatLockout()` | New private helper |
| Template | Add "Forgot password?" link between button and register link |

---

## Affected Files

| File | Change |
|------|--------|
| `src/app/features/auth/login/login.component.ts` | +lockout display, +`passwordChangeRequired` redirect, +forgot link |
| `src/app/features/auth/forgot-password/forgot-password.component.ts` | **Created** — stub page |
| `src/app/app.routes.ts` | +`/forgot-password` route |

---

## API Contract (from US-017)

**Login 401 — locked account:**
```json
{
  "status": 401,
  "title": "Authentication failed.",
  "detail": "Account is locked. Try again in 843 seconds.",
  "lockoutSecondsRemaining": 843
}
```

**Login 200 — expired credential:**
```json
{
  "accessToken": "...",
  "refreshToken": "...",
  "expiresIn": 900,
  "passwordChangeRequired": true
}
```

---

## Acceptance Criteria

- [ ] Login with locked account → 401 with `lockoutSecondsRemaining` → message shows "Account is locked. Try again in X min Y sec."
- [ ] Login with expired credential → `passwordChangeRequired: true` → navigates to `/change-password`
- [ ] Login with valid credential → navigates to `/dashboard` as before
- [ ] "Forgot password?" link appears on login page → navigates to `/forgot-password`
- [ ] `/forgot-password` page renders stub message and "Back to Sign In" button
- [ ] `npm run build` passes (0 errors)

## Verification

```bash
cd src/health-platform-ui
npm run build
```
