# Task 003: Angular — Complete Intake Button, Status Badge & Staff Filter

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-043 |
| **Epic** | EP-006 |
| **Layer** | Angular Frontend — models, service, components, routing guard |
| **Priority** | High |
| **Estimated Effort** | 35 minutes |
| **Dependencies** | Tasks 001–002 complete — `PatientAppointmentDto` includes `intakeStatus` + `isIntakeWindowOpen`; `TodayAppointmentItemDto` includes `intakeStatus`; `GET /appointments/{id}/intake-window` endpoint live |

## Objective

1. **Extend `booking.models.ts`** — add `intakeStatus` and `isIntakeWindowOpen` to `AppointmentItemDto`; add `intakeStatus` to a new `StaffAppointmentItemDto`
2. **Update `AppointmentCardComponent`** — show intake status `p-tag` badge; add "Complete Intake" `p-button` for upcoming appointments within the window
3. **Create `IntakeWindowGuard`** — route guard that calls `GET /appointments/{id}/intake-window`; on closed window, shows a toast and redirects to `/intake`
4. **Update `MyAppointmentsComponent`** — wire intake button click → route to `/intake/form?appointmentId=...`
5. **Update staff appointment search component** — add "Intake Pending" filter chip that sets `hasIntakePending=true`
6. **2 spec smoke tests** — `AppointmentCardComponent` (with intake props) + `IntakeWindowGuard`

---

## Acceptance Criteria Covered

- AC: "Complete Intake" button on upcoming appointment detail (if not yet completed)
- AC: Intake available 7 days before appointment through 15 minutes after check-in (guard enforces)
- AC: Appointment detail shows intake status: Not Started, In Progress, Completed
- AC: Staff dashboard filter: "Intake Pending" for today's appointments
- AC: Completed badge shown on appointment card in patient's "My Appointments" list
- AC: Intake link accessed after appointment completed → "Intake period has ended" message (toast + redirect)

---

## Design Notes

### Intake status display mapping

| `intakeStatus` value | Badge label | `p-tag` severity |
|---|---|---|
| `null` (no record) | Not Started | `warn` |
| `'Draft'` | In Progress | `warn` |
| `'Completed'` | Completed | `success` |
| `'ReviewedByProvider'` | Reviewed | `info` |
| `'Orphaned'` | Orphaned | `danger` |

### "Complete Intake" button visibility rule (Angular)

Show when **all** of:
- `appointment.status` is `'Scheduled'` or `'Booked'` (upcoming)
- `appointment.intakeStatus` is `null` or `'Draft'`
- `appointment.isIntakeWindowOpen === true`

---

## Implementation Steps

### 1. Extend `booking.models.ts`

In `src/health-platform-ui/src/app/core/models/booking.models.ts`, update `AppointmentItemDto`:

```typescript
export interface AppointmentItemDto {
  appointmentId: string;
  providerId: string;
  providerName: string;
  slotTime: string;
  endTime: string;
  status: AppointmentStatus;
  visitReason: string | null;
  patientName: string;
  intakeStatus: string | null;       // null = no record yet ("Not Started")
  isIntakeWindowOpen: boolean;
}
```

Also add a staff-facing type (used by the check-in screen):

```typescript
export interface StaffAppointmentItemDto {
  appointmentId: string;
  patientId: string;
  patientFullName: string;
  status: string;
  slotTime: string;
  isWalkIn: boolean;
  isLateArrival: boolean;
  arrivalTime: string | null;
  intakeStatus: string | null;
}
```

### 2. Add `IntakeWindowService` to Angular

Create `src/health-platform-ui/src/app/core/services/intake-window.service.ts`:

```typescript
import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface IntakeWindowDto {
  isOpen: boolean;
  reason: string | null;
}

@Injectable({ providedIn: 'root' })
export class IntakeWindowService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  check(appointmentId: string): Observable<IntakeWindowDto> {
    return this.http.get<IntakeWindowDto>(
      `${this.base}/appointments/${appointmentId}/intake-window`
    );
  }
}
```

### 3. Create `IntakeWindowGuard`

Create `src/health-platform-ui/src/app/features/intake/intake-window.guard.ts`:

```typescript
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { IntakeWindowService } from '../../core/services/intake-window.service';
import { ToastService } from '../../shared/services/toast.service';

export const intakeWindowGuard: CanActivateFn = async (route) => {
  const appointmentId = route.queryParamMap.get('appointmentId');
  if (!appointmentId) return true; // no appointment context — allow through

  const windowService = inject(IntakeWindowService);
  const toast         = inject(ToastService);
  const router        = inject(Router);

  try {
    const { isOpen, reason } = await firstValueFrom(windowService.check(appointmentId));
    if (!isOpen) {
      toast.warn('Intake unavailable', reason ?? 'Intake period has ended.');
      return router.parseUrl('/intake');
    }
    return true;
  } catch {
    // On error, allow through — server will enforce
    return true;
  }
};
```

### 4. Register guard on intake form route

In `src/health-platform-ui/src/app/features/intake/intake.routes.ts`, update the `'form'` route:

```typescript
{
  path: 'form',
  canActivate: [intakeWindowGuard],
  loadComponent: () =>
    import('./intake-landing/intake-landing.component').then((m) => m.IntakeLandingComponent),
},
```

### 5. Update `AppointmentCardComponent`

Open `src/health-platform-ui/src/app/features/booking/appointment-card/appointment-card.component.ts`.

Add intake status helper methods and the "Complete Intake" button to the template:

**Add to the class:**
```typescript
@Input() showIntakeButton = false;
@Output() intakeRequest = new EventEmitter<AppointmentItemDto>();

intakeStatusLabel(status: string | null): string {
  if (!status) return 'Not Started';
  if (status === 'Draft') return 'In Progress';
  if (status === 'Completed') return 'Completed';
  if (status === 'ReviewedByProvider') return 'Reviewed';
  return status;
}

intakeStatusSeverity(status: string | null): TagSeverity {
  if (!status || status === 'Draft') return 'warn';
  if (status === 'Completed') return 'success';
  if (status === 'ReviewedByProvider') return 'info';
  return 'danger';
}

get canCompleteIntake(): boolean {
  const upcoming = ['Scheduled', 'Booked'];
  return (
    upcoming.includes(this.appointment.status) &&
    (this.appointment.intakeStatus == null || this.appointment.intakeStatus === 'Draft') &&
    this.appointment.isIntakeWindowOpen === true
  );
}
```

**Add to the card template** (inside the flex column with action buttons):
```html
<!-- Intake status badge — always show when there is a record -->
@if (appointment.intakeStatus !== undefined) {
  <p-tag
    [value]="intakeStatusLabel(appointment.intakeStatus)"
    [severity]="intakeStatusSeverity(appointment.intakeStatus)"
  />
}
<!-- Complete Intake CTA -->
@if (canCompleteIntake) {
  <p-button
    label="Complete Intake"
    severity="info"
    size="small"
    icon="pi pi-clipboard"
    [outlined]="true"
    (onClick)="intakeRequest.emit(appointment)"
  />
}
```

### 6. Update `MyAppointmentsComponent`

Open `src/health-platform-ui/src/app/features/booking/my-appointments/my-appointments.component.ts`.

Add `Router` injection and a handler for the intake button:

```typescript
private readonly router = inject(Router);

onIntakeRequest(appointment: AppointmentItemDto): void {
  this.router.navigate(['/intake/form'], {
    queryParams: { appointmentId: appointment.appointmentId },
  });
}
```

In the template where `<app-appointment-card>` is rendered, add:
```html
[showIntakeButton]="true"
(intakeRequest)="onIntakeRequest($event)"
```

### 7. Update Staff Check-in Component

Open `src/health-platform-ui/src/app/features/booking/appointment-card/appointment-card.component.ts` — no change needed here.

Find the staff check-in / today's appointments component. Open the relevant component in `src/health-platform-ui/src/app/features/` (e.g., `clinical/` or `dashboard/`).

Add an "Intake Pending" filter toggle:

```typescript
intakePendingFilter = signal(false);

toggleIntakePending(): void {
  this.intakePendingFilter.update(v => !v);
  this.loadTodayAppointments();
}

loadTodayAppointments(): void {
  // pass hasIntakePending: this.intakePendingFilter() to the API query
}
```

In the template, add a toggle button:
```html
<p-button
  [label]="intakePendingFilter() ? 'All Patients' : 'Intake Pending'"
  [severity]="intakePendingFilter() ? 'contrast' : 'warn'"
  icon="pi pi-filter"
  size="small"
  [outlined]="!intakePendingFilter()"
  (onClick)="toggleIntakePending()"
/>
```

---

## Implementation Steps — Tests

### Spec 1: `AppointmentCardComponent` intake badge smoke test

Add to `src/health-platform-ui/src/app/features/booking/appointment-card/`:

File: `appointment-card-intake.spec.ts`

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { AppointmentCardComponent } from './appointment-card.component';
import { AppointmentStatus } from '../../../core/models/booking.models';

const MOCK_APPOINTMENT = {
  appointmentId: '11111111-1111-1111-1111-111111111111',
  providerId: '22222222-2222-2222-2222-222222222222',
  providerName: 'Dr. Test',
  slotTime: new Date(Date.now() + 86400000).toISOString(),
  endTime: new Date(Date.now() + 90000000).toISOString(),
  status: AppointmentStatus.Booked,
  visitReason: null,
  patientName: 'Jane Doe',
  intakeStatus: null,
  isIntakeWindowOpen: true,
};

describe('AppointmentCardComponent (intake)', () => {
  let fixture: ComponentFixture<AppointmentCardComponent>;
  let component: AppointmentCardComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppointmentCardComponent],
      providers: [provideRouter([]), provideNoopAnimations()],
    }).compileComponents();

    fixture = TestBed.createComponent(AppointmentCardComponent);
    component = fixture.componentInstance;
    component.appointment = MOCK_APPOINTMENT as any;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('canCompleteIntake returns true for upcoming appointment with open window', () => {
    expect(component.canCompleteIntake).toBe(true);
  });

  it('intakeStatusLabel returns "Not Started" for null status', () => {
    expect(component.intakeStatusLabel(null)).toBe('Not Started');
  });
});
```

### Spec 2: `IntakeWindowGuard` smoke test

Create `src/health-platform-ui/src/app/features/intake/intake-window.guard.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { IntakeWindowService } from '../../core/services/intake-window.service';

describe('IntakeWindowGuard dependencies', () => {
  it('should resolve IntakeWindowService', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        IntakeWindowService,
      ],
    });
    const svc = TestBed.inject(IntakeWindowService);
    expect(svc).toBeTruthy();
  });
});
```

---

## Verification

```bash
cd src/health-platform-ui
npx ng lint --fix
npx ng test --no-watch
```

Expected: **27/27** tests (24 baseline + 3 new — 2 card-intake + 1 guard).
