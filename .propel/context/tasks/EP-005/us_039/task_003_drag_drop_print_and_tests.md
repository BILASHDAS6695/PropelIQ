# Task 003: Drag-Drop Reassignment, Print Layout & Unit Tests

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-039 |
| **Epic** | EP-005 |
| **Layer** | Frontend — Angular component + CSS + tests |
| **Priority** | High |
| **Estimated Effort** | 35 minutes |
| **Dependencies** | Tasks 001 + 002 complete — grid, store, provider selector, quick-book dialog all in place |

## Objective

1. **HTML5 drag-and-drop reassignment** — appointment blocks are `draggable`. Dropping
   on a different provider column calls `BookingService.rescheduleAppointment` with the
   matching slot in the target provider. Snap-back + error toast on blocked target.
2. **Print-friendly layout** — `@media print` CSS hides controls and renders a clean
   single-page grid.
3. **Unit tests** — `multi-provider-day.store.spec.ts` with 4 tests covering store
   state, navigation, and `updateAppointmentProvider`.

---

## Acceptance Criteria Covered

- AC: Drag appointment between providers (staff only) for reassignment
- AC: Drag appointment to blocked slot → snap back with error tooltip
- AC: Print-friendly layout for daily schedule printout

---

## Design Notes

### Drag-and-drop approach (native HTML5, no CDK required)

Uses three native DOM events:
- `dragstart` — stores the dragged appointment ID + source provider ID in `DragEvent.dataTransfer`
- `dragover` — calls `event.preventDefault()` to allow drop
- `drop` — reads the stored IDs, determines the target provider, finds the matching
  time slot, calls `rescheduleAppointment`

**Finding the target slot**: The drop target is the provider column (`data-provider-id`).
The `drop` event `offsetY` gives the pixel position within the column. Convert to minutes:
```
minutesFromDayStart = Math.floor(offsetY / PX_PER_MINUTE / 15) * 15  // snap to 15-min grid
```
Then find a slot for the target provider at that time.

**Snap-back**: On failure (blocked / no slot), optimistically updating the UI is NOT done.
The drag operation simply fails with a toast error. The appointment stays in its original
column (no UI mutation on error path).

**Optimistic update**: On success, call
`store.updateAppointmentProvider(appointmentId, fromProviderId, toProviderId)` immediately
for responsive feedback, then `store.loadForDate(...)` to re-sync with the server.

### Print CSS

```css
@media print {
  .mp-header,
  .mp-selector,
  .mp-actions { display: none !important; }
  .mp-grid-wrapper { overflow: visible !important; }
  .mp-grid { grid-template-columns: 72px repeat(auto-fill, minmax(120px, 1fr)) !important; }
  body { font-size: 10pt; }
}
```

---

## Implementation Steps

### 1. Drag-and-drop handlers in `MultiProviderDayComponent`

**Class additions:**

```typescript
private dragAppointmentId = '';
private dragFromProviderId = '';

onDragStart(
  event: DragEvent,
  appt: CalendarAppointmentDto,
  fromProviderId: string,
): void {
  this.dragAppointmentId = appt.appointmentId;
  this.dragFromProviderId = fromProviderId;
  event.dataTransfer?.setData('text/plain', appt.appointmentId);
}

onDragOver(event: DragEvent): void {
  event.preventDefault();
}

async onDrop(event: DragEvent, toProviderId: string): Promise<void> {
  event.preventDefault();
  if (
    !this.dragAppointmentId ||
    !this.dragFromProviderId ||
    this.dragFromProviderId === toProviderId
  ) {
    return;
  }

  const offsetY = event.offsetY;
  const snappedMinutes = Math.floor(offsetY / PX_PER_MINUTE / 15) * 15;

  const targetSlot = (this.store.slotsByProvider()[toProviderId] ?? []).find((s) => {
    const d = new Date(s.startTime);
    const slotMin = (d.getHours() - DAY_START_HOUR) * 60 + d.getMinutes();
    return slotMin === snappedMinutes && s.status === 'Available';
  });

  if (!targetSlot) {
    this.toast.error('Blocked', 'Cannot reassign: the target slot is not available.');
    this.dragAppointmentId = '';
    this.dragFromProviderId = '';
    return;
  }

  try {
    await firstValueFrom(
      this.bookSvc.rescheduleAppointment(this.dragAppointmentId, targetSlot.slotId),
    );
    this.store.updateAppointmentProvider(
      this.dragAppointmentId,
      this.dragFromProviderId,
      toProviderId,
    );
    await this.store.loadForDate(this.store.currentDate());
    this.toast.success('Reassigned', 'Appointment moved successfully.');
  } catch {
    this.toast.error('Error', 'Could not reassign the appointment.');
  } finally {
    this.dragAppointmentId = '';
    this.dragFromProviderId = '';
  }
}
```

Note: `bookSvc` is already injected in Task 002 for `confirmQuickBook()`.

---

### 2. Print button + print CSS

**Print button** — add to the header row in the template:

```html
<p-button
  label="Print"
  icon="pi pi-print"
  severity="secondary"
  size="small"
  [outlined]="true"
  class="mp-actions"
  (onClick)="window.print()"
/>
```

Expose `window` in the component class:
```typescript
protected readonly window = window;
```

**Print CSS** — add inside the component's `styles` array:

```css
@media print {
  .mp-header,
  .mp-selector,
  .mp-actions { display: none !important; }
  .mp-grid-wrapper { overflow: visible !important; }
  .appt-block { box-shadow: none !important; cursor: default; }
  .slot-blocked { background: #e5e7eb !important; }
}
```

---

### 3. Add unit tests

Create `src/health-platform-ui/src/app/features/multi-provider/multi-provider-day.store.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { MessageService } from 'primeng/api';
import { MultiProviderDayStore } from './multi-provider-day.store';

describe('MultiProviderDayStore', () => {
  let store: InstanceType<typeof MultiProviderDayStore>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [MultiProviderDayStore, provideHttpClient(), MessageService],
    });
    store = TestBed.inject(MultiProviderDayStore);
  });

  it('should have correct initial state', () => {
    expect(store.selectedProviderIds()).toEqual([]);
    expect(store.allProviders()).toEqual([]);
    expect(store.isLoading()).toBe(false);
  });

  it('toggleProvider: adds provider when not selected', () => {
    store.toggleProvider('prov-001');
    expect(store.selectedProviderIds()).toContain('prov-001');
  });

  it('toggleProvider: removes provider when already selected', () => {
    store.toggleProvider('prov-001');
    store.toggleProvider('prov-001');
    expect(store.selectedProviderIds()).not.toContain('prov-001');
  });

  it('navigateDay: advances currentDate by 1 when direction is next', () => {
    const before = store.currentDate().getDate();
    store.navigateDay('next');
    expect(store.currentDate().getDate()).toBe(before + 1);
  });
});
```

---

### 4. Add smoke test for `MultiProviderDayComponent`

Create `src/health-platform-ui/src/app/features/multi-provider/multi-provider-day.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { MultiProviderDayComponent } from './multi-provider-day.component';

describe('MultiProviderDayComponent', () => {
  let fixture: ComponentFixture<MultiProviderDayComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MultiProviderDayComponent],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(MultiProviderDayComponent);
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

Expected: all prior tests pass + 4 store tests + 1 component smoke test.

```bash
npx ng build
npx ng lint
```

Expected: build clean, `All files pass linting.`

### Manual smoke-test checklist

| Step | Expected |
|------|----------|
| Navigate to `/booking/staff-schedule` (staff login) | Grid renders with time column + provider columns |
| Toggle provider checkboxes | Columns update; warning shows if >5 active |
| Click available (light) slot | Quick-book dialog opens |
| Fill patient name + reason → Book | Appointment appears in column; toast success |
| Click blocked (hatched) slot | Nothing happens |
| Drag appointment to another provider's available time | Appointment moves; toast success |
| Drag appointment to blocked slot | Toast error; appointment stays in original column |
| Ctrl+P / Print button | Header and selectors hidden; clean grid layout |
