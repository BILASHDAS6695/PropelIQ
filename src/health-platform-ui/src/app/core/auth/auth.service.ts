import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

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
  private readonly http = inject(HttpClient);

  readonly user = this.currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this.currentUser() !== null);
  readonly userRole = computed(() => this.currentUser()?.role ?? null);

  private readonly router = inject(Router);

  constructor() {
    this.loadFromStorage();
  }

  login(email: string, password: string): Observable<void> {
    return this.http
      .post<{ accessToken: string; refreshToken: string; expiresIn: number }>(
        `${environment.apiUrl}/auth/login`,
        { email, password }
      )
      .pipe(
        map((res) => {
          const payload = JSON.parse(atob(res.accessToken.split('.')[1]));
          const user: User = {
            id:        payload.sub,
            email:     payload.email,
            firstName: '',
            lastName:  '',
            role:      payload.role?.toLowerCase() as User['role'],
          };
          this.currentUser.set(user);
          this.token.set(res.accessToken);
          sessionStorage.setItem('auth_token',  res.accessToken);
          sessionStorage.setItem('auth_userId', payload.sub);
          sessionStorage.setItem('auth_user',   JSON.stringify(user));
          localStorage.setItem('refresh_token', res.refreshToken);
        })
      );
  }

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
          sessionStorage.setItem('auth_userId', payload.sub);
          localStorage.setItem('refresh_token', res.refreshToken);
        })
      );
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

  register(payload: {
    email: string;
    firstName: string;
    lastName: string;
    phone?: string | null;
    password: string;
    confirmPassword: string;
  }): Observable<{ userId: string }> {
    return this.http.post<{ userId: string }>(
      `${environment.apiUrl}/auth/register`,
      payload,
    );
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
