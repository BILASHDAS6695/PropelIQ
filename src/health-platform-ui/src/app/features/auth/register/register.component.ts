import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="auth-page">
      <h1>Create Account</h1>
      <p>Registration form coming soon.</p>
      <a routerLink="/login">Already have an account?</a>
    </div>
  `,
})
export class RegisterComponent {}
