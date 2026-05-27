# Task 002: Inactivity Timer — Warn at 13 Minutes, Auto-Logout at 15 Minutes

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-018 |
| **Epic** | EP-001 |
| **Layer** | Angular — Core Service + Layout Component |
| **Priority** | Critical |
| **Estimated Effort** | 2 hours |
| **Dependencies** | Task 001 — `AuthService.logout()` must work correctly |

## Objective

Implement US-018 AC-8: show a warning dialog to authenticated users at 13 minutes of inactivity, then auto-logout at 15 minutes. Any user interaction (mouse move, key press, click, scroll, touch) resets the timer.

---

## Architecture

```
AppLayoutComponent
  └── InactivityWarningComponent (PrimeNG Dialog, conditionally mounted)
         │
         └── InactivityTimerService
               ├── fromEvent(document, 'mousemove' | 'keydown' | 'click' | 'scroll' | 'touchstart')
               ├── debounceTime(780_000)  → warn signal
               └── debounceTime(900_000)  → auto-logout
```

The service is **only active while the user is authenticated**. It starts/stops based on `AuthService.isAuthenticated`.

---

## Implementation Steps

### 1. Create `src/app/core/services/inactivity-timer.service.ts`

```typescript
import { inject, Injectable, OnDestroy, signal } from '@angular/core';
import { fromEvent, merge, Subscription } from 'rxjs';
import { debounceTime } from 'rxjs/operators';
import { AuthService } from '../auth/auth.service';

const WARN_MS   = 13 * 60 * 1000; // 780 000 ms
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

    const activity$ = merge(
      ...USER_EVENTS.map((ev) => fromEvent(document, ev)),
    );

    // Subscribe to activity to reset both debounced timers.
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

    // Also fire an initial synthetic event so the debounce timer starts from now.
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
```

### 2. Create `src/app/shared/components/inactivity-warning/inactivity-warning.component.ts`

```typescript
import { Component, inject, output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InactivityTimerService } from '../../../core/services/inactivity-timer.service';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-inactivity-warning',
  standalone: true,
  imports: [DialogModule, ButtonModule],
  template: `
    <p-dialog
      header="Session Expiring Soon"
      [visible]="timer.showWarning()"
      [modal]="true"
      [closable]="false"
      [style]="{ width: '420px' }"
    >
      <p class="mb-4">
        Your session will expire in 2 minutes due to inactivity.<br />
        Click <strong>Stay Signed In</strong> to continue.
      </p>
      <ng-template pTemplate="footer">
        <p-button
          label="Sign Out Now"
          severity="secondary"
          (onClick)="signOut()"
        />
        <p-button
          label="Stay Signed In"
          (onClick)="staySignedIn()"
          class="ml-2"
        />
      </ng-template>
    </p-dialog>
  `,
})
export class InactivityWarningComponent {
  protected readonly timer = inject(InactivityTimerService);
  private readonly auth = inject(AuthService);

  staySignedIn(): void {
    // Any interaction already resets the debounce; just dismiss the dialog.
    this.timer.showWarning.set(false);
    document.dispatchEvent(new Event('mousemove'));
  }

  signOut(): void {
    this.timer.stop();
    this.auth.logout();
  }
}
```

### 3. Update `src/app/layout/app-layout.component.ts` — Mount timer + warning

Add import and start/stop lifecycle:

```typescript
import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AppHeaderComponent } from './header/app-header.component';
import { AppSidebarComponent } from './sidebar/app-sidebar.component';
import { InactivityTimerService } from '../core/services/inactivity-timer.service';
import { InactivityWarningComponent } from '../shared/components/inactivity-warning/inactivity-warning.component';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, AppHeaderComponent, AppSidebarComponent, InactivityWarningComponent],
  template: `
    <div class="app-layout" [class.sidebar-collapsed]="sidebarCollapsed()">
      <app-sidebar
        [collapsed]="sidebarCollapsed()"
        (toggleCollapse)="sidebarCollapsed.set($event)"
      />
      <div class="app-main">
        <app-header (menuToggle)="sidebarCollapsed.update((v) => !v)" />
        <main class="app-content">
          <router-outlet />
        </main>
      </div>
    </div>
    <app-inactivity-warning />
  `,
  // ... existing styles unchanged
})
export class AppLayoutComponent implements OnInit, OnDestroy {
  protected readonly sidebarCollapsed = signal(false);
  private readonly inactivity = inject(InactivityTimerService);

  ngOnInit(): void {
    this.inactivity.start();
  }

  ngOnDestroy(): void {
    this.inactivity.stop();
  }
}
```

### 4. Export from `src/app/shared/index.ts`

Add:
```typescript
export { InactivityWarningComponent } from './components/inactivity-warning/inactivity-warning.component';
```

---

## Affected Files

| File | Change |
|------|--------|
| `src/app/core/services/inactivity-timer.service.ts` | **Created** — debounce-based inactivity service |
| `src/app/shared/components/inactivity-warning/inactivity-warning.component.ts` | **Created** — PrimeNG Dialog warning |
| `src/app/layout/app-layout.component.ts` | `OnInit`/`OnDestroy` + mount `<app-inactivity-warning />` |
| `src/app/shared/index.ts` | +export for warning component |

---

## Behaviour Spec

| Time since last interaction | Behaviour |
|---------------------------|-----------|
| 0 – 12:59 | Normal — no visible indicator |
| 13:00 | Warning dialog appears: "Session expiring in 2 minutes" |
| 13:01 – 14:59 | Dialog stays visible; any interaction dismisses it and resets timers |
| 15:00 | Auto-logout: `auth.logout()` called → redirect to `/login` |

---

## Acceptance Criteria

- [ ] `InactivityTimerService.start()` called in `AppLayoutComponent.ngOnInit()`
- [ ] `InactivityTimerService.stop()` called in `AppLayoutComponent.ngOnDestroy()`
- [ ] Warning dialog appears after exactly 13 minutes of no user interaction
- [ ] Clicking "Stay Signed In" dismisses dialog and resets timers
- [ ] Clicking "Sign Out Now" calls `auth.logout()` immediately
- [ ] Auto-logout fires at 15 minutes if no interaction after warning
- [ ] Timer only active inside authenticated layout (not on `/login` or `/register`)
- [ ] `npm run build` passes (0 errors)

## Verification

To test locally (temporarily lower `WARN_MS` / `LOGOUT_MS` to 5s/10s):
1. Log in → wait → warning dialog appears at 5s
2. Click "Stay Signed In" → dialog closes, timer resets
3. Wait 10s with no interaction → auto-logged out, redirected to `/login`
