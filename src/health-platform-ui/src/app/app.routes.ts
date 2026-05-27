import { Routes } from '@angular/router';
import { AppLayoutComponent } from './layout/app-layout.component';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  {
    path: '',
    component: AppLayoutComponent,
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        loadChildren: () =>
          import('./features/dashboard/dashboard.routes').then((m) => m.DASHBOARD_ROUTES),
      },
      {
        path: 'booking',
        loadChildren: () =>
          import('./features/booking/booking.routes').then((m) => m.BOOKING_ROUTES),
      },
      {
        path: 'intake',
        loadChildren: () => import('./features/intake/intake.routes').then((m) => m.INTAKE_ROUTES),
      },
      {
        path: 'clinical',
        loadChildren: () =>
          import('./features/clinical/clinical.routes').then((m) => m.CLINICAL_ROUTES),
      },
      {
        path: 'change-password',
        loadComponent: () =>
          import('./features/auth/change-password/change-password.component').then(
            (m) => m.ChangePasswordComponent,
          ),
      },
      {
        path: 'admin',
        canActivate: [roleGuard('admin')],
        loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
      },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    loadComponent: () =>
      import('./features/auth/register/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: 'forgot-password',
    loadComponent: () =>
      import('./features/auth/forgot-password/forgot-password.component').then(
        (m) => m.ForgotPasswordComponent,
      ),
  },
  {
    path: 'forbidden',
    loadComponent: () =>
      import('./features/auth/forbidden/forbidden.component').then((m) => m.ForbiddenComponent),
  },
  { path: '**', redirectTo: 'dashboard' },
];
