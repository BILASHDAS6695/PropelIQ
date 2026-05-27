import { inject, Injectable, OnDestroy, signal } from '@angular/core';
import { fromEvent, merge, Subscription } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import { AuthService } from '../auth/auth.service';

const WARN_MS = 13 * 60 * 1000; // 780 000 ms
const LOGOUT_MS = 15 * 60 * 1000; // 900 000 ms

const USER_EVENTS = ['mousemove', 'keydown', 'click', 'scroll', 'touchstart'] as const;

@Injectable({ providedIn: 'root' })
export class InactivityTimerService implements OnDestroy {
  private readonly auth = inject(AuthService);

  readonly showWarning = signal(false);

  private warnSub?: Subscription;
  private logoutSub?: Subscription;
  private activitySub?: Subscription;

  /** Call once after the user authenticates (e.g. from AppLayoutComponent). */
  start(): void {
    this.stop(); // clear any previous subscriptions

    const activity$ = merge(...USER_EVENTS.map((ev) => fromEvent(document, ev)));

    // Subscribe to activity to reset the warning dialog.
    this.activitySub = activity$.subscribe(() => {
      if (this.showWarning()) this.showWarning.set(false);
    });

    this.warnSub = activity$.pipe(debounceTime(WARN_MS)).subscribe(() => {
      this.showWarning.set(true);
    });

    this.logoutSub = activity$.pipe(debounceTime(LOGOUT_MS)).subscribe(() => {
      this.showWarning.set(false);
      this.auth.logout();
    });

    // Fire a synthetic event so the debounce timer starts from now.
    document.dispatchEvent(new Event('mousemove'));
  }

  /** Call when the user logs out or the layout component is destroyed. */
  stop(): void {
    this.warnSub?.unsubscribe();
    this.logoutSub?.unsubscribe();
    this.activitySub?.unsubscribe();
    this.showWarning.set(false);
  }

  ngOnDestroy(): void {
    this.stop();
  }
}
