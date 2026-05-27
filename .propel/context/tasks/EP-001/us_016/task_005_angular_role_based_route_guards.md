# Task 005: Angular Role-Based Route Guards

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-016 |
| **Epic** | EP-001 |
| **Layer** | Frontend (Angular Routing) |
| **Priority** | Critical |
| **Estimated Effort** | 40 minutes |
| **Dependencies** | Task 002 (backend policies inform role constants) |

## Objective

Apply the existing `authGuard` and `roleGuard` functions to the Angular route
configuration (`app.routes.ts`) so that:

1. All protected routes (`/dashboard`, `/booking`, `/intake`, `/clinical`, `/admin`)
   require authentication — redirect to `/login` if no valid session.
2. The `/admin` route further restricts access to the `admin` role — redirect to
   `/forbidden` for authenticated non-admin users.
3. A `/forbidden` route is added to display a consistent "Access Denied" view.
4. `roleGuard` is updated to redirect to `/forbidden` instead of `/dashboard` when a
   role check fails, preventing confusing navigation for staff and admin users who are
   already on the dashboard.

## Acceptance Criteria Covered

- AC: Patient endpoints accessible to patients (own), staff, admin
- AC: Admin endpoints accessible to admins only
- AC: Staff endpoints accessible to staff and admin
- AC: Unauthorized access returns 403 Forbidden (frontend equivalent: redirect to `/forbidden`)

## Files to Create / Modify

| File | Change |
|------|--------|
| `src/health-platform-ui/src/app/app.routes.ts` | **Modify** — add `canActivate` guards to protected routes; add `/forbidden` route |
| `src/health-platform-ui/src/app/core/guards/role.guard.ts` | **Modify** — redirect to `/forbidden` instead of `/dashboard` on role mismatch |
| `src/health-platform-ui/src/app/features/auth/forbidden/forbidden.component.ts` | **Create** — standalone 403 access-denied component |

---

## Implementation Steps

### 1. Update `roleGuard` redirect target

**File:** `src/health-platform-ui/src/app/core/guards/role.guard.ts`

Change the redirect destination from `/dashboard` to `/forbidden`:

```typescript
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';

export const roleGuard = (...allowedRoles: string[]): CanActivateFn => {
  return () => {
    const authService = inject(AuthService);
    const router = inject(Router);
    const userRole = authService.userRole();

    if (userRole && allowedRoles.includes(userRole)) {
      return true;
    }

    router.navigate(['/forbidden']);
    return false;
  };
};
```

### 2. Create `ForbiddenComponent`

**File:** `src/health-platform-ui/src/app/features/auth/forbidden/forbidden.component.ts`

```typescript
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-forbidden',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="forbidden-container" role="main" aria-labelledby="forbidden-title">
      <h1 id="forbidden-title">Access Denied</h1>
      <p>You do not have permission to view this page.</p>
      <a routerLink="/dashboard" aria-label="Return to dashboard">Return to Dashboard</a>
    </div>
  `,
  styles: [`
    .forbidden-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      min-height: 60vh;
      gap: 1rem;
      text-align: center;
    }
  `]
})
export class ForbiddenComponent {}
```

### 3. Update `app.routes.ts`

**File:** `src/health-platform-ui/src/app/app.routes.ts`

```typescript
import { Routes } from '@angular/router';
import { AppLayoutComponent } from './layout/app-layout.component';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  {
    path: '',
    component: AppLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
      {
        path: 'booking',
        loadChildren: () =>
          import('./features/booking/booking.routes').then((m) => m.BOOKING_ROUTES),
      },
      {
        path: 'intake',
        loadChildren: () =>
          import('./features/intake/intake.routes').then((m) => m.INTAKE_ROUTES),
      },
      {
        path: 'clinical',
        loadChildren: () =>
          import('./features/clinical/clinical.routes').then((m) => m.CLINICAL_ROUTES),
      },
      {
        path: 'admin',
        canActivate: [roleGuard('admin')],
        loadChildren: () =>
          import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
      },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./features/auth/register/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: 'forbidden',
    loadComponent: () =>
      import('./features/auth/forbidden/forbidden.component').then((m) => m.ForbiddenComponent),
  },
  { path: '**', redirectTo: 'dashboard' },
];
```

Key changes from the original:
- `canActivate: [authGuard]` added to the root layout route (guards all children).
- `canActivate: [roleGuard('admin')]` added to the `/admin` child route.
- `/forbidden` lazy-loaded route added.
- Original `/dashboard`, `/booking`, `/intake`, `/clinical` routes have no per-route
  `canActivate` — authentication is handled by the parent layout route guard.

---

## Role Mapping Reference

The Angular `AuthService` lowercases the role claim on login:

```typescript
role: payload.role?.toLowerCase() as User['role']
// 'Patient' → 'patient', 'Staff' → 'staff', 'Admin' → 'admin'
```

`roleGuard` string arguments must therefore use lowercase values:

| Backend `UserRole` | Frontend `role` string | `roleGuard` argument |
|--------------------|------------------------|----------------------|
| `Patient` | `'patient'` | `roleGuard('patient', 'staff', 'admin')` |
| `Staff` | `'staff'` | `roleGuard('staff', 'admin')` |
| `Admin` | `'admin'` | `roleGuard('admin')` |

Future staff-only sub-routes (e.g., `/queue`, `/walk-in`) should use
`canActivate: [roleGuard('staff', 'admin')]`.

---

## Design Notes

- `canActivate` on the parent layout route is the preferred Angular pattern for applying
  a guard to all child routes. It avoids duplicating `authGuard` on every child route
  and ensures new child routes added in the future are protected by default.
- `roleGuard('admin')` on the `/admin` child is evaluated **after** `authGuard` on
  the parent because Angular evaluates `canActivate` top-down from parent to child.
  An unauthenticated user is redirected to `/login` before the role check runs.
- The `/forbidden` route is intentionally placed outside the authenticated layout so
  it is always reachable, even in edge cases where the auth state is inconsistent.
- Redirecting role failures to `/forbidden` (instead of `/dashboard`) avoids a confusing
  infinite redirect loop for users who do not have access to the dashboard's default
  content.
- This guard is a **UX safeguard only** — the API enforces policy server-side (Tasks
  002–004). The frontend guard prevents unnecessary API round-trips but is not a security
  boundary.

## Acceptance Checklist

- [ ] `roleGuard` redirects to `/forbidden` on role mismatch
- [ ] `ForbiddenComponent` created as a standalone component
- [ ] Parent layout route has `canActivate: [authGuard]`
- [ ] `/admin` child route has `canActivate: [roleGuard('admin')]`
- [ ] `/forbidden` route added and resolves correctly
- [ ] Unauthenticated user navigating to `/dashboard` → redirected to `/login`
- [ ] Patient user navigating to `/admin` → redirected to `/forbidden`
- [ ] Admin user navigating to `/admin` → route loads correctly
- [ ] `ng build` completes with 0 errors
