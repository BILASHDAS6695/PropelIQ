import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

@Component({
  selector: 'app-forbidden',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="forbidden-container" role="main" aria-labelledby="forbidden-title">
      <h1 id="forbidden-title">Access Denied</h1>
      <p>You do not have permission to view this page.</p>
      <a routerLink="/dashboard" aria-label="Return to dashboard">Return to Dashboard</a>
    </div>
  `,
  styles: [`
    .forbidden-container {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      min-height: 60vh;
      gap: 1rem;
      text-align: center;
    }
  `]
})
export class ForbiddenComponent {}
