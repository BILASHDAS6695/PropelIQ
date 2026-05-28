import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { firstValueFrom } from 'rxjs';
import { IntakeWindowService } from '../../core/services/intake-window.service';

export const intakeWindowGuard: CanActivateFn = async (route) => {
  const appointmentId = route.queryParamMap.get('appointmentId');
  if (!appointmentId) return true;

  const windowSvc = inject(IntakeWindowService);
  const toast = inject(MessageService);
  const router = inject(Router);

  try {
    const result = await firstValueFrom(windowSvc.check(appointmentId));
    if (!result.isOpen) {
      toast.add({
        severity: 'warn',
        summary: 'Intake Unavailable',
        detail: result.reason ?? 'The intake period for this appointment has ended.',
        life: 5000,
      });
      return router.parseUrl('/intake');
    }
    return true;
  } catch {
    return true;
  }
};
