import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [RouterLink, ButtonModule],
  template: `
    <div class="auth-page flex align-items-center justify-content-center min-h-screen">
      <div
        class="auth-card surface-card p-4 shadow-2 border-round"
        style="width: 100%; max-width: 420px; text-align: center;"
      >
        <h1 class="text-2xl font-semibold mb-3">Forgot Password</h1>
        <p class="mb-4 text-color-secondary">
          Password reset via email is coming soon.<br />
          Please contact your administrator to reset your password.
        </p>
        <p-button label="Back to Sign In" routerLink="/login" styleClass="w-full" />
      </div>
    </div>
  `,
})
export class ForgotPasswordComponent {}
