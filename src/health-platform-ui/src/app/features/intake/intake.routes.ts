import { Routes } from '@angular/router';
import { intakeWindowGuard } from './intake-window.guard';

export const INTAKE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./intake-chat/intake-chat.component').then((m) => m.IntakeChatComponent),
  },
  {
    path: 'form',
    canActivate: [intakeWindowGuard],
    loadComponent: () =>
      import('./intake-landing/intake-landing.component').then((m) => m.IntakeLandingComponent),
  },
  {
    path: 'summary/:appointmentId',
    loadComponent: () =>
      import('./intake-summary/intake-summary.component').then((m) => m.IntakeSummaryComponent),
  },
];
