import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';
import { AuthService } from '../../../core/auth/auth.service';

// ── Custom cross-field validator ─────────────────────────────────────────────
function passwordsMatchValidator(control: AbstractControl): ValidationErrors | null {
  const password = control.get('password');
  const confirmPassword = control.get('confirmPassword');
  if (!password || !confirmPassword) return null;
  return password.value === confirmPassword.value ? null : { passwordsMismatch: true };
}

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    ButtonModule,
    InputTextModule,
    PasswordModule,
    MessageModule,
  ],
  template: `
    <div class="auth-page">
      <div class="auth-card">
        <h1 class="auth-title">Create Account</h1>

        @if (serverError) {
          <p-message severity="error" [text]="serverError" styleClass="w-full mb-4" />
        }

        <form [formGroup]="form" (ngSubmit)="onSubmit()" novalidate>
          <!-- Email -->
          <div class="field">
            <label for="email">Email address</label>
            <input
              pInputText
              id="email"
              type="email"
              formControlName="email"
              placeholder="you@example.com"
              autocomplete="email"
              class="w-full"
              [class.ng-invalid]="isInvalid('email')"
              aria-required="true"
              [attr.aria-describedby]="isInvalid('email') ? 'email-error' : null"
            />
            @if (isInvalid('email')) {
              <small id="email-error" class="field-error" role="alert">
                {{ getError('email') }}
              </small>
            }
          </div>

          <!-- First Name -->
          <div class="field">
            <label for="firstName">First name</label>
            <input
              pInputText
              id="firstName"
              type="text"
              formControlName="firstName"
              placeholder="Alice"
              autocomplete="given-name"
              class="w-full"
              [class.ng-invalid]="isInvalid('firstName')"
              aria-required="true"
            />
            @if (isInvalid('firstName')) {
              <small class="field-error" role="alert">First name is required.</small>
            }
          </div>

          <!-- Last Name -->
          <div class="field">
            <label for="lastName">Last name</label>
            <input
              pInputText
              id="lastName"
              type="text"
              formControlName="lastName"
              placeholder="Smith"
              autocomplete="family-name"
              class="w-full"
              [class.ng-invalid]="isInvalid('lastName')"
              aria-required="true"
            />
            @if (isInvalid('lastName')) {
              <small class="field-error" role="alert">Last name is required.</small>
            }
          </div>

          <!-- Phone (optional) -->
          <div class="field">
            <label for="phone"> Phone number <span class="optional">(optional)</span> </label>
            <input
              pInputText
              id="phone"
              type="tel"
              formControlName="phone"
              placeholder="+14155552671"
              autocomplete="tel"
              class="w-full"
              [class.ng-invalid]="isInvalid('phone')"
            />
            @if (isInvalid('phone')) {
              <small class="field-error" role="alert">
                Enter a valid phone number (digits, optional leading +).
              </small>
            }
          </div>

          <!-- Password -->
          <div class="field">
            <label for="password">Password</label>
            <p-password
              inputId="password"
              formControlName="password"
              placeholder="Min 12 chars, mixed case, digit, special"
              [feedback]="true"
              [toggleMask]="true"
              styleClass="w-full"
              [inputStyleClass]="isInvalid('password') ? 'ng-invalid w-full' : 'w-full'"
              autocomplete="new-password"
            />
            @if (isInvalid('password')) {
              <small class="field-error" role="alert">
                {{ getError('password') }}
              </small>
            }
          </div>

          <!-- Confirm Password -->
          <div class="field">
            <label for="confirmPassword">Confirm password</label>
            <p-password
              inputId="confirmPassword"
              formControlName="confirmPassword"
              placeholder="Repeat password"
              [feedback]="false"
              [toggleMask]="true"
              styleClass="w-full"
              [inputStyleClass]="
                isInvalid('confirmPassword') || form.hasError('passwordsMismatch')
                  ? 'ng-invalid w-full'
                  : 'w-full'
              "
              autocomplete="new-password"
            />
            @if (form.hasError('passwordsMismatch') && form.get('confirmPassword')?.touched) {
              <small class="field-error" role="alert">Passwords do not match.</small>
            }
          </div>

          <!-- Submit -->
          <p-button
            type="submit"
            label="Create account"
            [loading]="loading"
            [disabled]="form.invalid || loading"
            styleClass="w-full mt-2"
          />
        </form>

        <p class="auth-footer">Already have an account? <a routerLink="/login">Sign in</a></p>
      </div>
    </div>
  `,
  styles: [
    `
      .auth-page {
        min-height: 100vh;
        display: flex;
        align-items: center;
        justify-content: center;
        background: var(--surface-ground);
        padding: 2rem 1rem;
      }
      .auth-card {
        width: 100%;
        max-width: 440px;
        background: var(--surface-card);
        border: 1px solid var(--surface-border);
        border-radius: 12px;
        padding: 2.5rem 2rem;
      }
      .auth-title {
        margin: 0 0 1.5rem;
        font-size: 1.5rem;
        font-weight: 600;
        color: var(--text-color);
      }
      .field {
        display: flex;
        flex-direction: column;
        gap: 0.375rem;
        margin-bottom: 1.25rem;
      }
      label {
        font-size: 0.875rem;
        font-weight: 500;
        color: var(--text-color);
      }
      .optional {
        color: var(--text-color-secondary);
        font-weight: 400;
      }
      .field-error {
        color: var(--red-500, #ef4444);
        font-size: 0.75rem;
      }
      .auth-footer {
        margin-top: 1.5rem;
        text-align: center;
        font-size: 0.875rem;
        color: var(--text-color-secondary);
      }
      .auth-footer a {
        color: var(--primary-color);
        text-decoration: none;
      }
    `,
  ],
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  // Password complexity: 12+ chars, ≥1 uppercase, ≥1 lowercase, ≥1 digit,
  // ≥1 special character — mirrors server-side NFR-014 rule
  private readonly passwordPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{12,}$/;

  // Phone: optional; when provided must be E.164-compatible
  private readonly phonePattern = /^\+?[0-9]{7,15}$/;

  readonly form: FormGroup = this.fb.group(
    {
      email: ['', [Validators.required, Validators.maxLength(256), Validators.email]],
      firstName: ['', [Validators.required, Validators.maxLength(100)]],
      lastName: ['', [Validators.required, Validators.maxLength(100)]],
      phone: ['', [Validators.pattern(this.phonePattern)]],
      password: ['', [Validators.required, Validators.pattern(this.passwordPattern)]],
      confirmPassword: ['', [Validators.required]],
    },
    { validators: passwordsMatchValidator },
  );

  loading = false;
  serverError: string | null = null;

  isInvalid(field: string): boolean {
    const ctrl = this.form.get(field);
    return !!(ctrl?.invalid && ctrl.touched);
  }

  getError(field: string): string {
    const ctrl = this.form.get(field);
    if (!ctrl?.errors) return '';
    if (ctrl.errors['required']) return `${this.fieldLabel(field)} is required.`;
    if (ctrl.errors['email']) return 'Email format is invalid.';
    if (ctrl.errors['maxlength']) return `${this.fieldLabel(field)} is too long.`;
    if (ctrl.errors['pattern']) {
      if (field === 'password')
        return (
          'Password must be at least 12 characters and include an uppercase letter, ' +
          'a lowercase letter, a digit, and a special character.'
        );
      if (field === 'phone') return 'Enter a valid phone number (digits, optional leading +).';
    }
    return 'Invalid value.';
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading = true;
    this.serverError = null;

    const { email, firstName, lastName, phone, password, confirmPassword } =
      this.form.getRawValue() as {
        email: string;
        firstName: string;
        lastName: string;
        phone: string;
        password: string;
        confirmPassword: string;
      };

    this.auth
      .register({ email, firstName, lastName, phone: phone || null, password, confirmPassword })
      .subscribe({
        next: () => {
          this.loading = false;
          this.router.navigate(['/login'], { queryParams: { registered: 'true' } });
        },
        error: (err: HttpErrorResponse) => {
          this.loading = false;
          if (err.status === 409) {
            this.serverError =
              (err.error as { detail?: string })?.detail ??
              'An account with this email already exists.';
          } else {
            this.serverError = 'Registration failed. Please try again later.';
          }
        },
      });
  }

  private fieldLabel(field: string): string {
    const labels: Record<string, string> = {
      email: 'Email',
      firstName: 'First name',
      lastName: 'Last name',
      phone: 'Phone number',
      password: 'Password',
      confirmPassword: 'Confirm password',
    };
    return labels[field] ?? field;
  }
}
