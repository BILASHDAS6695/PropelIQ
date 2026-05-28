# Task 001: MultiProviderDayStore + Time-Grid Scaffold

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-039 |
| **Epic** | EP-005 |
| **Layer** | Frontend — ngrx/signals store + Angular component |
| **Priority** | High |
| **Estimated Effort** | 40 minutes |
| **Dependencies** | US-037 complete — `CalendarService` and `CalendarAppointmentDto` available |

## Objective

1. **Add `multi-provider-day.store.ts`** — ngrx/signals store managing selected
   providers, per-provider appointments, per-provider available slots, and
   current date.
2. **Add `multi-provider-day.component.ts`** — staff-only standalone component
   rendering a scrollable time-grid with one column per selected provider (max 5
   visible, horizontal scroll for more). Appointment blocks are positioned
   absolutely within each column. Available slots and blocked slots are visually
   distinguished.
3. **Register route and sidebar nav item.**

---

## Acceptance Criteria Covered

- AC: Day view showing columns for each provider (max 5 side by side)
- AC: Time rows: 15-minute intervals from clinic open (08:00) to close (18:00)
- AC: Appointments displayed as blocks spanning their duration
- AC: Available slots shown as empty/light-coloured cells
- AC: Blocked/unavailable slots shown with hatched pattern
- AC: All providers selected (>5) → horizontal scroll, sticky time column

---

## Design Notes

### Grid geometry (pixel constants)

```
CELL_HEIGHT_PX  = 45    // height of one 15-minute row
PX_PER_MINUTE   = 3     // CELL_HEIGHT_PX / 15
DAY_START_HOUR  = 8     // 08:00
DAY_END_HOUR    = 18    // 18:00
TOTAL_MINUTES   = 600   // (18 - 8) × 60
TIME_COL_WIDTH  = 72    // px, sticky
PROVIDER_COL_MIN_WIDTH = 160  // px
```

### Grid layout (CSS)

- Outer container: `overflow-x: auto` (horizontal scroll when >5 providers)
- Grid: `display: grid; grid-template-columns: 72px repeat(N, minmax(160px, 1fr))`
- Time column: `position: sticky; left: 0; z-index: 2`
- Provider column body: `position: relative; height: 600 * 3 = 1800px`
- Appointment block: `position: absolute; left: 4px; right: 4px; border-radius: 4px`
  - `top`: `(startMinutesFromDayStart) * 3` px
  - `height`: `max(durationMinutes * 3, 18)` px (min 18 px = readable)

### Available vs blocked slots

- The API (`BookingService.getAvailableSlots`) returns `SlotDto[]` with
  `status: 'Available' | 'Booked'` for a given provider + date.
- Available slot cells (`status === 'Available'`): light surface background,
  `cursor: pointer`, hover highlight.
- Booked slots: already covered by the appointment block overlay.
- Blocked (no slot in the response for that interval): repeating-gradient
  hatching: `background: repeating-linear-gradient(45deg, transparent, transparent 4px, var(--surface-200) 4px, var(--surface-200) 8px)`

### Data loading

For each selected provider, on date change:
1. `CalendarService.getAppointments(dayStart, dayEnd, providerId)` → appointments
2. `BookingService.getAvailableSlots(providerId, dateString)` → slots

Both are loaded in `Promise.all` per provider, then merged into store state.

---

## Implementation Steps

### 1. Add `MultiProviderDayStore`

Create `src/health-platform-ui/src/app/features/multi-provider/multi-provider-day.store.ts`:

```typescript
import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { CalendarService } from '../../core/services/calendar.service';
import { BookingService } from '../../core/services/booking.service';
import { ToastService } from '../../shared/services/toast.service';
import { CalendarAppointmentDto } from '../../core/models/calendar.models';
import { ProviderSummaryDto, SlotDto } from '../../core/models/booking.models';

interface MultiProviderDayState {
  currentDate: Date;
  allProviders: ProviderSummaryDto[];
  selectedProviderIds: string[];
  appointmentsByProvider: Record<string, CalendarAppointmentDto[]>;
  slotsByProvider: Record<string, SlotDto[]>;
  isLoading: boolean;
}

const initialState: MultiProviderDayState = {
  currentDate: new Date(),
  allProviders: [],
  selectedProviderIds: [],
  appointmentsByProvider: {},
  slotsByProvider: {},
  isLoading: false,
};

export const MultiProviderDayStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods(
    (
      store,
      calSvc = inject(CalendarService),
      bookSvc = inject(BookingService),
      toast = inject(ToastService),
    ) => ({
      async init(): Promise<void> {
        patchState(store, { isLoading: true });
        try {
          const allProviders = await firstValueFrom(bookSvc.getProviders());
          // Default: first 3 providers selected (or all if fewer)
          const selectedProviderIds = allProviders
            .slice(0, Math.min(3, allProviders.length))
            .map((p) => p.providerId);
          patchState(store, { allProviders, selectedProviderIds, isLoading: false });
          await this.loadForDate(store.currentDate());
        } catch {
          patchState(store, { isLoading: false });
          toast.error('Error', 'Failed to load providers.');
        }
      },

      toggleProvider(providerId: string): void {
        const current = store.selectedProviderIds();
        const next = current.includes(providerId)
          ? current.filter((id) => id !== providerId)
          : [...current, providerId];
        patchState(store, { selectedProviderIds: next });
      },

      async setDate(date: Date): Promise<void> {
        patchState(store, { currentDate: date });
        await this.loadForDate(date);
      },

      navigateDay(direction: 'prev' | 'next'): void {
        const d = new Date(store.currentDate());
        d.setDate(d.getDate() + (direction === 'next' ? 1 : -1));
        patchState(store, { currentDate: d });
        void this.loadForDate(d);
      },

      goToToday(): void {
        const today = new Date();
        patchState(store, { currentDate: today });
        void this.loadForDate(today);
      },

      async loadForDate(date: Date): Promise<void> {
        const providerIds = store.selectedProviderIds();
        if (providerIds.length === 0) return;
        patchState(store, { isLoading: true });

        const dayStart = new Date(date.getFullYear(), date.getMonth(), date.getDate(), 0, 0, 0);
        const dayEnd = new Date(date.getFullYear(), date.getMonth(), date.getDate(), 23, 59, 59);
        const dateStr = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;

        try {
          const results = await Promise.all(
            providerIds.map(async (pid) => {
              const [appointments, slots] = await Promise.all([
                firstValueFrom(calSvc.getAppointments(dayStart, dayEnd, pid)),
                firstValueFrom(bookSvc.getAvailableSlots(pid, dateStr)),
              ]);
              return { pid, appointments, slots };
            }),
          );

          const appointmentsByProvider: Record<string, CalendarAppointmentDto[]> = {};
          const slotsByProvider: Record<string, SlotDto[]> = {};
          for (const { pid, appointments, slots } of results) {
            appointmentsByProvider[pid] = appointments;
            slotsByProvider[pid] = slots;
          }

          patchState(store, { appointmentsByProvider, slotsByProvider, isLoading: false });
        } catch {
          patchState(store, { isLoading: false });
          toast.error('Error', 'Failed to load schedule data.');
        }
      },

      updateAppointmentProvider(
        appointmentId: string,
        fromProviderId: string,
        toProviderId: string,
      ): void {
        const apptsByProvider = { ...store.appointmentsByProvider() };
        const appt = apptsByProvider[fromProviderId]?.find(
          (a) => a.appointmentId === appointmentId,
        );
        if (!appt) return;
        apptsByProvider[fromProviderId] = (apptsByProvider[fromProviderId] ?? []).filter(
          (a) => a.appointmentId !== appointmentId,
        );
        apptsByProvider[toProviderId] = [
          ...(apptsByProvider[toProviderId] ?? []),
          { ...appt, providerId: toProviderId },
        ];
        patchState(store, { appointmentsByProvider: apptsByProvider });
      },
    }),
  ),
);
```

---

### 2. Add `MultiProviderDayComponent`

Create `src/health-platform-ui/src/app/features/multi-provider/multi-provider-day.component.ts`.

**Key constants at top of file:**
```typescript
const DAY_START_HOUR = 8;
const DAY_END_HOUR = 18;
const PX_PER_MINUTE = 3;
const CELL_HEIGHT_PX = 45; // 15 min × 3 px/min
const GRID_HEIGHT_PX = (DAY_END_HOUR - DAY_START_HOUR) * 60 * PX_PER_MINUTE; // 1800
```

**Imports:** `CommonModule, FormsModule, ButtonModule, DatePickerModule, DialogModule,
CheckboxModule, InputTextModule, SkeletonModule, TagModule`

**Template overview:**
```html
<div class="mp-page">
  <!-- Header: date nav + date picker -->
  <div class="mp-header">...</div>

  <!-- Provider selector panel (collapsible) -->
  <div class="mp-provider-selector">...</div>

  <!-- Time grid -->
  <div class="mp-grid-wrapper" #gridWrapper>
    <div class="mp-grid" [style.grid-template-columns]="gridTemplateColumns()">
      <!-- Header row: empty time cell + one header cell per provider -->
      <div class="time-header-cell"></div>
      @for (p of selectedProviders(); track p.providerId) {
        <div class="provider-header-cell">
          <span class="font-semibold text-sm">{{ p.name }}</span>
          <span class="text-xs text-color-secondary">{{ p.specialty }}</span>
        </div>
      }

      <!-- Time column + provider columns body -->
      <div class="time-col">
        @for (label of timeLabels; track label) {
          <div class="time-cell">{{ label }}</div>
        }
      </div>

      @for (p of selectedProviders(); track p.providerId) {
        <div class="provider-col"
             [attr.data-provider-id]="p.providerId"
             (dragover)="onDragOver($event)"
             (drop)="onDrop($event, p.providerId)">
          <!-- Available/blocked slot background cells -->
          @for (slot of timeSlots; track slot.minutes) {
            <div
              class="slot-cell"
              [class.slot-available]="isSlotAvailable(p.providerId, slot.minutes)"
              [class.slot-blocked]="!isSlotAvailable(p.providerId, slot.minutes)"
              [style.top.px]="slot.minutes * PX_PER_MINUTE"
              (click)="onSlotClick(p.providerId, slot.minutes)"
            ></div>
          }

          <!-- Appointment blocks -->
          @for (appt of appointmentsFor(p.providerId); track appt.appointmentId) {
            <div
              class="appt-block"
              [class]="apptBlockClass(appt.status)"
              [style.top.px]="apptTop(appt)"
              [style.height.px]="apptHeight(appt)"
              draggable="true"
              (dragstart)="onDragStart($event, appt, p.providerId)"
              (click)="$event.stopPropagation()"
              role="button"
              [attr.tabindex]="0"
              [attr.aria-label]="appt.providerName + ' ' + (appt.slotTime | date: 'h:mm a')"
            >
              <div class="appt-title">{{ appt.patientName }}</div>
              <div class="appt-time text-xs">
                {{ appt.slotTime | date: 'h:mm' }}–{{ appt.endTime | date: 'h:mm a' }}
              </div>
            </div>
          }
        </div>
      }
    </div>
  </div>

  <!-- Quick-book dialog (Task 002) -->
  <!-- Drag-error toast feedback (Task 003) -->
</div>
```

**Key computed signals and helpers in the class:**
```typescript
readonly selectedProviders = computed(() =>
  store.allProviders().filter((p) => store.selectedProviderIds().includes(p.providerId))
);

readonly gridTemplateColumns = computed(() =>
  `72px repeat(${this.selectedProviders().length}, minmax(160px, 1fr))`
);

// 40 rows × 15 min = 600 min (08:00–18:00)
readonly timeLabels = Array.from({ length: 40 }, (_, i) => {
  const totalMin = DAY_START_HOUR * 60 + i * 15;
  const h = Math.floor(totalMin / 60);
  const m = totalMin % 60;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
});

// 40 slot objects (minutes from day-start)
readonly timeSlots = Array.from({ length: 40 }, (_, i) => ({ minutes: i * 15 }));

appointmentsFor(providerId: string): CalendarAppointmentDto[] {
  return this.store.appointmentsByProvider()[providerId] ?? [];
}

apptTop(appt: CalendarAppointmentDto): number {
  const start = new Date(appt.slotTime);
  const minFromDayStart = (start.getHours() - DAY_START_HOUR) * 60 + start.getMinutes();
  return minFromDayStart * PX_PER_MINUTE;
}

apptHeight(appt: CalendarAppointmentDto): number {
  const duration =
    (new Date(appt.endTime).getTime() - new Date(appt.slotTime).getTime()) / 60000;
  return Math.max(duration * PX_PER_MINUTE, 18);
}

isSlotAvailable(providerId: string, minutesFromDayStart: number): boolean {
  const slots = this.store.slotsByProvider()[providerId] ?? [];
  return slots.some((s) => {
    const slotDate = new Date(s.startTime);
    const slotMin = (slotDate.getHours() - DAY_START_HOUR) * 60 + slotDate.getMinutes();
    return slotMin === minutesFromDayStart && s.status === 'Available';
  });
}
```

---

### 3. Register route + sidebar nav item

**`booking.routes.ts`** — add:
```typescript
{
  path: 'staff-schedule',
  loadComponent: () =>
    import('../multi-provider/multi-provider-day.component').then(
      (m) => m.MultiProviderDayComponent,
    ),
},
```

**`app-sidebar.component.ts`** — add after Calendar:
```typescript
{ label: 'Staff Schedule', icon: 'pi-th-large', route: '/booking/staff-schedule' },
```

---

## Verification

```bash
cd src/health-platform-ui
npx ng build
npx ng lint
```

Expected: build clean, lint clean. (Tests are added in Task 003.)
