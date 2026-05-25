# Task 004: Core Services — AuthService, JWT Interceptor, Error Handler

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-002 |
| **Epic** | EP-TECH |
| **Layer** | Frontend / Core |
| **Priority** | Critical |
| **Estimated Effort** | 3 hours |
| **Dependencies** | Task 001 |

## Objective

Implement core infrastructure services: a stub AuthService using Angular signals, a functional JWT HttpInterceptor, and a global error handler — all registered via the standalone `provideHttpClient()` pattern.

## Implementation Steps

### 1. Create AuthService (Stub)

**File:** `src/app/core/auth/auth.service.ts`

```typescript
import { Injectable, signal, computed } from '@angular/core';
import { Router } from '@angular/router';

export interface User {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: 'patient' | 'staff' | 'admin';
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly currentUser = signal<User | null>(null);
  private readonly token = signal<string | null>(null);

  readonly user = this.currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly userRole = computed(() => this.currentUser()?.role ?? null);

  constructor(private readonly router: Router) {
    this.loadFromStorage();
  }

  login(email: string, password: string): void {
    // TODO: Replace with real API call
    const mockUser: User = {
      id: '1',
      email,
      firstName: 'Sarah',
      lastName: 'Chen',
      role: 'patient',
    };
    const mockToken = 'stub-jwt-token';

    this.currentUser.set(mockUser);
    this.token.set(mockToken);
    sessionStorage.setItem('auth_token', mockToken);
    sessionStorage.setItem('auth_user', JSON.stringify(mockUser));
  }

  logout(): void {
    this.currentUser.set(null);
    this.token.set(null);
    sessionStorage.removeItem('auth_token');
    sessionStorage.removeItem('auth_user');
    this.router.navigate(['/login']);
  }

  getToken(): string | null {
    return this.token();
  }

  private loadFromStorage(): void {
    const token = sessionStorage.getItem('auth_token');
    const userJson = sessionStorage.getItem('auth_user');
    if (token && userJson) {
      this.token.set(token);
      this.currentUser.set(JSON.parse(userJson) as User);
    }
  }
}
```

### 2. Create JWT Http Interceptor

**File:** `src/app/core/interceptors/auth.interceptor.ts`

```typescript
import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpEvent } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { environment } from '../../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
): Observable<HttpEvent<unknown>> => {
  const authService = inject(AuthService);
  const token = authService.getToken();

  // Only attach token to our API requests
  if (token && req.url.startsWith(environment.apiUrl)) {
    const cloned = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`,
      },
    });
    return next(cloned);
  }

  return next(req);
};
```

### 3. Create Global Error Handler Interceptor

**File:** `src/app/core/interceptors/error.interceptor.ts`

```typescript
import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      switch (error.status) {
        case 401:
          sessionStorage.removeItem('auth_token');
          sessionStorage.removeItem('auth_user');
          router.navigate(['/login']);
          break;
        case 403:
          router.navigate(['/dashboard']);
          break;
        case 0:
          console.error('Network error — API may be unreachable');
          break;
      }

      return throwError(() => error);
    }),
  );
};
```

### 4. Create Auth Guard

**File:** `src/app/core/guards/auth.guard.ts`

```typescript
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  router.navigate(['/login']);
  return false;
};
```

### 5. Create Role Guard

**File:** `src/app/core/guards/role.guard.ts`

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

    router.navigate(['/dashboard']);
    return false;
  };
};
```

### 6. Register Interceptors in app.config.ts

Update `app.config.ts` providers:

```typescript
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';

// Add to providers array:
provideHttpClient(withInterceptors([authInterceptor, errorInterceptor])),
```

### 7. Create Core Barrel Export

**File:** `src/app/core/index.ts`

```typescript
export { AuthService } from './auth/auth.service';
export { authInterceptor } from './interceptors/auth.interceptor';
export { errorInterceptor } from './interceptors/error.interceptor';
export { authGuard } from './guards/auth.guard';
export { roleGuard } from './guards/role.guard';
```

## Acceptance Criteria

- [ ] `AuthService` uses signals for reactive state (`user`, `isAuthenticated`, `userRole`)
- [ ] `AuthService.login()` stores token in `sessionStorage` (stub)
- [ ] `AuthService.logout()` clears state and redirects to `/login`
- [ ] `authInterceptor` attaches `Authorization: Bearer <token>` only to API-bound requests
- [ ] `errorInterceptor` handles 401 (redirect login), 403 (redirect dashboard), network errors
- [ ] `authGuard` blocks unauthenticated route access
- [ ] `roleGuard` restricts routes by user role
- [ ] `provideHttpClient(withInterceptors([...]))` registered in `app.config.ts`
- [ ] All services tree-shakeable (`providedIn: 'root'`)

## Verification

```bash
ng build --configuration production  # No compile errors
ng test --code-coverage  # AuthService unit tests pass (if tests written)
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| TR-004 | Angular HttpClient with interceptors |
| TR-007 | Angular Router Guards |
| US-002 AC-4 | AuthService, JWT interceptor, error handler |
