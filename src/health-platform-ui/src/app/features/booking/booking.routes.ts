import { Routes } from '@angular/router';

export const BOOKING_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./book-appointment/book-appointment.component').then(
        (m) => m.BookAppointmentComponent,
      ),
  },
  {
    path: 'appointments',
    loadComponent: () =>
      import('./my-appointments/my-appointments.component').then((m) => m.MyAppointmentsComponent),
  },
  {
    path: 'calendar',
    loadComponent: () =>
      import('../calendar/calendar-view.component').then((m) => m.CalendarViewComponent),
  },
  {
    path: 'staff-schedule',
    loadComponent: () =>
      import('../multi-provider/multi-provider-day.component').then(
        (m) => m.MultiProviderDayComponent,
      ),
  },
];
