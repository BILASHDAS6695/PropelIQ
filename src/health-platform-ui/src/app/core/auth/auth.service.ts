import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
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

  login(email: string, _password: string): void {
    // TODO: Replace with real API call — _password will be sent to backend
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
