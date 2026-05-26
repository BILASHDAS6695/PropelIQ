import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastModule } from 'primeng/toast';
import { LoadingSpinnerComponent } from './shared';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, ToastModule, LoadingSpinnerComponent],
  template: `
    <p-toast position="top-right" />
    <app-loading-spinner />
    <router-outlet />
  `,
})
export class App {}
