# Task 001: Memory-Only Token Storage & 401 Refresh-and-Retry Interceptor

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-018 |
| **Epic** | EP-001 |
| **Layer** | Angular — Core / Auth Service / Interceptors |
| **Priority** | Critical |
| **Estimated Effort** | 2 hours |
| **Dependencies** | None — foundational security fix |

## Objective

US-018 AC-3 requires the access token to live **in memory only** (not `sessionStorage`/`localStorage`) to prevent XSS token theft. AC-5 requires the HTTP interceptor to attempt a silent token refresh on 401 before redirecting to login. These two concerns are addressed together because moving the token out of storage forces the interceptor to use the in-memory token exclusively.

---

## Gap Analysis (what the current code does wrong)

| Location | Current behaviour | Required behaviour |
|----------|-------------------|-------------------|
| `auth.service.ts` `login()` | Writes access token to `sessionStorage.auth_token` | Access token stored only in memory `signal` |
| `auth.service.ts` `refresh()` | Reads access token from `sessionStorage` | Reads userId from `sessionStorage` only; token from memory |
| `auth.service.ts` `loadFromStorage()` | Rehydrates token + user from `sessionStorage` on app init | Token NOT rehydrated — refresh token in `localStorage` triggers re-auth |
| `auth.service.ts` `clearAuthState()` | Removes `sessionStorage.auth_token` | Same (nothing to remove) |
| `auth.interceptor.ts` | Attaches token; no 401 handling | Attaches token |
| `error.interceptor.ts` | On 401: clears state, redirects | Moved to `auth.interceptor.ts` — attempt refresh first |

**Allowed storage:**
- `localStorage.refresh_token` — 7-day rotating token (acceptable for non-sensitive rotation key)
- `sessionStorage.auth_userId` — non-sensitive, needed for refresh payload
- Nothing else — access token MUST stay in the `AuthService` memory signal

---

## Implementation Steps

### 1. `src/app/core/auth/auth.service.ts` — Remove access token from sessionStorage

**Remove** these lines from `login()`:
```typescript
sessionStorage.setItem('auth_token', res.accessToken);
sessionStorage.setItem('auth_user', JSON.stringify(user));
```

**Keep** (userId needed for refresh call):
```typescript
sessionStorage.setItem('auth_userId', payload.sub);
localStorage.setItem('refresh_token', res.refreshToken);
```

**Update** `refresh()` — remove sessionStorage write for token:
```typescript
// Remove: sessionStorage.setItem('auth_token', res.accessToken);
// Keep:   sessionStorage.setItem('auth_userId', payload.sub);
```

**Update** `clearAuthState()` — remove token/user from sessionStorage:
```typescript
clearAuthState(): void {
  this.currentUser.set(null);
  this.token.set(null);
  sessionStorage.removeItem('auth_userId');
  localStorage.removeItem('refresh_token');
}
```

**Update** `loadFromStorage()` — token cannot be rehydrated; only restore userId for the refresh call:
```typescript
private loadFromStorage(): void {
  // Access token is memory-only; it is lost on page refresh.
  // The auth interceptor will trigger a refresh automatically when the
  // first API call returns 401, picking up the refresh token from localStorage.
}
```

### 2. `src/app/core/interceptors/auth.interceptor.ts` — Add 401 refresh-and-retry

Replace the existing `auth.interceptor.ts` with this implementation:

```typescript
import {
  HttpErrorResponse,
  HttpEvent,
  HttpHandlerFn,
  HttpInterceptorFn,
  HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { BehaviorSubject, Observable, throwError } from 'rxjs';
import { catchError, filter, switchMap, take } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';

// Single shared refresh state — prevents multiple simultaneous refresh calls.
let isRefreshing = false;
const refreshDone$ = new BehaviorSubject<boolean>(false);

export const authInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
): Observable<HttpEvent<unknown>> => {
  const authService = inject(AuthService);

  // Skip auth header for non-API requests.
  if (!req.url.startsWith(environment.apiUrl)) {
    return next(req);
  }

  const token = authService.getToken();
  const authedReq = token ? addBearer(req, token) : req;

  return next(authedReq).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status !== 401) return throwError(() => err);

      // Skip refresh for auth endpoints themselves (avoid infinite loops).
      if (isAuthEndpoint(req.url)) {
        authService.clearAuthState();
        return throwError(() => err);
      }

      return handle401(req, next, authService);
    }),
  );
};

function addBearer(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

function isAuthEndpoint(url: string): boolean {
  return url.includes('/auth/login') || url.includes('/auth/refresh') || url.includes('/auth/logout');
}

function handle401(
  req: HttpRequest<unknown>,
  next: HttpHandlerFn,
  authService: AuthService,
): Observable<HttpEvent<unknown>> {
  if (isRefreshing) {
    // Queue this request — it will retry once refresh completes.
    return refreshDone$.pipe(
      filter((done) => done),
      take(1),
      switchMap(() => {
        const newToken = authService.getToken();
        return newToken ? next(addBearer(req, newToken)) : throwError(() => new Error('No token'));
      }),
    );
  }

  isRefreshing = true;
  refreshDone$.next(false);

  return authService.refresh().pipe(
    switchMap(() => {
      isRefreshing = false;
      refreshDone$.next(true);
      const newToken = authService.getToken();
      return newToken ? next(addBearer(req, newToken)) : throwError(() => new Error('No token after refresh'));
    }),
    catchError((refreshErr) => {
      isRefreshing = false;
      refreshDone$.next(false);
      authService.clearAuthState();
      return throwError(() => refreshErr);
    }),
  );
}
```

### 3. `src/app/core/interceptors/error.interceptor.ts` — Remove 401 handler

The 401 case is now fully handled by `auth.interceptor.ts`. Update `error.interceptor.ts` to only handle non-401 errors:

```typescript
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 403) {
        router.navigate(['/forbidden']);
      } else if (error.status === 0) {
        console.error('Network error — API may be unreachable');
      }
      return throwError(() => error);
    }),
  );
};
```

### 4. `src/app/app.config.ts` — Ensure interceptor order

`auth.interceptor.ts` must come **before** `error.interceptor.ts` so it can handle 401 first:

```typescript
provideHttpClient(withInterceptors([authInterceptor, errorInterceptor, loadingInterceptor]))
```

This order is already correct — verify it remains unchanged.

---

## Affected Files

| File | Change |
|------|--------|
| `src/app/core/auth/auth.service.ts` | Remove sessionStorage writes for access token; update `loadFromStorage` |
| `src/app/core/interceptors/auth.interceptor.ts` | Add 401 refresh-and-retry with BehaviorSubject queue |
| `src/app/core/interceptors/error.interceptor.ts` | Remove 401 handler (moved to auth interceptor) |

---

## Security Notes

- Access token MUST NOT appear in `sessionStorage`, `localStorage`, cookies, or the DOM
- The refresh token in `localStorage` is acceptable — it is an opaque rotation key, not a credential by itself
- `isRefreshing` is module-level (singleton per bundle) which is correct for SPA: one refresh at a time
- `isAuthEndpoint` check prevents infinite retry loops on `/auth/login` and `/auth/refresh` failures

---

## Acceptance Criteria

- [ ] Access token never written to `sessionStorage` or `localStorage`
- [ ] Page refresh → token gone from memory → next API call gets 401 → interceptor triggers refresh → if refresh token valid, new token obtained silently
- [ ] If refresh fails → `clearAuthState()` called → redirect to `/login`
- [ ] Concurrent 401 requests during refresh: all queued, retried with new token after single refresh
- [ ] 401 on `/auth/login` or `/auth/refresh` itself does NOT trigger a refresh loop

## Verification

```bash
cd src/health-platform-ui
npm run build
# No TypeScript errors
```

Manual:
1. Log in → open DevTools → verify `sessionStorage` contains only `auth_userId` (no token)
2. Refresh page → next authenticated API call triggers `/auth/refresh` silently
3. Clear `localStorage.refresh_token` → refresh page → redirected to login
