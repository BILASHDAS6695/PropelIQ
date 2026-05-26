import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="auth-page">
      <h1>Sign In</h1>
      <p>Login form coming soon.</p>
      <a routerLink="/register">Create account</a>
    </div>
  `,
})
export class LoginComponent {}
