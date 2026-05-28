# Task 002: Swap UI Components + Appointment Card Integration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-031 |
| **Epic** | EP-003 |
| **Layer** | Angular / Features / Booking (components + wiring) |
| **Priority** | Medium |
| **Estimated Effort** | 90 minutes |
| **Dependencies** | Task 001 (`SwapService`, `SwappableSlotDto`, `SwapRequestDto`, `SwapHistoryItemDto`) |

## Objective

Build the full patient-facing swap slot UI flow:

1. **`SwapSlotBrowserComponent`** — modal dialog listing anonymized swappable slots.
2. **`SwapConfirmDialogComponent`** — confirmation dialog before submitting the request.
3. **`SwapHistoryComponent`** — inline history list (Pending / Accepted / Declined / Cancelled / Expired) shown on appointment detail.
4. **Update `AppointmentCardComponent`** — add "Swap Slot" button and embed `SwapHistoryComponent`.
5. **Update `MyAppointmentsComponent`** — orchestrate the multi-step dialog flow and call `SwapService`.

## Acceptance Criteria Covered

- AC: "Swap Slot" button on appointment detail for eligible appointments
- AC: Swap browser — list of available slots (anonymized, time only)
- AC: Swap request confirmation dialog — "Offer your [time] for [selected time]?"
- AC: Swap history visible in appointment detail (requested, accepted, declined)
- AC: Loading / error states for swap operations
- AC: Mobile-responsive swap interface
- AC: No swappable slots available → "No swap options available" empty state
- AC: Swap request expired while viewing → handled via 409 response mapped to toast

---

## Implementation Steps

### 1. Create `SwapSlotBrowserComponent`

Create `src/health-platform-ui/src/app/features/booking/swap/swap-slot-browser/swap-slot-browser.component.ts`:

```typescript
import { CommonModule } from '@angular/common';
import {
  Component,
  EventEmitter,
  inject,
  Input,
  OnChanges,
  Output,
  signal,
} from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { SkeletonModule } from 'primeng/skeleton';
import { SwapService } from '../../../../core/services/swap.service';
import {
  AppointmentItemDto,
  SwappableSlotDto,
} from '../../../../core/models/booking.models';

@Component({
  selector: 'app-swap-slot-browser',
  standalone: true,
  imports: [CommonModule, DialogModule, ButtonModule, SkeletonModule],
  template: `
    <p-dialog
      header="Choose a Slot to Swap"
      [(visible)]="visible"
      [modal]="true"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: 'min(480px, 95vw)' }"
      (onHide)="cancel.emit()"
    >
      @if (loading()) {
        @for (i of [1, 2, 3]; track i) {
          <div class="mb-2">
            <p-skeleton height="2.5rem" />
          </div>
        }
      } @else if (error()) {
        <div class="text-center py-4 text-color-secondary">
          <i
            class="pi pi-exclamation-circle mb-2"
            style="font-size:1.5rem;display:block"
            aria-hidden="true"
          ></i>
          Failed to load available slots. Please try again.
        </div>
        <div class="flex justify-content-end mt-3">
          <p-button label="Close" severity="secondary" (onClick)="cancel.emit()" />
        </div>
      } @else if (slots().length === 0) {
        <div class="text-center py-4 text-color-secondary" role="status">
          <i
            class="pi pi-calendar-times mb-2"
            style="font-size:1.5rem;display:block"
            aria-hidden="true"
          ></i>
          No swap options available for this appointment.
        </div>
        <div class="flex justify-content-end mt-3">
          <p-button label="Close" severity="secondary" (onClick)="cancel.emit()" />
        </div>
      } @else {
        <p class="text-sm text-color-secondary mb-3">
          Your current appointment:
          <strong>{{ appointment.slotTime | date: 'h:mm a, EEE MMM d' }}</strong
          >. Select a slot to offer in exchange:
        </p>
        <ul class="list-none m-0 p-0" role="listbox" aria-label="Available swap slots">
          @for (slot of slots(); track slot.appointmentId) {
            <li
              class="flex align-items-center justify-content-between p-2 border-1 border-round mb-2 cursor-pointer"
              [class.slot-selected]="selectedSlot()?.appointmentId === slot.appointmentId"
              (click)="selectedSlot.set(slot)"
              (keyup.enter)="selectedSlot.set(slot)"
              tabindex="0"
              role="option"
              [attr.aria-selected]="selectedSlot()?.appointmentId === slot.appointmentId"
              [attr.aria-label]="'Swap with slot at ' + (slot.slotTime | date: 'h:mm a, EEE MMM d')"
            >
              <span class="font-medium">{{ slot.slotTime | date: 'h:mm a' }}</span>
              <span class="text-color-secondary text-sm">{{
                slot.slotTime | date: 'EEE, MMM d'
              }}</span>
            </li>
          }
        </ul>
        <div class="flex justify-content-end gap-2 mt-3">
          <p-button
            label="Cancel"
            severity="secondary"
            [outlined]="true"
            (onClick)="cancel.emit()"
          />
          <p-button
            label="Next"
            icon="pi pi-arrow-right"
            iconPos="right"
            [disabled]="!selectedSlot()"
            (onClick)="slotSelected.emit(selectedSlot()!)"
          />
        </div>
      }
    </p-dialog>
  `,
  styles: [
    `
      .slot-selected {
        border-color: var(--p-primary-color) !important;
        background-color: var(--p-primary-50, #eff6ff);
      }
    `,
  ],
})
export class SwapSlotBrowserComponent implements OnChanges {
  @Input({ required: true }) appointment!: AppointmentItemDto;
  @Input() visible = false;

  @Output() slotSelected = new EventEmitter<SwappableSlotDto>();
  @Output() cancel = new EventEmitter<void>();

  private readonly swapSvc = inject(SwapService);

  readonly slots = signal<SwappableSlotDto[]>([]);
  readonly selectedSlot = signal<SwappableSlotDto | null>(null);
  readonly loading = signal(true);
  readonly error = signal(false);

  ngOnChanges(): void {
    if (!this.visible) return;
    // Reset state each time the dialog opens
    this.loading.set(true);
    this.error.set(false);
    this.selectedSlot.set(null);

    this.swapSvc.getSwappableSlots(this.appointment.appointmentId).subscribe({
      next: (data) => {
        this.slots.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }
}
```

---

### 2. Create `SwapConfirmDialogComponent`

Create `src/health-platform-ui/src/app/features/booking/swap/swap-confirm-dialog/swap-confirm-dialog.component.ts`:

```typescript
import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import {
  AppointmentItemDto,
  SwappableSlotDto,
} from '../../../../core/models/booking.models';

@Component({
  selector: 'app-swap-confirm-dialog',
  standalone: true,
  imports: [CommonModule, DialogModule, ButtonModule],
  template: `
    <p-dialog
      header="Confirm Slot Swap"
      [(visible)]="visible"
      [modal]="true"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: 'min(420px, 95vw)' }"
      (onHide)="back.emit()"
    >
      <p class="mb-3 line-height-3">
        Offer your
        <strong>{{ appointment.slotTime | date: 'h:mm a, EEE MMM d' }}</strong>
        appointment in exchange for the
        <strong>{{ targetSlot.slotTime | date: 'h:mm a, EEE MMM d' }}</strong>
        slot?
      </p>
      <p class="text-sm text-color-secondary mb-0">
        The other patient must accept this request. You may cancel it at any time
        while it remains pending.
      </p>
      <div class="flex justify-content-end gap-2 mt-4">
        <p-button
          label="Back"
          severity="secondary"
          [outlined]="true"
          [disabled]="submitting"
          (onClick)="back.emit()"
        />
        <p-button
          label="Send Request"
          icon="pi pi-check"
          [loading]="submitting"
          (onClick)="confirm.emit()"
        />
      </div>
    </p-dialog>
  `,
})
export class SwapConfirmDialogComponent {
  @Input({ required: true }) appointment!: AppointmentItemDto;
  @Input({ required: true }) targetSlot!: SwappableSlotDto;
  @Input() visible = false;
  @Input() submitting = false;

  @Output() confirm = new EventEmitter<void>();
  @Output() back = new EventEmitter<void>();
}
```

---

### 3. Create `SwapHistoryComponent`

Create `src/health-platform-ui/src/app/features/booking/swap/swap-history/swap-history.component.ts`:

```typescript
import { CommonModule } from '@angular/common';
import { Component, inject, Input, OnChanges, signal } from '@angular/core';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { SwapService } from '../../../../core/services/swap.service';
import {
  SwapHistoryItemDto,
  SwapRequestStatus,
} from '../../../../core/models/booking.models';

type TagSeverity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast';

const STATUS_SEVERITY: Record<SwapRequestStatus, TagSeverity> = {
  [SwapRequestStatus.Pending]:   'info',
  [SwapRequestStatus.Accepted]:  'success',
  [SwapRequestStatus.Declined]:  'danger',
  [SwapRequestStatus.Cancelled]: 'secondary',
  [SwapRequestStatus.Expired]:   'warn',
};

@Component({
  selector: 'app-swap-history',
  standalone: true,
  imports: [CommonModule, SkeletonModule, TagModule],
  template: `
    <div class="swap-history mt-3">
      <div class="font-semibold text-sm mb-2">Swap History</div>

      @if (loading()) {
        <p-skeleton height="2rem" />
      } @else if (history().length === 0) {
        <p class="text-sm text-color-secondary m-0">No swap requests for this appointment.</p>
      } @else {
        <ul class="list-none m-0 p-0" role="list" aria-label="Swap request history">
          @for (item of history(); track item.swapRequestId) {
            <li
              class="flex align-items-center justify-content-between py-2 border-bottom-1 surface-border gap-2"
              role="listitem"
            >
              <div class="text-sm flex-1 min-w-0">
                <span>
                  Offered
                  <strong>{{ item.requesterSlotTime | date: 'h:mm a, MMM d' }}</strong>
                  for
                  <strong>{{ item.targetSlotTime | date: 'h:mm a, MMM d' }}</strong>
                </span>
              </div>
              <p-tag
                [value]="item.status"
                [severity]="statusSeverity(item.status)"
                styleClass="flex-shrink-0"
              />
            </li>
          }
        </ul>
      }
    </div>
  `,
})
export class SwapHistoryComponent implements OnChanges {
  @Input({ required: true }) appointmentId!: string;

  private readonly swapSvc = inject(SwapService);

  readonly history = signal<SwapHistoryItemDto[]>([]);
  readonly loading = signal(true);

  ngOnChanges(): void {
    this.loading.set(true);
    this.swapSvc.getSwapHistory(this.appointmentId).subscribe({
      next: (data) => {
        this.history.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  statusSeverity(status: SwapRequestStatus): TagSeverity {
    return STATUS_SEVERITY[status] ?? 'secondary';
  }
}
```

---

### 4. Update `AppointmentCardComponent`

Edit `src/health-platform-ui/src/app/features/booking/appointment-card/appointment-card.component.ts`.

#### 4a. Add imports

Add to the imports array:
```typescript
import { SwapHistoryComponent } from '../swap/swap-history/swap-history.component';
```
Add `SwapHistoryComponent` to `imports: [...]`.

#### 4b. Add `@Input` and `@Output`

```typescript
@Input() showSwap = false;
@Input() showSwapHistory = false;
@Output() swapRequest = new EventEmitter<AppointmentItemDto>();
```

#### 4c. Add "Swap Slot" button to the template

Inside the `<div class="flex flex-column align-items-end gap-2">` block, after the `showReschedule` button:

```html
@if (showSwap) {
  <p-button
    label="Swap Slot"
    severity="secondary"
    size="small"
    icon="pi pi-arrows-h"
    [outlined]="true"
    aria-label="Request a slot swap for this appointment"
    (onClick)="swapRequest.emit(appointment)"
  />
}
```

#### 4d. Add swap history section to the template

At the bottom of the card body, after the status/button section:

```html
@if (showSwapHistory) {
  <app-swap-history [appointmentId]="appointment.appointmentId" />
}
```

---

### 5. Update `MyAppointmentsComponent`

Edit `src/health-platform-ui/src/app/features/booking/my-appointments/my-appointments.component.ts`.

#### 5a. Add imports

```typescript
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { SwapSlotBrowserComponent } from '../swap/swap-slot-browser/swap-slot-browser.component';
import { SwapConfirmDialogComponent } from '../swap/swap-confirm-dialog/swap-confirm-dialog.component';
import { SwapService } from '../../../core/services/swap.service';
import { SwappableSlotDto } from '../../../core/models/booking.models';
```

Add `SwapSlotBrowserComponent`, `SwapConfirmDialogComponent`, `ToastModule` to `imports: [...]`.

> **Note**: `MessageService` must be provided in the component or a parent module.
> Check if `app.config.ts` provides it globally; if not, add `providers: [MessageService]`
> to the component decorator.

#### 5b. Add state signals

```typescript
private readonly swapSvc = inject(SwapService);
private readonly toast = inject(MessageService);

readonly swapTargetAppt = signal<AppointmentItemDto | null>(null);
readonly selectedSwapSlot = signal<SwappableSlotDto | null>(null);
readonly showSwapBrowser = signal(false);
readonly showSwapConfirm = signal(false);
readonly swapSubmitting = signal(false);
```

#### 5c. Add swap flow methods

```typescript
openSwapBrowser(appt: AppointmentItemDto): void {
  this.swapTargetAppt.set(appt);
  this.selectedSwapSlot.set(null);
  this.showSwapBrowser.set(true);
}

onSlotSelected(slot: SwappableSlotDto): void {
  this.selectedSwapSlot.set(slot);
  this.showSwapBrowser.set(false);
  this.showSwapConfirm.set(true);
}

onSwapConfirmed(): void {
  const appt = this.swapTargetAppt();
  const slot = this.selectedSwapSlot();
  if (!appt || !slot) return;

  this.swapSubmitting.set(true);
  this.swapSvc.initiateSwapRequest(appt.appointmentId, slot.appointmentId).subscribe({
    next: () => {
      this.swapSubmitting.set(false);
      this.showSwapConfirm.set(false);
      this.swapTargetAppt.set(null);
      this.toast.add({
        severity: 'success',
        summary: 'Swap request sent',
        detail: 'The other patient has been notified.',
        life: 5_000,
      });
      // Reload appointments so history reflects the new Pending request
      this.store.loadAppointments();
    },
    error: (err) => {
      this.swapSubmitting.set(false);
      const detail =
        err?.status === 409
          ? 'This request has expired or a conflict occurred. Please try another slot.'
          : 'Failed to send swap request. Please try again.';
      this.toast.add({ severity: 'error', summary: 'Swap failed', detail, life: 6_000 });
    },
  });
}

closeSwapBrowser(): void {
  this.showSwapBrowser.set(false);
  this.swapTargetAppt.set(null);
}

closeSwapConfirm(): void {
  this.showSwapConfirm.set(false);
  this.showSwapBrowser.set(true); // return to browser on "Back"
}
```

#### 5d. Wire dialogs in the template

Inside the template, after the `<p-tabs>` block, add:

```html
<!-- Swap slot browser dialog -->
@if (swapTargetAppt() && showSwapBrowser()) {
  <app-swap-slot-browser
    [appointment]="swapTargetAppt()!"
    [visible]="showSwapBrowser()"
    (slotSelected)="onSlotSelected($event)"
    (cancel)="closeSwapBrowser()"
  />
}

<!-- Swap confirmation dialog -->
@if (swapTargetAppt() && selectedSwapSlot() && showSwapConfirm()) {
  <app-swap-confirm-dialog
    [appointment]="swapTargetAppt()!"
    [targetSlot]="selectedSwapSlot()!"
    [visible]="showSwapConfirm()"
    [submitting]="swapSubmitting()"
    (confirm)="onSwapConfirmed()"
    (back)="closeSwapConfirm()"
  />
}
```

#### 5e. Update `<app-appointment-card>` usage in the upcoming tab

Change the upcoming tab's `@for` block to enable swap:

```html
@for (appt of upcomingAppointments(); track appt.appointmentId) {
  <app-appointment-card
    [appointment]="appt"
    [showCancel]="true"
    [showSwap]="true"
    [showSwapHistory]="true"
    [showAddToCalendar]="true"
    (cancelRequest)="openCancelDialog($event)"
    (swapRequest)="openSwapBrowser($event)"
  />
}
```

---

## Edge Cases to Verify

| Scenario | Expected Behaviour |
|----------|--------------------|
| No swappable slots | `SwapSlotBrowserComponent` renders "No swap options available" empty state |
| API error loading slots | Error state rendered; user can close and retry |
| Swap request expired (409 from initiate) | Toast: "This request has expired or a conflict occurred." |
| Back button in confirm dialog | Returns to slot browser without resetting selected appointment |
| Mobile viewport | Dialogs use `width: min(480px, 95vw)` — fully responsive |

## Verification Checklist

- [ ] "Swap Slot" button visible on upcoming appointments
- [ ] `SwapSlotBrowserComponent` loads and displays anonymized slots
- [ ] Empty state shown when no slots available
- [ ] Selecting a slot and clicking Next opens `SwapConfirmDialogComponent`
- [ ] Back from confirm returns to browser
- [ ] Confirming a swap calls `SwapService.initiateSwapRequest()` and shows success toast
- [ ] 409 error on confirm shows "expired" user-friendly message
- [ ] `SwapHistoryComponent` lists historical requests with correct status badges
- [ ] All dialogs render correctly on mobile (< 640 px)
- [ ] No `any` types; no direct `HttpClient` calls in components
