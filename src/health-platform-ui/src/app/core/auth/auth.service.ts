import { inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, of } from 'rxjs';
import { catchError, finalize, map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AuthStore, AuthUser } from './auth.store';

// Re-export for consumers that currently import User from AuthService.
export type { AuthUser as User };

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly store = inject(AuthStore);
  private readonly token = signal<string | null>(null);
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly user = this.store.user;
  readonly isAuthenticated = this.store.isAuthenticated;
  readonly userRole = this.store.userRole;

  constructor() {
    // Access token is memory-only; it is lost on page refresh.
    // The auth interceptor will trigger a refresh automatically when the
    // first API call returns 401, picking up the refresh token from localStorage.
  }

  login(
    email: string,
    password: string,
  ): Observable<{ passwordChangeRequired: boolean; lockoutSecondsRemaining?: number }> {
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
      .post<{
        accessToken: string;
        refreshToken: string;
        expiresIn: number;
      }>(`${environment.apiUrl}/auth/refresh`, { userId, refreshToken })
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

  changePassword(
    currentPassword: string,
    newPassword: string,
    confirmNewPassword: string,
  ): Observable<void> {
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
    if (!payloadPart) {
      throw new Error('Invalid JWT token payload');
    }
    const base64 = payloadPart.replace(/-/g, '+').replace(/_/g, '/');
    const paddedBase64 = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=');
    return JSON.parse(atob(paddedBase64)) as { sub: string; email?: string; role?: string };
  }
}

