import { Component, inject } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { MessageModule } from 'primeng/message';
import { AuthService } from '../../../core/auth/auth.service';
import { ToastService } from '../../../shared/services/toast.service';

function passwordsMatchValidator(control: AbstractControl): ValidationErrors | null {
  const newPwd = control.get('newPassword');
  const confirm = control.get('confirmNewPassword');
  if (!newPwd || !confirm) return null;
  return newPwd.value === confirm.value ? null : { passwordsMismatch: true };
}

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [ReactiveFormsModule, ButtonModule, InputTextModule, PasswordModule, MessageModule],
  template: `
    <div class="auth-page flex align-items-center justify-content-center min-h-screen">
      <div
        class="auth-card surface-card p-4 shadow-2 border-round"
        style="width: 100%; max-width: 460px;"
      >
        <h1 class="text-2xl font-semibold mb-1 text-center">Change Password</h1>
        <p class="text-center text-color-secondary text-sm mb-4">
          Your password has expired or you chose to update it. Please set a new password.
        </p>

        @if (serverError) {
          <p-message severity="error" [text]="serverError" styleClass="w-full mb-4" />
        }

        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <!-- Current Password -->
          <div class="field mb-3">
            <label for="currentPassword" class="block mb-1 font-medium">Current Password</label>
            <p-password
              inputId="currentPassword"
              formControlName="currentPassword"
              [feedback]="false"
              [toggleMask]="true"
              styleClass="w-full"
              inputStyleClass="w-full"
              [class.ng-invalid]="isInvalid('currentPassword')"
              autocomplete="current-password"
            />
            @if (isInvalid('currentPassword')) {
              <small class="p-error">Current password is required.</small>
            }
          </div>

          <!-- New Password -->
          <div class="field mb-3">
            <label for="newPassword" class="block mb-1 font-medium">New Password</label>
            <p-password
              inputId="newPassword"
              formControlName="newPassword"
              [toggleMask]="true"
              styleClass="w-full"
              inputStyleClass="w-full"
              [class.ng-invalid]="isInvalid('newPassword')"
              autocomplete="new-password"
            />
            @if (isInvalid('newPassword')) {
              <small class="p-error">{{ getPasswordError() }}</small>
            }
            <small class="text-color-secondary text-xs">
              Minimum 12 characters · uppercase · lowercase · number · special character
            </small>
          </div>

          <!-- Confirm New Password -->
          <div class="field mb-4">
            <label for="confirmNewPassword" class="block mb-1 font-medium"
              >Confirm New Password</label
            >
            <p-password
              inputId="confirmNewPassword"
              formControlName="confirmNewPassword"
              [feedback]="false"
              [toggleMask]="true"
              styleClass="w-full"
              inputStyleClass="w-full"
              [class.ng-invalid]="
                isInvalid('confirmNewPassword') || form.hasError('passwordsMismatch')
              "
              autocomplete="new-password"
            />
            @if (form.hasError('passwordsMismatch') && form.get('confirmNewPassword')?.touched) {
              <small class="p-error">Passwords do not match.</small>
            }
          </div>

          <p-button
            type="submit"
            label="Change Password"
            styleClass="w-full"
            [loading]="loading"
            [disabled]="form.invalid || loading"
          />
        </form>
      </div>
    </div>
  `,
})
export class ChangePasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  loading = false;
  serverError = '';

  form: FormGroup = this.fb.group(
    {
      currentPassword: ['', Validators.required],
      newPassword: [
        '',
        [
          Validators.required,
          Validators.minLength(12),
          Validators.pattern(/[A-Z]/),
          Validators.pattern(/[a-z]/),
          Validators.pattern(/[0-9]/),
          Validators.pattern(/[^a-zA-Z0-9]/),
        ],
      ],
      confirmNewPassword: ['', Validators.required],
    },
    { validators: passwordsMatchValidator },
  );

  isInvalid(field: string): boolean {
    const c = this.form.get(field);
    return !!(c?.invalid && c.touched);
  }

  getPasswordError(): string {
    const ctrl = this.form.get('newPassword');
    if (!ctrl) return '';
    if (ctrl.hasError('required')) return 'New password is required.';
    if (ctrl.hasError('minlength')) return 'Must be at least 12 characters.';
    if (ctrl.hasError('pattern'))
      return 'Must include uppercase, lowercase, number, and special character.';
    return '';
  }

  submit(): void {
    if (this.form.invalid) return;

    this.loading = true;
    this.serverError = '';

    const { currentPassword, newPassword, confirmNewPassword } = this.form.value;

    this.auth.changePassword(currentPassword, newPassword, confirmNewPassword).subscribe({
      next: () => {
        this.loading = false;
        this.toast.success('Password changed successfully.');
        this.router.navigate(['/dashboard']);
      },
      error: (err: HttpErrorResponse) => {
        this.loading = false;
        this.serverError =
          err?.error?.detail ??
          err?.error?.errors?.NewPassword?.[0] ??
          'Password change failed. Please try again.';
      },
    });
  }
}
