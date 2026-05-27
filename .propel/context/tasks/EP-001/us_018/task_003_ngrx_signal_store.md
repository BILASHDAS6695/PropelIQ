# Task 003: NgRx Signal Store for Auth State

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-018 |
| **Epic** | EP-001 |
| **Layer** | Angular — State Management |
| **Priority** | High |
| **Estimated Effort** | 2 hours |
| **Dependencies** | Task 001 — AuthService memory-only token must be in place first |

## Objective

US-018 AC-9 and TR-003 require auth state managed via **NgRx Signal Store** (`@ngrx/signals`). The current implementation uses plain `signal()` from `@angular/core` inside `AuthService`. This task migrates auth state to a proper `signalStore()` and keeps `AuthService` as a thin facade over it.

---

## What Changes

| | Current | After |
|---|---------|-------|
| State primitive | `signal<User \| null>()` in `AuthService` | `signalStore()` with typed state |
| Token | `signal<string \| null>()` in `AuthService` | Kept in `AuthService` (token is sensitive; store is a state container, not a token vault) |
| Computed | `computed()` in `AuthService` | `withComputed()` in store |
| Methods | Methods on `AuthService` | `withMethods()` in store; `AuthService` delegates |
| Guards | Inject `AuthService` | Unchanged — still inject `AuthService` facade |

> **Note:** The access token stays in `AuthService` (memory signal) and is NOT put in the store — the store should never serialize sensitive data.

---

## Implementation Steps

### 1. Install `@ngrx/signals`

```bash
cd src/health-platform-ui
npm install @ngrx/signals@latest
```

Verify the version matches Angular 21 compatibility (NgRx 19.x supports Angular 19+).

### 2. Create `src/app/core/auth/auth.store.ts`

```typescript
import { computed } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';

export interface AuthUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: 'patient' | 'staff' | 'admin';
}

interface AuthState {
  user: AuthUser | null;
  isLoading: boolean;
}

const initialState: AuthState = {
  user: null,
  isLoading: false,
};

export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ user }) => ({
    isAuthenticated: computed(() => user() !== null),
    userRole: computed(() => user()?.role ?? null),
    userId: computed(() => user()?.id ?? null),
  })),
  withMethods((store) => ({
    setUser(user: AuthUser): void {
      patchState(store, { user });
    },
    clearUser(): void {
      patchState(store, { user: null });
    },
    setLoading(isLoading: boolean): void {
      patchState(store, { isLoading });
    },
  })),
);
```

### 3. Update `src/app/core/auth/auth.service.ts` — Delegate state to `AuthStore`

Replace the `signal<User | null>()` and associated `computed()` with injection and delegation to `AuthStore`. Keep the `signal<string | null>()` for the access token (not in store):

```typescript
import { computed, inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, of } from 'rxjs';
import { catchError, finalize, map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AuthStore, AuthUser } from './auth.store';
import { signal } from '@angular/core';

// Re-export the User type for consumers that currently import from AuthService.
export type { AuthUser as User };

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly store = inject(AuthStore);
  private readonly token = signal<string | null>(null);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  // Expose store signals as the public API.
  readonly user = this.store.user;
  readonly isAuthenticated = this.store.isAuthenticated;
  readonly userRole = this.store.userRole;

  login(email: string, password: string): Observable<{ passwordChangeRequired: boolean; lockoutSecondsRemaining?: number }> {
    return this.http
      .post<{
        accessToken: string;
        refreshToken: string;
        expiresIn: number;
        passwordChangeRequired: boolean;
      }>(`${environment.apiUrl}/auth/login`, { email, password })
      .pipe(
        map((res) => {
          const payload = this.decodeJwtPayload(res.accessToken);
          const user: AuthUser = {
            id: payload.sub,
            email: payload.email ?? '',
            firstName: '',
            lastName: '',
            role: (payload.role?.toLowerCase() ?? 'patient') as AuthUser['role'],
          };
          this.store.setUser(user);
          this.token.set(res.accessToken);
          sessionStorage.setItem('auth_userId', payload.sub);
          localStorage.setItem('refresh_token', res.refreshToken);
          return { passwordChangeRequired: res.passwordChangeRequired ?? false };
        }),
      );
  }

  refresh(): Observable<void> {
    const userId = sessionStorage.getItem('auth_userId') ?? '';
    const refreshToken = localStorage.getItem('refresh_token') ?? '';
    return this.http
      .post<{ accessToken: string; refreshToken: string; expiresIn: number }>(
        `${environment.apiUrl}/auth/refresh`,
        { userId, refreshToken },
      )
      .pipe(
        map((res) => {
          const payload = this.decodeJwtPayload(res.accessToken);
          this.token.set(res.accessToken);
          sessionStorage.setItem('auth_userId', payload.sub);
          localStorage.setItem('refresh_token', res.refreshToken);
        }),
      );
  }

  logout(): void {
    this.http
      .post<void>(`${environment.apiUrl}/auth/logout`, {})
      .pipe(
        catchError(() => of(void 0)),
        finalize(() => {
          this.clearAuthState();
          this.router.navigate(['/login']);
        }),
      )
      .subscribe();
  }

  clearAuthState(): void {
    this.store.clearUser();
    this.token.set(null);
    sessionStorage.removeItem('auth_userId');
    localStorage.removeItem('refresh_token');
  }

  getToken(): string | null {
    return this.token();
  }

  changePassword(currentPassword: string, newPassword: string, confirmNewPassword: string): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/auth/change-password`, {
      currentPassword,
      newPassword,
      confirmNewPassword,
    });
  }

  register(payload: {
    email: string;
    firstName: string;
    lastName: string;
    phone?: string | null;
    password: string;
    confirmPassword: string;
  }): Observable<{ userId: string }> {
    return this.http.post<{ userId: string }>(`${environment.apiUrl}/auth/register`, payload);
  }

  private decodeJwtPayload(token: string): { sub: string; email?: string; role?: string } {
    const payloadPart = token.split('.')[1];
    if (!payloadPart) throw new Error('Invalid JWT token');
    const base64 = payloadPart.replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=');
    return JSON.parse(atob(padded)) as { sub: string; email?: string; role?: string };
  }
}
```

> **Note:** `login()` return type is updated to `Observable<{ passwordChangeRequired: boolean }>` in preparation for Task 004. The `LoginComponent` must be updated accordingly in that task.

### 4. Update `src/app/core/index.ts` (if it exists)

Export `AuthStore` so consumers can import it:
```typescript
export { AuthStore } from './auth/auth.store';
export type { AuthUser as User } from './auth/auth.store';
```

### 5. Verify guards still work

`auth.guard.ts` and `role.guard.ts` inject `AuthService` and call `isAuthenticated()` / `userRole()` — these now delegate to the store. No changes needed in guard files.

---

## Affected Files

| File | Change |
|------|--------|
| `package.json` | +`@ngrx/signals` |
| `src/app/core/auth/auth.store.ts` | **Created** — NgRx Signal Store |
| `src/app/core/auth/auth.service.ts` | Delegates state to `AuthStore`; `login()` return type updated |
| `src/app/core/index.ts` | +export for `AuthStore` |

---

## Acceptance Criteria

- [ ] `@ngrx/signals` installed and in `package.json`
- [ ] `AuthStore` created with state `{ user, isLoading }` and computed `{ isAuthenticated, userRole, userId }`
- [ ] `AuthService.user`, `AuthService.isAuthenticated`, `AuthService.userRole` all delegate to store signals
- [ ] `AuthGuard` and `RoleGuard` continue to work without modification
- [ ] `InactivityTimerService` continues to work (uses `AuthService.logout()`)
- [ ] `npm run build` passes (0 TypeScript errors)

## Verification

```bash
cd src/health-platform-ui
npm install
npm run build
```
