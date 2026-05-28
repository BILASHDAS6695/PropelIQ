# Task 003: Angular Calendar — Store Refinements, Responsive List View & Unit Tests

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-037 |
| **Epic** | EP-005 |
| **Layer** | Frontend (Angular 21 + PrimeNG v21) + Tests |
| **Priority** | High |
| **Estimated Effort** | 40 minutes |
| **Dependencies** | Task 002 complete — `CalendarViewComponent` renders and routes correctly |

## Objective

1. **Refine `CalendarStore`** — add `setCurrentDate(date)` method to eliminate
   the `patchCurrentDate` workaround from Task 002.
2. **Populate staff provider filter** — wire `BookingService.getProviders()` in
   `ngOnInit` so the `p-select` has options.
3. **Add responsive list view** — on mobile (`max-width: 768px`) replace the month
   calendar with a compact date-scrollable list; implement via a media query signal.
4. **Auto-reload on navigation** — call `loadCurrentRange()` whenever `currentDate`
   or `viewMode` changes (use Angular `effect()` instead of manually calling in
   every mutator).
5. **Add unit tests** covering `CalendarStore` methods and `CalendarViewComponent`
   rendering.
6. **Build + lint clean** — target 60/60 total tests (58 from Task 001 + 2 new).

---

## Acceptance Criteria Covered

- AC: Navigate between months/weeks with arrow buttons → auto-reload
- AC: Today button to quickly return to current date
- AC: Responsive: month view on desktop, list view on mobile
- AC: Month with no appointments → empty calendar, "No appointments" indicator
- AC: Day with >5 appointments (staff view) → show count badge, expand on click

---

## Implementation Steps

### 1. Refine `CalendarStore` — add `setCurrentDate`

File: `src/health-platform-ui/src/app/features/calendar/calendar.store.ts`

Add the following method inside `withMethods(...)` after `goToToday`:

```typescript
setCurrentDate(date: Date): void {
  patchState(store, { currentDate: date });
},
```

---

### 2. Fix component to use `setCurrentDate`

File: `src/health-platform-ui/src/app/features/calendar/calendar-view.component.ts`

Replace the `onPickerDateChange` method and remove the `patchCurrentDate` helper:

```typescript
protected onPickerDateChange(date: Date | null): void {
  if (!date) return;
  this.store.setCurrentDate(date);
  void this.loadCurrentRange();
}
```

Remove the `patchCurrentDate` standalone function at the bottom of the file entirely.

---

### 3. Populate staff provider dropdown in `ngOnInit`

File: `src/health-platform-ui/src/app/features/calendar/calendar-view.component.ts`

Add `BookingService` injection and populate provider options:

```typescript
private readonly bookingSvc = inject(BookingService);
```

Update `ngOnInit`:

```typescript
async ngOnInit(): Promise<void> {
  if (this.isStaff()) {
    const providers = await firstValueFrom(this.bookingSvc.getProviders());
    this.providerOptions.set(
      providers.map(p => ({ label: p.name, value: p.providerId })),
    );
  }
  await this.loadCurrentRange();
}
```

Add to imports at top of file:

```typescript
import { firstValueFrom } from 'rxjs';
import { BookingService } from '../../core/services/booking.service';
```

---

### 4. Add responsive breakpoint signal

File: `src/health-platform-ui/src/app/features/calendar/calendar-view.component.ts`

Add after the `router` injection:

```typescript
private readonly isMobile = signal(window.innerWidth <= 768);
```

Add a `HostListener` to track resize:

```typescript
import { HostListener } from '@angular/core';

@HostListener('window:resize')
onResize(): void {
  this.isMobile.set(window.innerWidth <= 768);
}
```

In the template, replace the month-view block condition:

```html
@if (store.viewMode() === 'month' && !isMobile()) {
  <!-- existing DatePicker inline block -->
}

@if (store.viewMode() === 'month' && isMobile()) {
  <!-- compact list: same as week/day view but grouped by date heading -->
  <div>
    @for (group of appointmentsByDay(); track group.date) {
      <div class="mb-3">
        <h4 class="mt-0 mb-2 text-base font-semibold">
          {{ group.date | date: 'EEE, MMM d' }}
        </h4>
        @for (appt of group.items; track appt.appointmentId) {
          <div class="appt-block" [ngClass]="statusBlockClass(appt.status)" (click)="openDetail(appt)">
            <div class="flex justify-content-between align-items-center">
              <span class="font-semibold">{{ appt.slotTime | date: 'h:mm a' }} — {{ appt.providerName }}</span>
              <p-tag [value]="appt.status" [severity]="statusSeverity(appt.status)" />
            </div>
          </div>
        }
      </div>
    }
    @if (store.appointments().length === 0 && !store.isLoading()) {
      <div class="empty-state">
        <i class="pi pi-calendar mb-2" style="font-size:2rem;display:block"></i>
        No appointments this month.
      </div>
    }
  </div>
}
```

Add the `appointmentsByDay` computed signal to the component class:

```typescript
protected readonly appointmentsByDay = computed(() => {
  const groups = new Map<string, CalendarAppointmentDto[]>();
  for (const appt of this.store.appointments()) {
    const key = new Date(appt.slotTime).toDateString();
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key)!.push(appt);
  }
  return [...groups.entries()].map(([date, items]) => ({ date: new Date(date), items }));
});
```

---

### 5. Day-overflow badge (staff view: >5 appointments per day)

In the `#date` template inside `p-datepicker`, update to show a count badge when a
day has more than 5 appointments:

```html
<ng-template pTemplate="date" let-date>
  <span>{{ date.day }}</span>
  @if (dayAppointmentCount(date.year, date.month, date.day) as count) {
    @if (count > 0 && count <= 5) {
      <span class="appt-dot"></span>
    } @else if (count > 5) {
      <span
        class="text-xs font-bold"
        style="color: var(--primary-color); display:block; line-height:1"
      >{{ count }}</span>
    }
  }
</ng-template>
```

Add a helper to the component class:

```typescript
protected dayAppointmentCount(year: number, month: number, day: number): number {
  return this.store.appointments().filter(a => {
    const t = new Date(a.slotTime);
    return t.getFullYear() === year
        && t.getMonth() + 1 === month
        && t.getDate()      === day;
  }).length;
}
```

---

### 6. Add unit tests

Create `src/health-platform-ui/src/app/features/calendar/calendar-view.component.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { CalendarViewComponent } from './calendar-view.component';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';

describe('CalendarViewComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CalendarViewComponent],
      providers: [provideHttpClient(), provideRouter([])],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(CalendarViewComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
```

Create `src/health-platform-ui/src/app/features/calendar/calendar.store.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { CalendarStore } from './calendar.store';

describe('CalendarStore', () => {
  let store: InstanceType<typeof CalendarStore>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CalendarStore, provideHttpClient()],
    });
    store = TestBed.inject(CalendarStore);
  });

  it('should initialise with month view and today', () => {
    expect(store.viewMode()).toBe('month');
    const today = new Date();
    expect(store.currentDate().getDate()).toBe(today.getDate());
  });

  it('navigate(next) advances by one month in month view', () => {
    const before = store.currentDate().getMonth();
    store.navigate('next');
    expect(store.currentDate().getMonth()).toBe((before + 1) % 12);
  });

  it('navigate(prev) retreats by one month in month view', () => {
    const before = store.currentDate().getMonth();
    store.navigate('prev');
    const expected = before === 0 ? 11 : before - 1;
    expect(store.currentDate().getMonth()).toBe(expected);
  });

  it('goToToday resets currentDate to today', () => {
    store.navigate('next');
    store.goToToday();
    const today = new Date();
    expect(store.currentDate().getDate()).toBe(today.getDate());
    expect(store.currentDate().getMonth()).toBe(today.getMonth());
  });

  it('setViewMode changes the view mode', () => {
    store.setViewMode('week');
    expect(store.viewMode()).toBe('week');
  });
});
```

---

### 7. Backend test count bump

The 2 backend tests added in Task 001 bring the total to **58**.
Angular unit tests are run via `npm test` (vitest), not `dotnet test` — they are tracked separately.

---

## Verification

```bash
# Backend — confirm 58/58
cd src
dotnet test HealthPlatform.Tests/HealthPlatform.Tests.csproj -v q 2>&1 | Select-String "passed|failed" | Select-Object -Last 3

# Frontend — confirm build + lint clean
cd src/health-platform-ui
npm run build 2>&1 | tail -10
npm run lint  2>&1 | tail -5
npm test -- --run 2>&1 | tail -10
```

Expected:
- `dotnet test`: `Passed! - Failed: 0, Passed: 58, …`
- `npm run build`: `Build at: … - Hash: …`
- `npm test`: all Angular specs pass

---

## Notes

- `HostListener('window:resize')` is straightforward but not SSR-safe; since this app is CSR-only
  it is acceptable. Alternatively use `BreakpointObserver` from `@angular/cdk/layout` if the CDK
  is ever added.
- `window.innerWidth` in the constructor/field initializer will work in a browser context
  but throws in SSR. Wrap in `isPlatformBrowser()` if needed.
- The Angular specs use `vitest` (see `package.json`) — `npm test -- --run` runs them once
  (no watch mode).
- `CalendarStore` is `providedIn: 'root'` so `TestBed.inject(CalendarStore)` works without
  explicit provider registration in the test (the `providers: [CalendarStore, ...]` override
  ensures a fresh instance per test suite).
