import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { catchError, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const authService = inject(AuthService);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      switch (error.status) {
        case 401:
          authService.clearAuthState();

          if (error.error?.detail === 'Session expired') {
            router.navigate(['/login'], { queryParams: { expired: true } });
          } else {
            router.navigate(['/login']);
          }
          break;
        case 403:
          router.navigate(['/dashboard']);
          break;
        case 0:
          console.error('Network error — API may be unreachable');
          break;
      }

      return throwError(() => error);
    }),
  );
};
