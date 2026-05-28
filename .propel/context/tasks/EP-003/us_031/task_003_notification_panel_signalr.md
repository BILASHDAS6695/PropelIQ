# Task 003: Notification Panel Swap Actions + SignalR Badge

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-031 |
| **Epic** | EP-003 |
| **Layer** | Angular / Shared (notification-bell) + Core (NotificationService) |
| **Priority** | Medium |
| **Estimated Effort** | 40 minutes |
| **Dependencies** | Task 001 (`SwapService.respondToSwapRequest`), US-029 Task 003 (respond endpoint) |

## Objective

Extend the existing `NotificationBellComponent` so that incoming `SwapRequest`
notifications render inline **Accept** and **Decline** buttons. The real-time
notification badge (SignalR) already increments for all notification types —
this task confirms the wiring is correct for `SwapRequest` and adds an
on-page-focus polling fallback as required by the edge case AC.

## Acceptance Criteria Covered

- AC: Incoming swap requests shown in notification panel with Accept/Decline buttons
- AC: Real-time notification badge when new swap request received (SignalR)
- AC: SignalR disconnected → fall back to poll on page focus

---

## Implementation Steps

### 1. Parse Swap IDs from `actionUrl`

The `Notification.actionUrl` for `SwapRequest` notifications is expected to be
of the form `/appointments/{appointmentId}/swap-requests/{swapRequestId}`.

Add a pure helper function in `notification-bell.component.ts` (outside the class):

```typescript
const SWAP_URL_RE =
  /\/appointments\/([0-9a-f-]+)\/swap-requests\/([0-9a-f-]+)$/i;

function parseSwapIds(
  actionUrl: string | null,
): { appointmentId: string; swapRequestId: string } | null {
  if (!actionUrl) return null;
  const m = SWAP_URL_RE.exec(actionUrl);
  if (!m) return null;
  return { appointmentId: m[1], swapRequestId: m[2] };
}
```

> **Prerequisite**: Confirm with the backend team that `NotificationService`
> (server-side) sets `ActionUrl` to the pattern above when creating
> `SwapRequest` notifications (see `HealthPlatform.Infrastructure/Notifications`).

---

### 2. Inject `SwapService` + `MessageService` into `NotificationBellComponent`

Edit `src/health-platform-ui/src/app/shared/components/notification-bell/notification-bell.component.ts`.

#### 2a. Add imports at the top of the file

```typescript
import { signal } from '@angular/core';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { SwapService } from '../../../core/services/swap.service';
```

Add `ToastModule` to the component's `imports` array.

#### 2b. Inject services in the class body

```typescript
private readonly swapSvc  = inject(SwapService);
private readonly toast    = inject(MessageService);

/** Tracks which notification IDs have an in-flight respond request. */
readonly respondingId = signal<string | null>(null);
```

---

### 3. Add `respondToSwap()` Method

Insert the following method in `NotificationBellComponent`:

```typescript
respondToSwap(
  event: Event,
  notificationId: string,
  actionUrl: string | null,
  accept: boolean,
  panel: Popover,
): void {
  // Stop the parent click handler from navigating away
  event.stopPropagation();

  const ids = parseSwapIds(actionUrl);
  if (!ids) {
    this.toast.add({
      severity: 'warn',
      summary: 'Action unavailable',
      detail: 'Unable to identify the swap request. Please refresh.',
      life: 5_000,
    });
    return;
  }

  this.respondingId.set(notificationId);

  this.swapSvc
    .respondToSwapRequest(ids.appointmentId, ids.swapRequestId, accept)
    .subscribe({
      next: () => {
        this.respondingId.set(null);
        this.svc.markRead(notificationId);
        this.svc.loadFromApi(); // refresh list to remove acted-on item
        panel.hide();
        this.toast.add({
          severity: 'success',
          summary: accept ? 'Swap accepted' : 'Swap declined',
          detail: accept
            ? 'Your appointment time has been updated.'
            : 'The requester has been notified.',
          life: 5_000,
        });
      },
      error: (err) => {
        this.respondingId.set(null);
        const detail =
          err?.status === 409
            ? 'This swap request has already expired or been actioned.'
            : 'Something went wrong. Please try again.';
        this.toast.add({ severity: 'error', summary: 'Action failed', detail, life: 6_000 });
      },
    });
}
```

---

### 4. Update Notification List Template

Replace the existing `<li>` block inside `NotificationBellComponent`'s template
with the version below. The only change is the addition of inline Accept/Decline
buttons for `SwapRequest` type notifications.

```html
@for (n of svc.items(); track n.id) {
  <li
    [ngClass]="{ unread: !n.isRead }"
    class="notification-item flex gap-2 px-3 py-2 cursor-pointer"
    [attr.aria-label]="n.title"
    tabindex="0"
    role="listitem"
    (click)="onItemClick(n.id, n.actionUrl, panel)"
    (keyup.enter)="onItemClick(n.id, n.actionUrl, panel)"
  >
    <i [class]="iconFor(n.type)" class="mt-1 text-primary" aria-hidden="true"></i>
    <div class="flex-1 min-w-0">
      <p
        class="m-0 font-medium text-sm white-space-nowrap overflow-hidden text-overflow-ellipsis"
      >
        {{ n.title }}
      </p>
      <p
        class="m-0 text-color-secondary text-xs mt-1 white-space-nowrap overflow-hidden text-overflow-ellipsis"
      >
        {{ n.message }}
      </p>
      <span class="text-color-secondary text-xs">
        {{ n.sentAt | date: 'short' }}
      </span>

      <!-- Inline swap actions — only for pending SwapRequest notifications -->
      @if (n.type === 'SwapRequest' && parseSwapIds(n.actionUrl)) {
        <div class="flex gap-2 mt-2" (click)="$event.stopPropagation()">
          <p-button
            label="Accept"
            severity="success"
            size="small"
            icon="pi pi-check"
            [loading]="respondingId() === n.id"
            [disabled]="respondingId() !== null && respondingId() !== n.id"
            (onClick)="respondToSwap($event, n.id, n.actionUrl, true, panel)"
            aria-label="Accept swap request"
          />
          <p-button
            label="Decline"
            severity="danger"
            size="small"
            icon="pi pi-times"
            [outlined]="true"
            [loading]="respondingId() === n.id"
            [disabled]="respondingId() !== null && respondingId() !== n.id"
            (onClick)="respondToSwap($event, n.id, n.actionUrl, false, panel)"
            aria-label="Decline swap request"
          />
        </div>
      }
    </div>
    @if (!n.isRead) {
      <span
        class="unread-dot flex-shrink-0 mt-1"
        aria-label="Unread"
        pTooltip="Unread"
        tooltipPosition="left"
      ></span>
    }
  </li>
} @empty {
  <li class="px-3 py-4 text-center text-color-secondary text-sm" role="listitem">
    No notifications
  </li>
}
```

> **Note**: `parseSwapIds` must be exposed on the class (not just at module scope)
> for template access, **or** use a method wrapper:
>
> ```typescript
> // In the class body:
> protected parseSwapIds = parseSwapIds; // expose module-level fn to template
> ```

---

### 5. On-Page-Focus Polling Fallback (SignalR Disconnect Edge Case)

The `NotificationSignalRService` already polls every 30 s after a permanent
disconnect. The AC requires **on-page-focus** refresh as an additional trigger.

Edit `NotificationBellComponent` to implement `OnInit` and listen for the
window `focus` event:

```typescript
import { OnInit, OnDestroy } from '@angular/core';

export class NotificationBellComponent implements OnInit, OnDestroy {
  // … existing properties …

  private focusHandler = () => this.svc.loadFromApi();

  ngOnInit(): void {
    window.addEventListener('focus', this.focusHandler);
  }

  ngOnDestroy(): void {
    window.removeEventListener('focus', this.focusHandler);
  }
}
```

> **Why**: When SignalR is disconnected and the user switches tabs and returns,
> the 30-second poll may not have fired yet. A focus event ensures the list
> is always fresh on tab return without any polling latency.

---

### 6. Confirm SignalR Badge for `SwapRequest` Notifications (No Changes Needed)

Review `src/health-platform-ui/src/app/core/services/notification.service.ts`.

The existing `received$` subscription:
```typescript
this.sub = this.signalr.received$.subscribe((payload) => {
  // …
  this._items.update((list) => [payload, ...list].slice(0, 20));
  if (!payload.isRead) {
    this._unread.update((n) => n + 1);    // ← already works for SwapRequest
  }
  if (HIGH_PRIORITY_TYPES.includes(payload.type)) {  // SwapRequest is HIGH_PRIORITY
    this.toast.add({ severity: 'warn', … });          // ← toast already fires
  }
});
```

`SwapRequest` is already in `HIGH_PRIORITY_TYPES` (see `notification.model.ts`),
so the badge increments and a toast fires on arrival.
**No changes required to `NotificationService`.**

---

## Edge Cases to Verify

| Scenario | Expected Behaviour |
|----------|--------------------|
| SwapRequest notification with valid `actionUrl` | Accept/Decline buttons render inline |
| SwapRequest notification with null/malformed `actionUrl` | No buttons rendered; clicking the notification navigates to `actionUrl` if set |
| 409 on respond (expired/already acted) | Toast: "This swap request has already expired or been actioned." |
| SignalR disconnected | 30-second poll fires (existing behaviour) |
| SignalR disconnected + user returns from another tab | `loadFromApi()` fires on `focus` event |
| Accept accepted — user's appointment time changes | Toast confirms; notification list refreshed via `loadFromApi()` |

## Verification Checklist

- [ ] `SwapRequest` notification shows Accept / Decline buttons in the popover
- [ ] Accept calls `respondToSwapRequest(..., true)` and shows success toast
- [ ] Decline calls `respondToSwapRequest(..., false)` and shows success toast
- [ ] 409 response shows "expired" user-friendly error toast
- [ ] Only one respond request in-flight at a time (`respondingId` disables other buttons)
- [ ] Badge increments when a new `SwapRequest` push arrives via SignalR
- [ ] High-priority toast fires for incoming `SwapRequest` (existing behaviour)
- [ ] Window `focus` listener triggers `loadFromApi()` (verify in DevTools Network tab)
- [ ] `OnDestroy` removes the `focus` listener (no memory leak)
