# Task 003: Angular Notification Preferences Settings Page

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-035 |
| **Epic** | EP-004 |
| **Layer** | Frontend (Angular) |
| **Priority** | Low |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | Task 002 complete — `GET/PUT /api/users/{id}/notification-preferences` endpoints live |

## Objective

1. **Add `NotificationPreferencesModel`** TypeScript interface and
   `NotificationPreferencesService` Angular service (HTTP + local state).
2. **Create `NotificationPreferencesComponent`** — a standalone settings page
   with per-category toggles for email and in-app channels using PrimeNG
   `ToggleSwitch` components.
3. **Add a lazy route** at `/notification-preferences` under the authenticated
   layout, and a sidebar navigation entry.
4. **Wire the "Preferences" link** in the notification bell dropdown to navigate
   to the new page.

---

## Acceptance Criteria Covered

- AC: Settings page: toggle email notifications per category (reminders, swap, general)
- AC: Settings page: toggle in-app notifications per category
- AC: Changes take effect immediately (no restart needed)
- AC: Default preferences: all channels enabled for all categories

---

## Implementation Steps

### 1. Add `NotificationPreferencesModel`

Add to `src/health-platform-ui/src/app/core/models/notification.model.ts`
(append at the end of the file):

```typescript
export interface NotificationPreferences {
  emailReminders: boolean;
  emailSwap: boolean;
  emailGeneral: boolean;
  inAppReminders: boolean;
  inAppSwap: boolean;
  inAppGeneral: boolean;
}

export const DEFAULT_PREFERENCES: NotificationPreferences = {
  emailReminders: true,
  emailSwap: true,
  emailGeneral: true,
  inAppReminders: true,
  inAppSwap: true,
  inAppGeneral: true,
};
```

---

### 2. Create `NotificationPreferencesService`

Create `src/health-platform-ui/src/app/core/services/notification-preferences.service.ts`:

```typescript
import { inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { NotificationPreferences, DEFAULT_PREFERENCES } from '../models/notification.model';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';

@Injectable({ providedIn: 'root' })
export class NotificationPreferencesService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  private readonly _prefs = signal<NotificationPreferences>({ ...DEFAULT_PREFERENCES });
  readonly prefs = this._prefs.asReadonly();

  private get userId(): string {
    return this.auth.currentUser()?.id ?? '';
  }

  async load(): Promise<void> {
    if (!this.userId) return;
    try {
      const data = await firstValueFrom(
        this.http.get<NotificationPreferences>(
          `${environment.apiUrl}/users/${this.userId}/notification-preferences`,
        ),
      );
      this._prefs.set(data);
    } catch {
      // Non-fatal: keep defaults
    }
  }

  async save(prefs: NotificationPreferences): Promise<void> {
    await firstValueFrom(
      this.http.put(
        `${environment.apiUrl}/users/${this.userId}/notification-preferences`,
        prefs,
      ),
    );
    this._prefs.set({ ...prefs });
  }
}
```

> **Note:** `AuthService` already exposes `currentUser()` signal in the existing
> codebase. If the service exposes the user ID differently, adjust the `userId`
> getter accordingly.

---

### 3. Create `NotificationPreferencesComponent`

Create `src/health-platform-ui/src/app/features/notification-preferences/notification-preferences.component.ts`:

```typescript
import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CardModule } from 'primeng/card';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ButtonModule } from 'primeng/button';
import { DividerModule } from 'primeng/divider';
import { MessageService } from 'primeng/api';
import { NotificationPreferencesService } from '../../core/services/notification-preferences.service';
import { NotificationPreferences } from '../../core/models/notification.model';

@Component({
  selector: 'app-notification-preferences',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, CardModule, ToggleSwitchModule, ButtonModule, DividerModule],
  template: `
    <div class="preferences-page">
      <p-card header="Notification Preferences">
        <p class="subtitle">
          Choose which notifications you receive and through which channels. Security
          notifications (account lockout, password expiry) are always delivered.
        </p>

        <p-divider />

        <!-- Email channel -->
        <section aria-labelledby="email-heading">
          <h3 id="email-heading" class="channel-heading">
            <i class="pi pi-envelope"></i> Email
          </h3>
          <div class="pref-row">
            <label for="emailReminders">Appointment reminders</label>
            <p-toggleSwitch
              inputId="emailReminders"
              [(ngModel)]="draft().emailReminders"
            />
          </div>
          <div class="pref-row">
            <label for="emailSwap">Slot swap notifications</label>
            <p-toggleSwitch
              inputId="emailSwap"
              [(ngModel)]="draft().emailSwap"
            />
          </div>
          <div class="pref-row">
            <label for="emailGeneral">General notifications</label>
            <p-toggleSwitch
              inputId="emailGeneral"
              [(ngModel)]="draft().emailGeneral"
            />
          </div>
        </section>

        <p-divider />

        <!-- In-app channel -->
        <section aria-labelledby="inapp-heading">
          <h3 id="inapp-heading" class="channel-heading">
            <i class="pi pi-bell"></i> In-App
          </h3>
          <div class="pref-row">
            <label for="inAppReminders">Appointment reminders</label>
            <p-toggleSwitch
              inputId="inAppReminders"
              [(ngModel)]="draft().inAppReminders"
            />
          </div>
          <div class="pref-row">
            <label for="inAppSwap">Slot swap notifications</label>
            <p-toggleSwitch
              inputId="inAppSwap"
              [(ngModel)]="draft().inAppSwap"
            />
          </div>
          <div class="pref-row">
            <label for="inAppGeneral">General notifications</label>
            <p-toggleSwitch
              inputId="inAppGeneral"
              [(ngModel)]="draft().inAppGeneral"
            />
          </div>
        </section>

        <p-divider />

        <div class="actions">
          <p-button
            label="Save preferences"
            icon="pi pi-check"
            [loading]="saving()"
            (onClick)="save()"
          />
        </div>
      </p-card>
    </div>
  `,
  styles: [
    `
      .preferences-page {
        max-width: 560px;
        margin: 2rem auto;
        padding: 0 1rem;
      }
      .subtitle {
        color: var(--text-color-secondary);
        margin-bottom: 0.5rem;
        font-size: 0.875rem;
      }
      .channel-heading {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        font-size: 1rem;
        font-weight: 600;
        margin-bottom: 1rem;
        color: var(--text-color);
      }
      .pref-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 0.5rem 0;
        font-size: 0.875rem;
      }
      .actions {
        display: flex;
        justify-content: flex-end;
      }
    `,
  ],
})
export class NotificationPreferencesComponent implements OnInit {
  private readonly svc   = inject(NotificationPreferencesService);
  private readonly toast = inject(MessageService);

  readonly draft  = signal<NotificationPreferences>({ ...this.svc.prefs() });
  readonly saving = signal(false);

  async ngOnInit(): Promise<void> {
    await this.svc.load();
    this.draft.set({ ...this.svc.prefs() });
  }

  async save(): Promise<void> {
    this.saving.set(true);
    try {
      await this.svc.save({ ...this.draft() });
      this.toast.add({
        severity: 'success',
        summary: 'Saved',
        detail: 'Notification preferences updated.',
        life: 3_000,
      });
    } catch {
      this.toast.add({
        severity: 'error',
        summary: 'Error',
        detail: 'Failed to save preferences. Please try again.',
        life: 5_000,
      });
    } finally {
      this.saving.set(false);
    }
  }
}
```

---

### 4. Add lazy route

File: `src/health-platform-ui/src/app/app.routes.ts`

Inside the `children` array of the authenticated layout route, add after the
`change-password` route entry:

```typescript
{
  path: 'notification-preferences',
  loadComponent: () =>
    import(
      './features/notification-preferences/notification-preferences.component'
    ).then((m) => m.NotificationPreferencesComponent),
},
```

---

### 5. Add sidebar navigation entry

File: `src/health-platform-ui/src/app/layout/sidebar/app-sidebar.component.ts`

Add to the `navItems` array (after `change-password` conceptually — visible to
all authenticated users):

```typescript
{ label: 'Notifications', icon: 'pi-sliders-h', route: '/notification-preferences' },
```

---

### 6. Wire "Preferences" link in notification bell dropdown

File: `src/health-platform-ui/src/app/shared/components/notification-bell/notification-bell.component.ts`

In the popover footer (below the "Mark all read" button), add a router link:

```html
<a
  routerLink="/notification-preferences"
  class="pref-link"
  (click)="panel.hide()"
  aria-label="Manage notification preferences"
>
  <i class="pi pi-sliders-h"></i> Preferences
</a>
```

Ensure `RouterModule` is already in the `imports` array of the component (it
was added in US-034 Task 003 — no change needed).

---

## Verification

```bash
cd src/health-platform-ui

# Production build — must succeed with no errors
npx ng build --configuration production 2>&1 | Select-String "error TS|NG[0-9]+|ERROR|Application bundle"

# Lint — must pass clean
npx ng lint
# Expect: All files pass linting.
```

Manual verification (with API running):

1. Navigate to `/notification-preferences` — toggles render, all enabled by default.
2. Toggle "Appointment reminders" email OFF → click **Save preferences** →
   `PUT /api/users/{id}/notification-preferences` returns `204`.
3. Refresh the page — the toggle remains OFF (loaded from API).
4. Navigate away and back — preference survives full page cycle.
5. Trigger an appointment reminder job with the email pref disabled →
   confirm no email is sent (check server logs) but in-app notification is
   still delivered.

---

## Notes

- `ToggleSwitchModule` is imported from `primeng/toggleswitch` (PrimeNG v21).
  The `p-toggleSwitch` selector requires `[(ngModel)]` from `FormsModule` or
  `[checked]` / `(onChange)` for reactive forms — `FormsModule` is used here
  for simplicity given the small form size.
- The `draft` signal is a shallow copy of the current preferences so the user
  can cancel navigation without the backend being updated. The save is explicit
  via the button.
- `NotificationPreferencesService.load()` is called in `ngOnInit` — if the
  service was already loaded from a previous navigation the signal will update
  immediately (no extra HTTP call because the service is `providedIn: 'root'`
  and state persists for the session).
- The `AuthService` reference in `NotificationPreferencesService` uses
  `currentUser()` to obtain the user ID. If the existing `AuthService` exposes
  the current user differently (e.g., `user$` observable), adjust the getter.
- `p-toast` is already mounted globally in `app.html` — `MessageService` is
  already provided in `app.config.ts`. No layout changes required.
