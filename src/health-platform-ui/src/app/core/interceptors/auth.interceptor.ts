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
  return (
    url.includes('/auth/login') || url.includes('/auth/refresh') || url.includes('/auth/logout')
  );
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
      return newToken
        ? next(addBearer(req, newToken))
        : throwError(() => new Error('No token after refresh'));
    }),
    catchError((refreshErr) => {
      isRefreshing = false;
      refreshDone$.next(false);
      authService.clearAuthState();
      return throwError(() => refreshErr);
    }),
  );
}
