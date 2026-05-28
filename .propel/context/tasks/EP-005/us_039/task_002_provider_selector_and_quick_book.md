# Task 002: Provider Selector Panel & Quick-Book Dialog

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-039 |
| **Epic** | EP-005 |
| **Layer** | Frontend — Angular component additions |
| **Priority** | High |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | Task 001 complete — `MultiProviderDayComponent` scaffold + store in place |

## Objective

1. **Provider selector panel** — collapsible panel listing all providers with
   PrimeNG `p-checkbox` toggles. Shows a warning badge when >5 are active
   (horizontal scroll kicks in).
2. **Quick-book dialog** — `p-dialog` that opens when a staff member clicks an
   available slot. Captures patient name + visit reason; on confirm calls
   `BookingService.bookAppointment(slotId, visitReason)` then refreshes
   the current date's data.
3. **"Not Available" column state** — when a provider has no slots or appointments
   for the selected date, the column body shows a centred "Not Available" message
   instead of a blank grid.

---

## Acceptance Criteria Covered

- AC: Provider selector: checkboxes to include/exclude providers from view
- AC: Click empty slot → quick-book dialog (select patient, create appointment)
- AC: Provider with no schedule for selected date → column shows "Not Available"
- AC: All providers selected (>5) → warning displayed (grid already scrolls from Task 001)

---

## Design Notes

- **Provider selector**: show/hide via `selectorExpanded` signal (default `true`).
  Disabled checkbox if un-checking would leave 0 selected.
- **Quick-book dialog state** (local signals on the component, not in the store):
  ```typescript
  quickBookVisible = signal(false);
  quickBookProviderId = signal<string | null>(null);
  quickBookSlotMinutes = signal<number>(0);  // minutes from day-start
  quickBookPatientName = signal('');
  quickBookVisitReason = signal('');
  quickBookLoading = signal(false);
  ```
- **Finding the slot ID** for `bookAppointment`: match the slot in
  `store.slotsByProvider()[providerId]` where:
  ```
  (slotDate.getHours() - 8) * 60 + slotDate.getMinutes() === quickBookSlotMinutes
  && slot.status === 'Available'
  ```
  If no matching slot ID is found, show a toast error and close the dialog.
- **After successful booking**: call `store.loadForDate(store.currentDate())` to
  refresh all columns.

---

## Implementation Steps

### 1. Add provider selector panel to `MultiProviderDayComponent`

Insert inside `.mp-page`, between the header and the grid wrapper.

```html
<!-- Provider selector -->
<div class="mp-selector surface-card border-round p-3 mb-3">
  <div
    class="flex align-items-center justify-content-between cursor-pointer"
    (click)="selectorExpanded.set(!selectorExpanded())"
    role="button"
    tabindex="0"
    (keydown.enter)="selectorExpanded.set(!selectorExpanded())"
    aria-label="Toggle provider selector"
  >
    <span class="font-semibold">
      Providers
      <span class="text-color-secondary text-sm ml-1">
        ({{ store.selectedProviderIds().length }} selected)
      </span>
    </span>
    @if (store.selectedProviderIds().length > 5) {
      <span class="text-xs text-orange-500 font-medium mr-2">
        <i class="pi pi-exclamation-triangle mr-1"></i>Scroll to see all columns
      </span>
    }
    <i [class]="'pi ' + (selectorExpanded() ? 'pi-chevron-up' : 'pi-chevron-down')"></i>
  </div>

  @if (selectorExpanded()) {
    <div class="flex flex-wrap gap-3 mt-3">
      @for (p of store.allProviders(); track p.providerId) {
        <div class="flex align-items-center gap-2">
          <p-checkbox
            [ngModel]="store.selectedProviderIds().includes(p.providerId)"
            (ngModelChange)="onProviderToggle(p.providerId)"
            [binary]="true"
            [inputId]="'prov-' + p.providerId"
            [disabled]="
              store.selectedProviderIds().includes(p.providerId) &&
              store.selectedProviderIds().length === 1
            "
          />
          <label [for]="'prov-' + p.providerId" class="cursor-pointer text-sm">
            {{ p.name }}
            @if (p.specialty) {
              <span class="text-color-secondary ml-1">({{ p.specialty }})</span>
            }
          </label>
        </div>
      }
    </div>
  }
</div>
```

Add to component imports: `CheckboxModule` from `'primeng/checkbox'`.

Add to component class:
```typescript
selectorExpanded = signal(true);

onProviderToggle(providerId: string): void {
  this.store.toggleProvider(providerId);
  void this.store.loadForDate(this.store.currentDate());
}
```

---

### 2. Add "Not Available" column state

In the provider column body (`<div class="provider-col">`), add a conditional
before the slot cells:

```html
@if (!hasSchedule(p.providerId)) {
  <div
    class="flex align-items-center justify-content-center text-color-secondary text-sm"
    style="height: 100%; position: absolute; inset: 0"
  >
    <div class="text-center">
      <i class="pi pi-ban mb-2" style="font-size: 1.5rem; display: block"></i>
      Not Available
    </div>
  </div>
}
```

Add helper to component class:
```typescript
hasSchedule(providerId: string): boolean {
  const appts = this.store.appointmentsByProvider()[providerId] ?? [];
  const slots = this.store.slotsByProvider()[providerId] ?? [];
  return appts.length > 0 || slots.length > 0;
}
```

---

### 3. Add Quick-book dialog

Add PrimeNG `DialogModule` and `InputTextModule` to component imports.

**Template** (add after the grid wrapper):
```html
<p-dialog
  [visible]="quickBookVisible()"
  (visibleChange)="quickBookVisible.set($event)"
  header="Quick Book Appointment"
  [modal]="true"
  [draggable]="false"
  [resizable]="false"
  [style]="{ width: '380px' }"
>
  @if (quickBookSlotLabel(); as label) {
    <p class="mb-3 text-color-secondary text-sm">
      <i class="pi pi-clock mr-1"></i>{{ label }}
    </p>
  }
  <div class="field mb-3">
    <label for="qbPatient" class="block font-medium mb-1 text-sm">Patient Name</label>
    <input
      pInputText
      id="qbPatient"
      [ngModel]="quickBookPatientName()"
      (ngModelChange)="quickBookPatientName.set($event)"
      placeholder="Full patient name"
      class="w-full"
    />
  </div>
  <div class="field mb-3">
    <label for="qbReason" class="block font-medium mb-1 text-sm">Visit Reason</label>
    <input
      pInputText
      id="qbReason"
      [ngModel]="quickBookVisitReason()"
      (ngModelChange)="quickBookVisitReason.set($event)"
      placeholder="e.g. Annual checkup"
      class="w-full"
    />
  </div>
  <ng-template pTemplate="footer">
    <p-button
      label="Cancel"
      severity="secondary"
      [outlined]="true"
      (onClick)="closeQuickBook()"
    />
    <p-button
      label="Book"
      icon="pi pi-check"
      [loading]="quickBookLoading()"
      [disabled]="!quickBookPatientName().trim()"
      (onClick)="confirmQuickBook()"
    />
  </ng-template>
</p-dialog>
```

**Component class additions:**
```typescript
import { BookingService } from '../../core/services/booking.service';
// ...
private readonly bookSvc = inject(BookingService);

quickBookVisible = signal(false);
quickBookProviderId = signal<string | null>(null);
quickBookSlotMinutes = signal(0);
quickBookPatientName = signal('');
quickBookVisitReason = signal('');
quickBookLoading = signal(false);

readonly quickBookSlotLabel = computed(() => {
  const mins = this.quickBookSlotMinutes();
  const total = DAY_START_HOUR * 60 + mins;
  const h = Math.floor(total / 60);
  const m = total % 60;
  const timeStr = `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
  const prov = this.store
    .allProviders()
    .find((p) => p.providerId === this.quickBookProviderId());
  return prov ? `${timeStr} with ${prov.name}` : timeStr;
});

onSlotClick(providerId: string, minutesFromDayStart: number): void {
  if (!this.isSlotAvailable(providerId, minutesFromDayStart)) return;
  this.quickBookProviderId.set(providerId);
  this.quickBookSlotMinutes.set(minutesFromDayStart);
  this.quickBookPatientName.set('');
  this.quickBookVisitReason.set('');
  this.quickBookVisible.set(true);
}

closeQuickBook(): void {
  this.quickBookVisible.set(false);
}

async confirmQuickBook(): Promise<void> {
  const providerId = this.quickBookProviderId();
  const mins = this.quickBookSlotMinutes();
  if (!providerId) return;

  const slot = (this.store.slotsByProvider()[providerId] ?? []).find((s) => {
    const d = new Date(s.startTime);
    return (d.getHours() - DAY_START_HOUR) * 60 + d.getMinutes() === mins
      && s.status === 'Available';
  });

  if (!slot) {
    this.toast.error('Unavailable', 'This slot is no longer available.');
    this.closeQuickBook();
    return;
  }

  this.quickBookLoading.set(true);
  try {
    await firstValueFrom(
      this.bookSvc.bookAppointment(slot.slotId, this.quickBookVisitReason() || 'Walk-in'),
    );
    this.toast.success('Booked', 'Appointment created successfully.');
    this.closeQuickBook();
    await this.store.loadForDate(this.store.currentDate());
  } catch {
    this.toast.error('Error', 'Could not create the appointment.');
  } finally {
    this.quickBookLoading.set(false);
  }
}
```

Note: `toast` must be injected: `private readonly toast = inject(ToastService)`.

---

## Verification

```bash
cd src/health-platform-ui
npx ng build
npx ng lint
```

Expected: build clean, lint clean.
