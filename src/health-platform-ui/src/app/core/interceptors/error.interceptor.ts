import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      switch (error.status) {
        case 401:
          sessionStorage.removeItem('auth_token');
          sessionStorage.removeItem('auth_user');
          router.navigate(['/login']);
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
