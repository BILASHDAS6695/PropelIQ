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
];
