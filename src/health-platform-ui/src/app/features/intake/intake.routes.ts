import { Routes } from '@angular/router';

export const INTAKE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./intake-chat/intake-chat.component').then((m) => m.IntakeChatComponent),
  },
  {
    path: 'form',
    loadComponent: () =>
      import('./intake-landing/intake-landing.component').then((m) => m.IntakeLandingComponent),
  },
];
