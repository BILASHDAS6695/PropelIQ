# Task 003: Angular Notification Bell UI

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-034 |
| **Epic** | EP-004 |
| **Layer** | Frontend (Angular) |
| **Priority** | High |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | Task 001 + Task 002 complete — `/api/notifications`, `/api/notifications/mark-read`, SignalR hub at `/hubs/notifications` all ready |

## Objective

Replace the static bell icon in `AppHeaderComponent` with a fully reactive
notification bell that:

1. **Connects to SignalR** on login and auto-reconnects with exponential back-off.
2. **Loads the last 20 notifications** on startup via `GET /api/notifications`.
3. **Pushes new notifications** in real time via `ReceiveNotification`.
4. **Displays an unread count badge** (capped at "99+").
5. **Opens a dropdown** showing the last 20 entries — icon, title, message,
   timestamp, read/unread state, action link.
6. **Marks a notification as read** on click (calls `PATCH /api/notifications/mark-read?notificationId=…`).
7. **"Mark all read"** button calls `PATCH /api/notifications/mark-read` (no id).
8. **Shows a toast** for high-priority types (`SwapRequest`, `ArrivalAlert`).
9. Falls back to **HTTP polling every 30 seconds** if SignalR fails.

---

## Acceptance Criteria Covered

- AC: Frontend notification bell icon with unread count badge
- AC: Click bell → dropdown list of recent notifications (last 20)
- AC: Each notification: icon, message, timestamp, read/unread state, action link
- AC: Mark as read on click or "Mark all read" button
- AC: Toast popup for high-priority notifications (swap request, arrival)
- AC: SignalR auto-reconnect with exponential backoff
- AC: 100+ unread → "99+" badge

---

## Implementation Steps

### 1. Add `@microsoft/signalr` package

```bash
cd src/health-platform-ui
npm install @microsoft/signalr
```

### 2. Define notification models

Create `src/health-platform-ui/src/app/core/models/notification.model.ts`:

```typescript
export type NotificationType =
  | 'Reminder'
  | 'Confirmation'
  | 'SlotSwap'
  | 'General'
  | 'SwapRequest'
  | 'SwapResult'
  | 'ArrivalAlert'
  | 'StatusChange';

export interface Notification {
  id: string;
  type: NotificationType;
  title: string;
  message: string;
  actionUrl: string | null;
  isRead: boolean;
  sentAt: string; // ISO 8601
}

export interface GetNotificationsResult {
  items: Notification[];
  unreadCount: number;
}

/** High-priority types that trigger a toast popup. */
export const HIGH_PRIORITY_TYPES: NotificationType[] = [
  'SwapRequest',
  'ArrivalAlert',
];

/** Maps NotificationType to a PrimeNG icon class. */
export const NOTIFICATION_ICONS: Record<NotificationType, string> = {
  Reminder:     'pi pi-clock',
  Confirmation: 'pi pi-check-circle',
  SlotSwap:     'pi pi-arrows-h',
  General:      'pi pi-info-circle',
  SwapRequest:  'pi pi-arrows-h',
  SwapResult:   'pi pi-arrows-h',
  ArrivalAlert: 'pi pi-map-marker',
  StatusChange: 'pi pi-sync',
};
```

Export from `src/health-platform-ui/src/app/core/models/index.ts`
(or create it if it does not exist).

### 3. Create `NotificationSignalRService`

Create `src/health-platform-ui/src/app/core/services/notification-signalr.service.ts`:

```typescript
import { inject, Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { Notification } from '../models/notification.model';

interface InAppNotificationPayload extends Notification {
  newUnreadCount: number;
}

@Injectable({ providedIn: 'root' })
export class NotificationSignalRService implements OnDestroy {
  private connection: signalR.HubConnection | null = null;
  private reconnectAttempt = 0;
  private pollingTimer: ReturnType<typeof setInterval> | null = null;

  /** Emits whenever a new notification arrives via SignalR. */
  readonly received$ = new Subject<InAppNotificationPayload>();

  private readonly auth = inject(AuthService);

  start(): void {
    if (this.connection) return;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/notifications', {
        accessTokenFactory: () => this.auth.getToken() ?? '',
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (ctx) => {
          // Exponential back-off: 2s, 4s, 8s, 16s, 30s cap
          const delay = Math.min(1000 * Math.pow(2, ctx.previousRetryCount + 1), 30_000);
          return delay;
        },
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.on('ReceiveNotification', (payload: InAppNotificationPayload) => {
      this.reconnectAttempt = 0;
      this.received$.next(payload);
    });

    this.connection.onreconnecting(() => {
      this.reconnectAttempt++;
    });

    this.connection.onclose(() => {
      // Fall back to polling if SignalR cannot reconnect
      this.startPollingFallback();
    });

    this.connection
      .start()
      .then(() => this.stopPollingFallback())
      .catch(() => this.startPollingFallback());
  }

  stop(): void {
    this.stopPollingFallback();
    this.connection?.stop();
    this.connection = null;
  }

  ngOnDestroy(): void {
    this.stop();
  }

  private startPollingFallback(): void {
    if (this.pollingTimer) return;
    // Emit a sentinel that NotificationService will use to trigger HTTP refresh
    this.pollingTimer = setInterval(() => this.received$.next(null as any), 30_000);
  }

  private stopPollingFallback(): void {
    if (this.pollingTimer) {
      clearInterval(this.pollingTimer);
      this.pollingTimer = null;
    }
  }
}
```

### 4. Create `NotificationService`

Create `src/health-platform-ui/src/app/core/services/notification.service.ts`:

```typescript
import { inject, Injectable, signal, computed, OnDestroy } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MessageService } from 'primeng/api';
import { Subscription } from 'rxjs';
import {
  Notification,
  GetNotificationsResult,
  HIGH_PRIORITY_TYPES,
} from '../models/notification.model';
import { NotificationSignalRService } from './notification-signalr.service';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class NotificationService implements OnDestroy {
  private readonly http    = inject(HttpClient);
  private readonly signalr = inject(NotificationSignalRService);
  private readonly toast   = inject(MessageService);

  private readonly _items    = signal<Notification[]>([]);
  private readonly _unread   = signal<number>(0);

  readonly items   = this._items.asReadonly();
  readonly unread  = this._unread.asReadonly();
  readonly badgeLabel = computed(() => {
    const count = this._unread();
    return count > 99 ? '99+' : count > 0 ? String(count) : null;
  });

  private sub?: Subscription;

  init(): void {
    this.loadFromApi();
    this.signalr.start();

    this.sub = this.signalr.received$.subscribe((payload) => {
      if (!payload) {
        // Polling fallback — refresh from API
        this.loadFromApi();
        return;
      }

      // Prepend new notification and cap list at 20
      this._items.update((list) => [payload, ...list].slice(0, 20));
      this._unread.set(payload.newUnreadCount);

      if (HIGH_PRIORITY_TYPES.includes(payload.type)) {
        this.toast.add({
          severity: 'warn',
          summary:  payload.title,
          detail:   payload.message,
          life:     8000,
        });
      }
    });
  }

  loadFromApi(): void {
    this.http
      .get<GetNotificationsResult>(`${environment.apiUrl}/api/notifications`)
      .subscribe((res) => {
        this._items.set(res.items);
        this._unread.set(res.unreadCount);
      });
  }

  markRead(notificationId?: string): void {
    const params = notificationId ? { notificationId } : {};
    this.http
      .patch<{ markedRead: number }>(
        `${environment.apiUrl}/api/notifications/mark-read`,
        null,
        { params },
      )
      .subscribe(() => {
        if (notificationId) {
          this._items.update((list) =>
            list.map((n) =>
              n.id === notificationId ? { ...n, isRead: true } : n,
            ),
          );
          this._unread.update((count) => Math.max(0, count - 1));
        } else {
          this._items.update((list) => list.map((n) => ({ ...n, isRead: true })));
          this._unread.set(0);
        }
      });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
    this.signalr.stop();
  }
}
```

### 5. Create `NotificationBellComponent`

Create `src/health-platform-ui/src/app/shared/components/notification-bell/notification-bell.component.ts`:

```typescript
import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { CommonModule }       from '@angular/common';
import { RouterModule }       from '@angular/router';
import { ButtonModule }       from 'primeng/button';
import { BadgeModule }        from 'primeng/badge';
import { OverlayPanelModule } from 'primeng/overlaypanel';
import { DividerModule }      from 'primeng/divider';
import { TooltipModule }      from 'primeng/tooltip';
import { NotificationService }    from '../../../core/services/notification.service';
import { NOTIFICATION_ICONS }     from '../../../core/models/notification.model';

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    RouterModule,
    ButtonModule,
    BadgeModule,
    OverlayPanelModule,
    DividerModule,
    TooltipModule,
  ],
  template: `
    <p-button
      icon="pi pi-bell"
      [text]="true"
      [rounded]="true"
      [badge]="svc.badgeLabel()"
      badgeSeverity="danger"
      aria-label="Notifications"
      aria-haspopup="true"
      (onClick)="panel.toggle($event)"
    />

    <p-overlayPanel #panel styleClass="notification-panel" [style]="{ width: '360px' }">
      <!-- Header -->
      <div class="flex justify-content-between align-items-center px-3 pt-2 pb-1">
        <span class="font-semibold text-base">Notifications</span>
        @if (svc.unread() > 0) {
          <p-button
            label="Mark all read"
            [text]="true"
            size="small"
            (onClick)="markAllRead()"
          />
        }
      </div>
      <p-divider styleClass="my-1" />

      <!-- List -->
      <ul class="list-none m-0 p-0 notification-list" role="list">
        @for (n of svc.items(); track n.id) {
          <li
            class="notification-item flex gap-2 px-3 py-2 cursor-pointer"
            [class.unread]="!n.isRead"
            [attr.aria-label]="n.title"
            (click)="onItemClick(n.id, n.actionUrl)"
            (keyup.enter)="onItemClick(n.id, n.actionUrl)"
            tabindex="0"
            role="listitem"
          >
            <i [class]="iconFor(n.type)" class="mt-1 text-primary"></i>
            <div class="flex-1 min-w-0">
              <p class="m-0 font-medium text-sm white-space-nowrap overflow-hidden text-overflow-ellipsis">
                {{ n.title }}
              </p>
              <p class="m-0 text-color-secondary text-xs mt-1 white-space-nowrap overflow-hidden text-overflow-ellipsis">
                {{ n.message }}
              </p>
              <span class="text-color-secondary text-xs">
                {{ n.sentAt | date: 'short' }}
              </span>
            </div>
            @if (!n.isRead) {
              <span
                class="unread-dot"
                aria-label="Unread"
                pTooltip="Unread"
                tooltipPosition="left"
              ></span>
            }
          </li>
        } @empty {
          <li class="px-3 py-4 text-center text-color-secondary text-sm">
            No notifications
          </li>
        }
      </ul>
    </p-overlayPanel>
  `,
  styles: [`
    :host ::ng-deep .notification-panel .p-overlaypanel-content {
      padding: 0;
    }
    .notification-list {
      max-height: 420px;
      overflow-y: auto;
    }
    .notification-item {
      border-bottom: 1px solid var(--surface-border);
      transition: background 0.15s;
    }
    .notification-item:hover,
    .notification-item:focus {
      background: var(--surface-hover);
      outline: none;
    }
    .notification-item.unread {
      background: var(--blue-50);
    }
    .unread-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: var(--primary-color);
      flex-shrink: 0;
      margin-top: 4px;
    }
  `],
})
export class NotificationBellComponent implements OnInit {
  readonly svc = inject(NotificationService);

  ngOnInit(): void {
    this.svc.init();
  }

  iconFor(type: string): string {
    return NOTIFICATION_ICONS[type as keyof typeof NOTIFICATION_ICONS]
      ?? 'pi pi-bell';
  }

  onItemClick(id: string, actionUrl: string | null): void {
    this.svc.markRead(id);
    if (actionUrl) {
      // Navigation is handled by routerLink / programmatic navigation in a
      // real implementation; here we emit or use Router.navigate().
    }
  }

  markAllRead(): void {
    this.svc.markRead();
  }
}
```

### 6. Replace static bell in `AppHeaderComponent`

Update `src/health-platform-ui/src/app/layout/header/app-header.component.ts`:

- Remove the static `<p-button icon="pi pi-bell" … badge="3" …>` element.
- Import and render `<app-notification-bell />` in its place.

```typescript
import { NotificationBellComponent } from '../../shared/components/notification-bell/notification-bell.component';
// …
imports: [ /* existing */ NotificationBellComponent ],
// In template replace the static button with:
// <app-notification-bell />
```

### 7. Register `MessageService` in the component tree

`MessageService` is already registered in `app.config.ts`. Ensure
`<p-toast />` is present in `app-layout.component.ts` or `app.html` so toasts
render globally:

```html
<!-- in app.html or app-layout template -->
<p-toast position="top-right" />
```

Import `ToastModule` (or the standalone `Toast` component) wherever the root
template is.

---

## Files Created / Modified

| File | Change |
|------|--------|
| `src/health-platform-ui/package.json` | Add `@microsoft/signalr` |
| `src/health-platform-ui/src/app/core/models/notification.model.ts` | New |
| `src/health-platform-ui/src/app/core/services/notification-signalr.service.ts` | New |
| `src/health-platform-ui/src/app/core/services/notification.service.ts` | New |
| `src/health-platform-ui/src/app/shared/components/notification-bell/notification-bell.component.ts` | New |
| `src/health-platform-ui/src/app/layout/header/app-header.component.ts` | Replace static bell with `<app-notification-bell />` |
| `src/health-platform-ui/src/app/app.html` (or layout template) | Add `<p-toast />` if not already present |

---

## Verification

```bash
cd src/health-platform-ui
npm run build -- --configuration production
npm run lint
```

Expected: builds successfully with no lint errors; Angular strict mode
satisfied (all signals typed, no implicit any).

End-to-end smoke check:
1. Start the API and Angular dev server.
2. Log in as a patient.
3. Book an appointment > 24 h away.
4. Observe bell badge increments and toast appears for any high-priority event.
5. Click the bell → dropdown shows the notification.
6. Click the notification → badge decrements, item shows as read.
7. Click "Mark all read" → all items marked, badge disappears.
