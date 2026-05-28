# Task 002: Wire "Add to Calendar" Buttons & Bulk Export

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-038 |
| **Epic** | EP-005 |
| **Layer** | Frontend — Angular component updates |
| **Priority** | High |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 001 complete — `IcsService` available |

## Objective

Surface ICS export in every appointment-facing view:

1. **`BookingConfirmationComponent`** — replace the ad-hoc inline `icsDataUri()` method
   with `IcsService.buildSingle()` + `IcsService.download()`.
2. **`AppointmentCardComponent`** — add `showAddToCalendar` input + "Add to Calendar"
   button that calls `IcsService.download()`.
3. **`CalendarViewComponent` (detail drawer)** — add "Add to Calendar" button for the
   selected appointment (uses `CalendarAppointmentDto`).
4. **`MyAppointmentsComponent`** — add "Export All" button that calls
   `IcsService.buildBulk()` on all upcoming appointments and downloads a single
   `.ics` file.

---

## Acceptance Criteria Covered

- AC: "Add to Calendar" button on appointment confirmation view
- AC: "Add to Calendar" button on appointment detail view (calendar drawer)
- AC: Download triggered via browser (no server-side file storage)
- AC: Bulk export: download all upcoming appointments as single .ics file
- AC: Bulk export excludes cancelled appointments (handled by `IcsService.buildBulk`)
- AC: Mobile browser — `.ics` opens in native calendar app (Blob download works on mobile)

---

## Design Notes

- `AppointmentCardComponent` receives `AppointmentItemDto`; use `IcsService.buildSingle`.
- `CalendarViewComponent` uses `CalendarAppointmentDto`; `IcsService.buildSingle` accepts
  the `IcsAppointment` interface which both DTOs satisfy (matching field names).
- "Export All" only appears when `upcomingAppointments().length > 0`.
- `BookingConfirmationComponent` uses `BookingConfirmationDto` which has `appointmentTime`
  (not `slotTime`/`endTime`). Derive `endTime` as `start + 30 minutes` (same as current
  inline approach). Map to `IcsAppointment` shape before passing to `IcsService`.

---

## Implementation Steps

### 1. Update `BookingConfirmationComponent`

File: `src/health-platform-ui/src/app/features/booking/booking-confirmation/booking-confirmation.component.ts`

**Remove** the existing `icsDataUri()` method and the `<a [href]="icsDataUri(...)">` anchor.
**Inject** `IcsService`.
**Add** a `addToCalendar(c)` method.
**Replace** the anchor with a `<p-button>` that calls `addToCalendar(c)`.

Key changes:

```typescript
import { IcsService } from '../../../core/services/ics.service';
// ...
readonly ics = inject(IcsService);

addToCalendar(c: BookingConfirmationDto): void {
  const start = new Date(c.appointmentTime);
  const end = new Date(start.getTime() + 30 * 60 * 1000);
  const content = this.ics.buildSingle({
    appointmentId: c.appointmentId,
    providerName:  c.providerName,
    slotTime:      start.toISOString(),
    endTime:       end.toISOString(),
  });
  this.ics.download(`appointment-${c.appointmentId}`, content);
}
```

Replace the `<a>` anchor in the template with:

```html
<p-button
  label="Add to Calendar"
  icon="pi pi-calendar-plus"
  severity="secondary"
  [outlined]="true"
  (onClick)="addToCalendar(c)"
/>
```

---

### 2. Update `AppointmentCardComponent`

File: `src/health-platform-ui/src/app/features/booking/appointment-card/appointment-card.component.ts`

Add `showAddToCalendar` input (default `false`) and inject `IcsService`.

**Additions to class:**

```typescript
import { inject } from '@angular/core';
import { IcsService } from '../../../core/services/ics.service';
// ...
@Input() showAddToCalendar = false;
private readonly ics = inject(IcsService);

downloadIcs(appt: AppointmentItemDto): void {
  const content = this.ics.buildSingle(appt);
  this.ics.download(`appointment-${appt.appointmentId}`, content);
}
```

**Add button after the existing Cancel / Reschedule buttons:**

```html
@if (showAddToCalendar) {
  <p-button
    label="Add to Calendar"
    severity="secondary"
    size="small"
    icon="pi pi-calendar-plus"
    [outlined]="true"
    (onClick)="downloadIcs(appointment)"
  />
}
```

**Enable in `MyAppointmentsComponent`** — pass `[showAddToCalendar]="true"` on both
upcoming and past `<app-appointment-card>` elements.

---

### 3. Update `CalendarViewComponent` detail drawer

File: `src/health-platform-ui/src/app/features/calendar/calendar-view.component.ts`

Inject `IcsService` and add a method:

```typescript
import { IcsService } from '../../core/services/ics.service';
// ...
private readonly ics = inject(IcsService);

protected addToCalendar(appt: CalendarAppointmentDto): void {
  const content = this.ics.buildSingle(appt);
  this.ics.download(`appointment-${appt.appointmentId}`, content);
}
```

Add the button inside the drawer `<div class="flex flex-column gap-3">` block,
after the status row and before the Cancel button:

```html
<p-button
  label="Add to Calendar"
  severity="secondary"
  [outlined]="true"
  icon="pi pi-calendar-plus"
  styleClass="w-full"
  (onClick)="addToCalendar(appt)"
/>
```

---

### 4. Add Bulk Export to `MyAppointmentsComponent`

File: `src/health-platform-ui/src/app/features/booking/my-appointments/my-appointments.component.ts`

Inject `IcsService` and add a method:

```typescript
import { IcsService } from '../../../core/services/ics.service';
// ...
private readonly ics = inject(IcsService);

exportAllToCalendar(): void {
  const content = this.ics.buildBulk(this.upcomingAppointments());
  this.ics.download('my-appointments', content);
}
```

Add an "Export All" button in the header row (next to "Book New"), shown only when
there are upcoming appointments:

```html
@if (upcomingCount() > 0) {
  <p-button
    label="Export All"
    icon="pi pi-download"
    severity="secondary"
    size="small"
    [outlined]="true"
    (onClick)="exportAllToCalendar()"
  />
}
```

---

### 5. Update component smoke tests

#### `booking-confirmation.component.spec.ts`

If a spec file does not yet exist, create one with a single smoke test:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BookingConfirmationComponent } from './booking-confirmation.component';
import { provideHttpClient } from '@angular/common/http';
import { MessageService } from 'primeng/api';

describe('BookingConfirmationComponent', () => {
  let fixture: ComponentFixture<BookingConfirmationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BookingConfirmationComponent],
      providers: [provideHttpClient(), MessageService],
    }).compileComponents();
    fixture = TestBed.createComponent(BookingConfirmationComponent);
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
```

---

## Verification

```bash
cd src/health-platform-ui
npx ng test --no-watch
```

Expected: all prior tests pass + 4 `IcsService` tests from Task 001 still green.

```bash
npx ng build
npx ng lint
```

Expected: build clean, `All files pass linting.`

### Manual smoke-test checklist

| Step | Expected |
|------|----------|
| Book appointment → confirmation screen | "Add to Calendar" button downloads `.ics` |
| Open `.ics` in OS | Opens in calendar app; event has correct time, provider name, 1-hour alarm |
| My Appointments → upcoming list | Each card has "Add to Calendar" button |
| My Appointments → "Export All" | Downloads single `.ics` with all upcoming appointments |
| Calendar drawer → open any appointment | "Add to Calendar" button present, downloads single `.ics` |
| Exported `.ics` | Does NOT contain visit reason or patient notes |
| Cancelled appointment | Not included in bulk export |
