import { Component, inject, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';
import { CommonModule } from '@angular/common';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    InputTextModule,
    PasswordModule,
    MessageModule,
  ],
  template: `
    <div class="auth-page flex align-items-center justify-content-center min-h-screen">
      <div
        class="auth-card surface-card p-4 shadow-2 border-round"
        style="width: 100%; max-width: 420px;"
      >
        <h1 class="text-center text-2xl font-semibold mb-4">Sign In</h1>

        @if (registered) {
          <p-message
            severity="success"
            text="Account created — please sign in."
            styleClass="mb-3 w-full"
          >
          </p-message>
        }

        @if (serverError) {
          <p-message severity="error" [text]="serverError" styleClass="mb-3 w-full"> </p-message>
        }

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <div class="field mb-3">
            <label for="email" class="block mb-1 font-medium">Email</label>
            <input
              id="email"
              type="email"
              pInputText
              formControlName="email"
              class="w-full"
              [class.ng-invalid]="isInvalid('email')"
              autocomplete="username"
            />
            @if (isInvalid('email')) {
              <small class="p-error"> Enter a valid email address. </small>
            }
          </div>

          <div class="field mb-4">
            <label for="password" class="block mb-1 font-medium">Password</label>
            <p-password
              inputId="password"
              formControlName="password"
              [feedback]="false"
              [toggleMask]="true"
              styleClass="w-full"
              inputStyleClass="w-full"
              [class.ng-invalid]="isInvalid('password')"
              autocomplete="current-password"
            >
            </p-password>
            @if (isInvalid('password')) {
              <small class="p-error"> Password is required. </small>
            }
          </div>

          <p-button
            type="submit"
            label="Sign In"
            styleClass="w-full"
            [loading]="loading"
            [disabled]="form.invalid || loading"
          >
          </p-button>
        </form>

        <p class="text-center mt-3 text-sm">
          Don't have an account?
          <a routerLink="/register" class="text-primary font-medium">Create one</a>
        </p>
      </div>
    </div>
  `,
})
export class LoginComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  form: FormGroup = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  loading = false;
  serverError = '';
  registered = false;

  ngOnInit(): void {
    this.registered = this.route.snapshot.queryParamMap.get('registered') === 'true';
  }

  isInvalid(field: string): boolean {
    const c = this.form.get(field);
    return !!(c?.invalid && c.touched);
  }

  submit(): void {
    if (this.form.invalid) return;

    this.loading = true;
    this.serverError = '';

    const { email, password } = this.form.value;

    this.auth.login(email, password).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigate(['/dashboard']);
      },
      error: (err) => {
        this.loading = false;
        this.serverError = err?.error?.detail ?? 'Sign in failed. Please check your credentials.';
      },
    });
  }
}
