# Task 003: Routing Configuration with Lazy-Loaded Feature Routes

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-002 |
| **Epic** | EP-TECH |
| **Layer** | Frontend / Routing |
| **Priority** | Critical |
| **Estimated Effort** | 2 hours |
| **Dependencies** | Task 001, Task 002 |

## Objective

Configure Angular Router with `provideRouter()`, `withComponentInputBinding()`, and lazy-loaded feature routes for all major application areas. Each feature module gets a placeholder component and its own route file.

## Implementation Steps

### 1. Configure App Routes

**File:** `src/app/app.routes.ts`

```typescript
import { Routes } from '@angular/router';
import { AppLayoutComponent } from './layout/app-layout.component';

export const routes: Routes = [
  {
    path: '',
    component: AppLayoutComponent,
    children: [
      {
        path: 'dashboard',
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then(m => m.DASHBOARD_ROUTES),
      },
      {
        path: 'booking',
        loadChildren: () =>
          import('./features/booking/booking.routes').then(m => m.BOOKING_ROUTES),
      },
      {
        path: 'intake',
        loadChildren: () =>
          import('./features/intake/intake.routes').then(m => m.INTAKE_ROUTES),
      },
      {
        path: 'clinical',
        loadChildren: () =>
          import('./features/clinical/clinical.routes').then(m => m.CLINICAL_ROUTES),
      },
      {
        path: 'admin',
        loadChildren: () =>
          import('./features/admin/admin.routes').then(m => m.ADMIN_ROUTES),
      },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login.component').then(m => m.LoginComponent),
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./features/auth/register/register.component').then(m => m.RegisterComponent),
  },
  { path: '**', redirectTo: 'dashboard' },
];
```

### 2. Create Dashboard Feature Routes

**File:** `src/app/features/dashboard/dashboard.routes.ts`

```typescript
import { Routes } from '@angular/router';

export const DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./dashboard.component').then(m => m.DashboardComponent),
  },
];
```

**File:** `src/app/features/dashboard/dashboard.component.ts`

```typescript
import { Component } from '@angular/core';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  template: `<h1>Dashboard</h1><p>Patient dashboard coming soon.</p>`,
})
export class DashboardComponent {}
```

### 3. Create Booking Feature Routes

**File:** `src/app/features/booking/booking.routes.ts`

```typescript
import { Routes } from '@angular/router';

export const BOOKING_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./book-appointment/book-appointment.component').then(m => m.BookAppointmentComponent),
  },
  {
    path: 'appointments',
    loadComponent: () =>
      import('./my-appointments/my-appointments.component').then(m => m.MyAppointmentsComponent),
  },
];
```

**File:** `src/app/features/booking/book-appointment/book-appointment.component.ts`

```typescript
import { Component } from '@angular/core';

@Component({
  selector: 'app-book-appointment',
  standalone: true,
  template: `<h1>Book Appointment</h1><p>Provider selection & scheduling coming soon.</p>`,
})
export class BookAppointmentComponent {}
```

**File:** `src/app/features/booking/my-appointments/my-appointments.component.ts`

```typescript
import { Component } from '@angular/core';

@Component({
  selector: 'app-my-appointments',
  standalone: true,
  template: `<h1>My Appointments</h1><p>Appointment list coming soon.</p>`,
})
export class MyAppointmentsComponent {}
```

### 4. Create Intake Feature Routes

**File:** `src/app/features/intake/intake.routes.ts`

```typescript
import { Routes } from '@angular/router';

export const INTAKE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./intake-landing/intake-landing.component').then(m => m.IntakeLandingComponent),
  },
];
```

**File:** `src/app/features/intake/intake-landing/intake-landing.component.ts`

```typescript
import { Component } from '@angular/core';

@Component({
  selector: 'app-intake-landing',
  standalone: true,
  template: `<h1>Intake</h1><p>Chat & form-based intake coming soon.</p>`,
})
export class IntakeLandingComponent {}
```

### 5. Create Clinical Feature Routes

**File:** `src/app/features/clinical/clinical.routes.ts`

```typescript
import { Routes } from '@angular/router';

export const CLINICAL_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./documents/documents.component').then(m => m.DocumentsComponent),
  },
];
```

**File:** `src/app/features/clinical/documents/documents.component.ts`

```typescript
import { Component } from '@angular/core';

@Component({
  selector: 'app-documents',
  standalone: true,
  template: `<h1>Documents</h1><p>Document upload & NER viewer coming soon.</p>`,
})
export class DocumentsComponent {}
```

### 6. Create Admin Feature Routes

**File:** `src/app/features/admin/admin.routes.ts`

```typescript
import { Routes } from '@angular/router';

export const ADMIN_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./admin-dashboard/admin-dashboard.component').then(m => m.AdminDashboardComponent),
  },
];
```

**File:** `src/app/features/admin/admin-dashboard/admin-dashboard.component.ts`

```typescript
import { Component } from '@angular/core';

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  template: `<h1>Admin</h1><p>User management & audit logs coming soon.</p>`,
})
export class AdminDashboardComponent {}
```

### 7. Create Auth Placeholder Components

**File:** `src/app/features/auth/login/login.component.ts`

```typescript
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="auth-page">
      <h1>Sign In</h1>
      <p>Login form coming soon.</p>
      <a routerLink="/register">Create account</a>
    </div>
  `,
})
export class LoginComponent {}
```

**File:** `src/app/features/auth/register/register.component.ts`

```typescript
import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="auth-page">
      <h1>Create Account</h1>
      <p>Registration form coming soon.</p>
      <a routerLink="/login">Already have an account?</a>
    </div>
  `,
})
export class RegisterComponent {}
```

## Acceptance Criteria

- [ ] `provideRouter(routes, withComponentInputBinding())` configured in `app.config.ts`
- [ ] Five feature areas lazy-loaded: dashboard, booking, intake, clinical, admin
- [ ] Auth routes (login, register) load outside the app shell layout
- [ ] Default route redirects to `/dashboard`
- [ ] Wildcard route catches unknown paths
- [ ] Each feature has its own `*.routes.ts` file
- [ ] All placeholder components render without errors
- [ ] Navigation between routes works via sidebar links
- [ ] Production build chunk-splits feature routes (verify via `ng build` stats)

## Verification

```bash
ng build --configuration production --stats-json
# Inspect dist/stats.json for lazy chunks: booking, intake, clinical, admin, dashboard
ng serve  # Navigate between routes
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| TR-001 | Angular Router with standalone |
| TR-007 | Router guards (structure ready) |
| US-002 AC-3 | Lazy-loaded feature routes |
