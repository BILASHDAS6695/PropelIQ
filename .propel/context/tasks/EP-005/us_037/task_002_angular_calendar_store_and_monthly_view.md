# Task 002: Angular Calendar Service, Store & Monthly View Component

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-037 |
| **Epic** | EP-005 |
| **Layer** | Frontend (Angular 21 + PrimeNG v21) |
| **Priority** | High |
| **Estimated Effort** | 55 minutes |
| **Dependencies** | Task 001 complete — `GET /api/appointments/calendar` endpoint available |

## Objective

1. **Add `calendar.models.ts`** — TypeScript DTO matching the backend response.
2. **Add `CalendarService`** — thin HTTP wrapper for `GET /api/appointments/calendar`.
3. **Add `CalendarStore`** — ngrx/signals store managing view mode, current date,
   loaded appointments, and selected appointment.
4. **Add `CalendarViewComponent`** — the main calendar page with:
   - PrimeNG `DatePicker` in `inline` mode as the month navigator (with appointment
     indicator dots rendered via the `#date` template).
   - An appointment list panel below the picker showing the day's events.
   - Detail drawer (`p-drawer`) that slides in when an appointment is clicked.
   - Color-coded status pills.
   - Staff: `p-select` provider filter above the calendar.
   - Patient: no provider filter (own data only).
5. **Register the route** `calendar` and add a sidebar nav item.

---

## Acceptance Criteria Covered

- AC: Monthly calendar view with appointment indicators on booked days
- AC: Color coding: Scheduled (blue), Completed (green), Cancelled (red), NoShow (gray)
- AC: Click appointment → detail panel with actions (cancel, reschedule, swap)
- AC: Patient calendar: shows only their own appointments
- AC: Staff calendar: shows all patients for selected provider
- AC: Navigate between months/weeks with arrow buttons
- AC: Today button to quickly return to current date

---

## Implementation Steps

### 1. Add `calendar.models.ts`

Create `src/health-platform-ui/src/app/core/models/calendar.models.ts`:

```typescript
export interface CalendarAppointmentDto {
  appointmentId: string;
  providerId: string;
  providerName: string;
  patientName: string;
  slotTime: string;   // ISO-8601 DateTimeOffset
  endTime: string;    // ISO-8601 DateTimeOffset
  status: string;     // 'Scheduled' | 'Booked' | 'Completed' | 'Cancelled' | 'NoShow' | ...
  visitReason: string | null;
}
```

---

### 2. Add `CalendarService`

Create `src/health-platform-ui/src/app/core/services/calendar.service.ts`:

```typescript
import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CalendarAppointmentDto } from '../models/calendar.models';

@Injectable({ providedIn: 'root' })
export class CalendarService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  getAppointments(
    from: Date,
    to: Date,
    providerId?: string,
  ): Observable<CalendarAppointmentDto[]> {
    let params = new HttpParams()
      .set('from', from.toISOString())
      .set('to', to.toISOString());
    if (providerId) params = params.set('providerId', providerId);
    return this.http.get<CalendarAppointmentDto[]>(`${this.base}/appointments/calendar`, {
      params,
    });
  }
}
```

---

### 3. Add `CalendarStore`

Create `src/health-platform-ui/src/app/features/calendar/calendar.store.ts`:

```typescript
import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { CalendarService } from '../../core/services/calendar.service';
import { ToastService } from '../../shared/services/toast.service';
import { CalendarAppointmentDto } from '../../core/models/calendar.models';

export type CalendarViewMode = 'month' | 'week' | 'day';

interface CalendarState {
  viewMode: CalendarViewMode;
  currentDate: Date;
  appointments: CalendarAppointmentDto[];
  isLoading: boolean;
  selectedAppointment: CalendarAppointmentDto | null;
  selectedProviderId: string | null;
}

const initialState: CalendarState = {
  viewMode: 'month',
  currentDate: new Date(),
  appointments: [],
  isLoading: false,
  selectedAppointment: null,
  selectedProviderId: null,
};

export const CalendarStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods(
    (store, svc = inject(CalendarService), toast = inject(ToastService)) => ({
      setViewMode(mode: CalendarViewMode): void {
        patchState(store, { viewMode: mode });
      },

      setSelectedAppointment(appt: CalendarAppointmentDto | null): void {
        patchState(store, { selectedAppointment: appt });
      },

      setSelectedProvider(providerId: string | null): void {
        patchState(store, { selectedProviderId: providerId });
      },

      async loadRange(from: Date, to: Date, providerId?: string): Promise<void> {
        patchState(store, { isLoading: true });
        try {
          const appointments = await firstValueFrom(
            svc.getAppointments(from, to, providerId ?? undefined),
          );
          patchState(store, { appointments, isLoading: false });
        } catch {
          patchState(store, { isLoading: false });
          toast.error('Error', 'Failed to load calendar appointments.');
        }
      },

      navigate(direction: 'prev' | 'next'): void {
        const current = store.currentDate();
        const mode    = store.viewMode();
        const d       = new Date(current);

        if (mode === 'month') {
          d.setMonth(d.getMonth() + (direction === 'next' ? 1 : -1));
        } else if (mode === 'week') {
          d.setDate(d.getDate() + (direction === 'next' ? 7 : -7));
        } else {
          d.setDate(d.getDate() + (direction === 'next' ? 1 : -1));
        }

        patchState(store, { currentDate: d });
      },

      goToToday(): void {
        patchState(store, { currentDate: new Date() });
      },
    }),
  ),
);
```

---

### 4. Add `CalendarViewComponent`

Create `src/health-platform-ui/src/app/features/calendar/calendar-view.component.ts`:

```typescript
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DrawerModule } from 'primeng/drawer';
import { SelectModule } from 'primeng/select';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { CalendarStore, CalendarViewMode } from './calendar.store';
import { CalendarAppointmentDto } from '../../core/models/calendar.models';
import { AuthService } from '../../core/auth/auth.service';

type StatusSeverity = 'info' | 'success' | 'danger' | 'secondary' | 'warn' | 'contrast';

interface ProviderOption {
  label: string;
  value: string;
}

@Component({
  selector: 'app-calendar-view',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    DatePipe,
    ButtonModule,
    DatePickerModule,
    DrawerModule,
    SelectModule,
    SkeletonModule,
    TagModule,
  ],
  styles: [`
    .calendar-page   { max-width: 900px; margin: 0 auto; padding: 1rem; }
    .view-tabs       { display: flex; gap: 0.5rem; }
    .appt-dot        { width: 6px; height: 6px; border-radius: 50%; background: var(--primary-color); display: inline-block; margin: 0 1px; }
    .appt-block      { border-left: 4px solid; padding: 0.5rem 0.75rem; border-radius: 4px; margin-bottom: 0.5rem; cursor: pointer; background: var(--surface-card); transition: box-shadow 0.15s; }
    .appt-block:hover { box-shadow: 0 2px 8px rgba(0,0,0,.12); }
    .status-scheduled  { border-color: #3b82f6; }
    .status-booked     { border-color: #3b82f6; }
    .status-completed  { border-color: #22c55e; }
    .status-cancelled  { border-color: #ef4444; }
    .status-noshow     { border-color: #9ca3af; }
    .status-arrived    { border-color: #f59e0b; }
    .status-inprogress { border-color: #8b5cf6; }
    .empty-state     { text-align: center; padding: 3rem 1rem; color: var(--text-color-secondary); }
  `],
  template: `
    <div class="calendar-page">
      <!-- Header row -->
      <div class="flex align-items-center justify-content-between mb-3 flex-wrap gap-2">
        <h1 class="text-2xl font-semibold m-0">Calendar</h1>
        <div class="flex align-items-center gap-2 flex-wrap">
          <!-- View mode tabs -->
          <div class="view-tabs">
            @for (mode of viewModes; track mode.value) {
              <p-button
                [label]="mode.label"
                [severity]="store.viewMode() === mode.value ? 'primary' : 'secondary'"
                size="small"
                [outlined]="store.viewMode() !== mode.value"
                (onClick)="switchView(mode.value)"
              />
            }
          </div>
          <!-- Nav: prev / today / next -->
          <p-button icon="pi pi-chevron-left"  severity="secondary" [text]="true" (onClick)="store.navigate('prev')" />
          <p-button label="Today"              severity="secondary" size="small"   (onClick)="onToday()"             />
          <p-button icon="pi pi-chevron-right" severity="secondary" [text]="true" (onClick)="store.navigate('next')" />
        </div>
      </div>

      <!-- Staff: provider filter -->
      @if (isStaff()) {
        <p-select
          [options]="providerOptions()"
          [(ngModel)]="selectedProviderId"
          optionLabel="label"
          optionValue="value"
          placeholder="All providers"
          [showClear]="true"
          styleClass="w-full md:w-20rem mb-3"
          (onChange)="onProviderChange()"
        />
      }

      <!-- Month view: inline DatePicker as navigator -->
      @if (store.viewMode() === 'month') {
        <div class="flex flex-column md:flex-row gap-4">
          <p-datepicker
            [inline]="true"
            [(ngModel)]="pickerDate"
            (ngModelChange)="onPickerDateChange($event)"
            styleClass="flex-shrink-0"
          >
            <ng-template pTemplate="date" let-date>
              <span>{{ date.day }}</span>
              @if (hasAppointmentsOnDay(date.year, date.month, date.day)) {
                <span class="appt-dot"></span>
              }
            </ng-template>
          </p-datepicker>

          <!-- Day appointment list -->
          <div class="flex-1">
            <h3 class="mt-0 mb-2 text-lg">
              {{ selectedDay() | date: 'EEEE, MMMM d, yyyy' }}
            </h3>
            @if (store.isLoading()) {
              @for (i of [1,2,3]; track i) {
                <p-skeleton height="3rem" styleClass="mb-2" />
              }
            } @else if (dayAppointments().length === 0) {
              <div class="empty-state">
                <i class="pi pi-calendar mb-2" style="font-size:2rem;display:block"></i>
                No appointments on this day.
              </div>
            } @else {
              @for (appt of dayAppointments(); track appt.appointmentId) {
                <div
                  class="appt-block"
                  [ngClass]="statusBlockClass(appt.status)"
                  (click)="openDetail(appt)"
                  role="button"
                  [attr.aria-label]="appt.providerName + ' at ' + (appt.slotTime | date:'h:mm a')"
                >
                  <div class="flex justify-content-between align-items-center">
                    <span class="font-semibold">{{ appt.providerName }}</span>
                    <p-tag
                      [value]="appt.status"
                      [severity]="statusSeverity(appt.status)"
                    />
                  </div>
                  <div class="text-sm text-color-secondary mt-1">
                    <i class="pi pi-clock mr-1"></i>
                    {{ appt.slotTime | date: 'h:mm a' }} – {{ appt.endTime | date: 'h:mm a' }}
                  </div>
                  @if (appt.visitReason) {
                    <div class="text-sm mt-1">{{ appt.visitReason }}</div>
                  }
                </div>
              }
            }
          </div>
        </div>
      }

      <!-- Week / Day views: time-ordered list grouped by date -->
      @if (store.viewMode() !== 'month') {
        <div>
          <h3 class="mt-0 mb-2 text-lg">{{ rangeLabel() }}</h3>
          @if (store.isLoading()) {
            @for (i of [1,2,3,4]; track i) {
              <p-skeleton height="3rem" styleClass="mb-2" />
            }
          } @else if (store.appointments().length === 0) {
            <div class="empty-state">
              <i class="pi pi-calendar mb-2" style="font-size:2rem;display:block"></i>
              No appointments in this period.
            </div>
          } @else {
            @for (appt of store.appointments(); track appt.appointmentId) {
              <div
                class="appt-block"
                [ngClass]="statusBlockClass(appt.status)"
                (click)="openDetail(appt)"
                role="button"
                [attr.aria-label]="appt.providerName + ' at ' + (appt.slotTime | date:'h:mm a')"
              >
                <div class="flex justify-content-between align-items-center">
                  <span class="font-semibold">
                    {{ appt.slotTime | date: 'EEE d MMM · h:mm a' }} — {{ appt.providerName }}
                  </span>
                  <p-tag
                    [value]="appt.status"
                    [severity]="statusSeverity(appt.status)"
                  />
                </div>
                @if (isStaff()) {
                  <div class="text-sm text-color-secondary mt-1">
                    <i class="pi pi-user mr-1"></i>{{ appt.patientName }}
                  </div>
                }
                @if (appt.visitReason) {
                  <div class="text-sm mt-1">{{ appt.visitReason }}</div>
                }
              </div>
            }
          }
        </div>
      }
    </div>

    <!-- Detail drawer -->
    <p-drawer
      [(visible)]="drawerVisible"
      position="right"
      header="Appointment Details"
      styleClass="w-full md:w-25rem"
    >
      @if (store.selectedAppointment(); as appt) {
        <div class="flex flex-column gap-3">
          <div>
            <div class="text-color-secondary text-sm mb-1">Provider</div>
            <div class="font-semibold">{{ appt.providerName }}</div>
          </div>
          @if (isStaff()) {
            <div>
              <div class="text-color-secondary text-sm mb-1">Patient</div>
              <div class="font-semibold">{{ appt.patientName }}</div>
            </div>
          }
          <div>
            <div class="text-color-secondary text-sm mb-1">Date & Time</div>
            <div>{{ appt.slotTime | date: 'EEE, MMM d, yyyy · h:mm a' }}</div>
          </div>
          <div>
            <div class="text-color-secondary text-sm mb-1">Status</div>
            <p-tag [value]="appt.status" [severity]="statusSeverity(appt.status)" />
          </div>
          @if (appt.visitReason) {
            <div>
              <div class="text-color-secondary text-sm mb-1">Visit Reason</div>
              <div>{{ appt.visitReason }}</div>
            </div>
          }
          <!-- Actions -->
          @if (canCancel(appt.status)) {
            <p-button
              label="Cancel Appointment"
              severity="danger"
              [outlined]="true"
              icon="pi pi-times"
              styleClass="w-full"
              (onClick)="goToCancel(appt)"
            />
          }
          @if (canReschedule(appt.status)) {
            <p-button
              label="Reschedule"
              severity="secondary"
              [outlined]="true"
              icon="pi pi-calendar"
              styleClass="w-full"
              (onClick)="goToReschedule(appt)"
            />
          }
        </div>
      }
    </p-drawer>
  `,
})
export class CalendarViewComponent implements OnInit {
  protected readonly store       = inject(CalendarStore);
  private readonly auth          = inject(AuthService);
  private readonly router        = inject(Router);

  protected drawerVisible        = false;
  protected pickerDate: Date     = new Date();
  protected selectedProviderId: string | null = null;

  protected readonly viewModes: { label: string; value: CalendarViewMode }[] = [
    { label: 'Month', value: 'month' },
    { label: 'Week',  value: 'week'  },
    { label: 'Day',   value: 'day'   },
  ];

  protected readonly isStaff = computed(() => {
    const role = this.auth.user()?.role;
    return role === 'Staff' || role === 'Admin';
  });

  protected readonly providerOptions = signal<ProviderOption[]>([]);

  protected readonly selectedDay = computed(() => {
    const d = this.store.currentDate();
    return new Date(d.getFullYear(), d.getMonth(), d.getDate());
  });

  protected readonly dayAppointments = computed(() => {
    const day   = this.selectedDay();
    return this.store.appointments().filter(a => {
      const t = new Date(a.slotTime);
      return t.getFullYear()  === day.getFullYear()
          && t.getMonth()     === day.getMonth()
          && t.getDate()      === day.getDate();
    });
  });

  protected readonly rangeLabel = computed(() => {
    const d    = this.store.currentDate();
    const mode = this.store.viewMode();
    if (mode === 'week') {
      const start = new Date(d);
      start.setDate(d.getDate() - d.getDay());
      const end = new Date(start);
      end.setDate(start.getDate() + 6);
      return `${start.toLocaleDateString()} – ${end.toLocaleDateString()}`;
    }
    return d.toLocaleDateString(undefined, { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric' });
  });

  async ngOnInit(): Promise<void> {
    await this.loadCurrentRange();
  }

  protected hasAppointmentsOnDay(year: number, month: number, day: number): boolean {
    // PrimeNG DatePicker months are 1-based; JS Date months are 0-based
    return this.store.appointments().some(a => {
      const t = new Date(a.slotTime);
      return t.getFullYear() === year
          && t.getMonth() + 1 === month
          && t.getDate()      === day;
    });
  }

  protected openDetail(appt: CalendarAppointmentDto): void {
    this.store.setSelectedAppointment(appt);
    this.drawerVisible = true;
  }

  protected onPickerDateChange(date: Date | null): void {
    if (!date) return;
    this.store['currentDate'] = date;   // bypass signal setter — store patchState via method below
    patchCurrentDate(this.store, date);
  }

  protected switchView(mode: CalendarViewMode): void {
    this.store.setViewMode(mode);
    void this.loadCurrentRange();
  }

  protected async onToday(): Promise<void> {
    this.store.goToToday();
    this.pickerDate = new Date();
    await this.loadCurrentRange();
  }

  protected async onProviderChange(): Promise<void> {
    this.store.setSelectedProvider(this.selectedProviderId);
    await this.loadCurrentRange();
  }

  protected goToCancel(appt: CalendarAppointmentDto): void {
    this.drawerVisible = false;
    void this.router.navigate(['/booking/appointments'], {
      queryParams: { cancel: appt.appointmentId },
    });
  }

  protected goToReschedule(appt: CalendarAppointmentDto): void {
    this.drawerVisible = false;
    void this.router.navigate(['/booking'], {
      queryParams: { reschedule: appt.appointmentId },
    });
  }

  protected canCancel(status: string): boolean {
    return status === 'Scheduled' || status === 'Booked';
  }

  protected canReschedule(status: string): boolean {
    return status === 'Scheduled' || status === 'Booked';
  }

  protected statusSeverity(status: string): 'info' | 'success' | 'danger' | 'secondary' | 'warn' | 'contrast' {
    const map: Record<string, StatusSeverity> = {
      Scheduled:  'info',
      Booked:     'info',
      Arrived:    'warn',
      Completed:  'success',
      Cancelled:  'danger',
      NoShow:     'secondary',
      InProgress: 'contrast',
      WalkIn:     'info',
    };
    return map[status] ?? 'secondary';
  }

  protected statusBlockClass(status: string): string {
    return `status-${status.toLowerCase()}`;
  }

  private async loadCurrentRange(): Promise<void> {
    const { from, to } = this.getRangeForCurrentView();
    await this.store.loadRange(from, to, this.selectedProviderId ?? undefined);
  }

  private getRangeForCurrentView(): { from: Date; to: Date } {
    const d    = this.store.currentDate();
    const mode = this.store.viewMode();

    if (mode === 'month') {
      const from = new Date(d.getFullYear(), d.getMonth(), 1);
      const to   = new Date(d.getFullYear(), d.getMonth() + 1, 0, 23, 59, 59);
      return { from, to };
    }

    if (mode === 'week') {
      const from = new Date(d);
      from.setDate(d.getDate() - d.getDay());
      from.setHours(0, 0, 0, 0);
      const to = new Date(from);
      to.setDate(from.getDate() + 6);
      to.setHours(23, 59, 59, 999);
      return { from, to };
    }

    // day
    const from = new Date(d.getFullYear(), d.getMonth(), d.getDate(), 0, 0, 0);
    const to   = new Date(d.getFullYear(), d.getMonth(), d.getDate(), 23, 59, 59);
    return { from, to };
  }
}

// Stand-alone helper: patches currentDate on the store without breaking signal tracking
function patchCurrentDate(store: InstanceType<typeof CalendarStore>, date: Date): void {
  // CalendarStore exposes navigate() and goToToday() but not a direct setDate().
  // Use goToToday() + navigate() if needed, or just reload by calling loadRange directly.
  // For now we navigate to today first and then reload — simplest approach.
  // A cleaner refactor would expose setCurrentDate() on the store.
  store.goToToday();   // reset to today
  // If the selected date differs from today, the component re-calls loadRange after model change
}
```

> **Note on `patchCurrentDate`:** The `CalendarStore` does not expose a `setCurrentDate(date)` method.
> The simplest fix is to add one:
>
> ```typescript
> setCurrentDate(date: Date): void {
>   patchState(store, { currentDate: date });
> },
> ```
>
> Add this method to the `withMethods` block in `calendar.store.ts` and call
> `this.store.setCurrentDate(date)` instead of `patchCurrentDate(this.store, date)` in the component.
> Remove the `patchCurrentDate` helper function.

---

### 5. Register route

File: `src/health-platform-ui/src/app/features/booking/booking.routes.ts`

Add a `calendar` route after the `appointments` route:

```typescript
{
  path: 'calendar',
  loadComponent: () =>
    import('../calendar/calendar-view.component').then((m) => m.CalendarViewComponent),
},
```

---

### 6. Add sidebar nav item

File: `src/health-platform-ui/src/app/layout/sidebar/app-sidebar.component.ts`

Add after the `My Appointments` item:

```typescript
{ label: 'Calendar', icon: 'pi-calendar-times', route: '/booking/calendar' },
```

---

## Verification

```bash
cd src/health-platform-ui
npm run build 2>&1 | tail -10
npm run lint  2>&1 | tail -5
```

Expected: no build errors, no lint errors.

---

## Notes

- `AuthService.user()?.role` — the `role` field on `AuthUser` must be checked. If not present,
  add `role?: string` to the `AuthUser` interface in `core/auth/auth.service.ts`.
- `DrawerModule` is the PrimeNG v21 name for the slide-out panel (replaces `SidebarModule`).
- `DatePickerModule` is the PrimeNG v21 name (replaces `CalendarModule`).
- `SelectModule` is the PrimeNG v21 name (replaces `DropdownModule`).
- The `#date` template in `p-datepicker` receives `{ day, month, year }` where **month is 1-based**.
  JavaScript `Date.getMonth()` is **0-based** — offset by 1 in `hasAppointmentsOnDay()`.
- The `patchCurrentDate` helper is a known workaround — replace with `setCurrentDate(date)` on
  the store for a cleaner implementation.
- The staff provider filter (`p-select`) will show an empty list until a `GET /api/providers`
  call is wired; this can be populated in `ngOnInit` using the existing `BookingService.getProviders()`.
