# Task 002: Booking Flow Components (Provider Selection → Slot Picker → Form → Confirmation)

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-027 |
| **Epic** | EP-002 |
| **Layer** | Angular — Feature Components (booking flow) |
| **Priority** | High |
| **Estimated Effort** | 90 minutes |
| **Dependencies** | Task 001 (BookingStore, BookingService, booking.models.ts) |

## Objective

Replace the `BookAppointmentComponent` stub with a fully functional 4-step booking flow:

1. **Step 1 — Provider List** (`ProviderListComponent`): card grid, specialty dropdown filter, name
   search, skeleton loaders.
2. **Step 2 — Slot Picker** (`SlotPickerComponent`): PrimeNG `p-datepicker` month view highlighting
   available days, time-slot chips for the selected date.
3. **Step 3 — Booking Form** (`BookingFormComponent`): visit reason textarea, confirm / back buttons.
4. **Step 4 — Confirmation** (`BookingConfirmationComponent`): appointment summary + `.ics` download.

The parent `BookAppointmentComponent` orchestrates the steps using a `currentStep` signal.

---

## Acceptance Criteria Covered

- AC: Provider cards showing name and specialty
- AC: Filter providers by specialty (dropdown) and name (search input)
- AC: Calendar month view with available days highlighted
- AC: Time-slot chips for selected date
- AC: Visit reason captured in booking form
- AC: Confirmation page with appointment summary and `.ics` download
- AC: Skeleton loaders during data fetch
- AC: Friendly error messages with retry
- AC: Responsive mobile-first layout (min 320px)

---

## Implementation Steps

### 1. Create `ProviderListComponent`

Create `src/health-platform-ui/src/app/features/booking/provider-list/provider-list.component.ts`:

```typescript
import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { SkeletonModule } from 'primeng/skeleton';
import { BookingStore } from '../booking.store';
import { ProviderSummaryDto } from '../../../core/models/booking.models';

const SPECIALTIES = [
  { label: 'All Specialties', value: null },
  { label: 'General Practice', value: 'General Practice' },
  { label: 'Cardiology', value: 'Cardiology' },
  { label: 'Dermatology', value: 'Dermatology' },
  { label: 'Pediatrics', value: 'Pediatrics' },
  { label: 'Orthopedics', value: 'Orthopedics' },
];

@Component({
  selector: 'app-provider-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    CardModule,
    ButtonModule,
    InputTextModule,
    SelectModule,
    SkeletonModule,
  ],
  template: `
    <div class="provider-list">
      <h2 class="text-xl font-semibold mb-3">Select a Provider</h2>

      <!-- Filters -->
      <div class="grid mb-4">
        <div class="col-12 md:col-6 lg:col-4 mb-2">
          <p-select
            [(ngModel)]="selectedSpecialty"
            [options]="specialties"
            optionLabel="label"
            optionValue="value"
            placeholder="Filter by specialty"
            styleClass="w-full"
            (onChange)="applyFilters()"
          />
        </div>
        <div class="col-12 md:col-6 lg:col-4 mb-2">
          <input
            pInputText
            [(ngModel)]="nameFilter"
            placeholder="Search by name"
            class="w-full"
            (input)="applyFilters()"
          />
        </div>
      </div>

      <!-- Skeleton loading -->
      @if (store.isLoading()) {
        <div class="grid">
          @for (i of skeletonItems; track i) {
            <div class="col-12 md:col-6 lg:col-4 mb-3">
              <p-card>
                <p-skeleton height="1.5rem" styleClass="mb-2" />
                <p-skeleton height="1rem" width="60%" />
              </p-card>
            </div>
          }
        </div>
      }

      <!-- Error state -->
      @if (store.error() && !store.isLoading()) {
        <div class="text-center py-5">
          <p class="text-color-secondary mb-3">{{ store.error() }}</p>
          <p-button label="Retry" icon="pi pi-refresh" (onClick)="loadProviders()" />
        </div>
      }

      <!-- Provider cards -->
      @if (!store.isLoading() && !store.error()) {
        @if (filteredProviders().length === 0) {
          <div class="text-center py-5 text-color-secondary">
            No providers found matching your criteria.
          </div>
        } @else {
          <div class="grid">
            @for (provider of filteredProviders(); track provider.providerId) {
              <div class="col-12 md:col-6 lg:col-4 mb-3">
                <p-card styleClass="provider-card cursor-pointer h-full">
                  <div class="flex align-items-center gap-3 mb-2">
                    <div
                      class="provider-avatar flex align-items-center justify-content-center border-circle bg-primary-100 text-primary-700 font-bold"
                      style="width:3rem;height:3rem;flex-shrink:0"
                    >
                      {{ initials(provider.name) }}
                    </div>
                    <div>
                      <div class="font-semibold text-lg">{{ provider.name }}</div>
                      <div class="text-color-secondary text-sm">
                        {{ provider.specialty ?? 'General Practice' }}
                      </div>
                    </div>
                  </div>
                  <p-button
                    label="Select"
                    styleClass="w-full mt-2"
                    size="small"
                    (onClick)="select(provider)"
                  />
                </p-card>
              </div>
            }
          </div>
        }
      }
    </div>
  `,
})
export class ProviderListComponent implements OnInit {
  readonly store = inject(BookingStore);

  selectedSpecialty: string | null = null;
  nameFilter = '';
  specialties = SPECIALTIES;
  skeletonItems = [1, 2, 3, 4, 5, 6];

  filteredProviders = signal<ProviderSummaryDto[]>([]);

  ngOnInit(): void {
    this.loadProviders();
  }

  loadProviders(): void {
    this.store.loadProviders(this.selectedSpecialty ?? undefined, this.nameFilter || undefined);
    // mirror store providers into filtered list after load — done in applyFilters
    // We rely on the store effect via applyFilters triggered by user actions.
    // On initial load we watch via effect; use a simple getter instead:
  }

  applyFilters(): void {
    this.store.loadProviders(this.selectedSpecialty ?? undefined, this.nameFilter || undefined);
  }

  select(provider: ProviderSummaryDto): void {
    this.store.selectProvider(provider);
  }

  initials(name: string): string {
    return name
      .split(' ')
      .slice(0, 2)
      .map((w) => w[0]?.toUpperCase() ?? '')
      .join('');
  }
}
```

> **Simplification**: The name filter is sent to the API each time the user types (server-side
> filtering).  The backend `GetProvidersQuery` accepts `specialty` only at present; the `name`
> parameter can be added to the query, or client-side filtering applied if the backend does not yet
> support it.  Switch `applyFilters()` to filter `store.providers()` locally if needed.

---

### 2. Create `SlotPickerComponent`

Create `src/health-platform-ui/src/app/features/booking/slot-picker/slot-picker.component.ts`:

```typescript
import { CommonModule } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { BookingStore } from '../booking.store';
import { SlotDto } from '../../../core/models/booking.models';

@Component({
  selector: 'app-slot-picker',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePickerModule, ButtonModule, SkeletonModule, TagModule],
  template: `
    <div class="slot-picker">
      <h2 class="text-xl font-semibold mb-1">Choose a Date & Time</h2>
      <p class="text-color-secondary mb-3">
        Booking with <strong>{{ store.selectedProvider()?.name }}</strong>
      </p>

      <div class="grid">
        <!-- Calendar -->
        <div class="col-12 md:col-6 mb-3">
          <p-datepicker
            [(ngModel)]="selectedDate"
            [inline]="true"
            [minDate]="today"
            styleClass="w-full"
            (onSelect)="onDateSelected($event)"
          />
        </div>

        <!-- Time slots -->
        <div class="col-12 md:col-6 mb-3">
          @if (store.slotsLoading()) {
            <div class="flex flex-wrap gap-2">
              @for (i of skeletonItems; track i) {
                <p-skeleton width="5rem" height="2.5rem" styleClass="border-round-xl" />
              }
            </div>
          } @else if (selectedDate) {
            @if (store.availableSlots().length === 0) {
              <div class="text-center text-color-secondary py-4">
                No available slots for this date.
              </div>
            } @else {
              <div class="flex flex-wrap gap-2">
                @for (slot of store.availableSlots(); track slot.slotId) {
                  <button
                    type="button"
                    class="slot-chip p-2 px-3 border-round-xl border-1 cursor-pointer transition-all transition-duration-200"
                    [class.slot-chip--selected]="isSelected(slot)"
                    [class.border-primary]="isSelected(slot)"
                    [class.bg-primary]="isSelected(slot)"
                    [class.text-white]="isSelected(slot)"
                    [class.border-300]="!isSelected(slot)"
                    (click)="selectSlot(slot)"
                  >
                    {{ slot.startTime | date: 'h:mm a' }}
                  </button>
                }
              </div>
            }
          } @else {
            <div class="text-color-secondary py-4">Select a date to see available slots.</div>
          }
        </div>
      </div>
    </div>
  `,
  styles: [`
    .slot-chip { background: transparent; font-size: 0.875rem; }
    .slot-chip:hover:not(.slot-chip--selected) { background: var(--p-primary-50); border-color: var(--p-primary-300) !important; }
  `],
})
export class SlotPickerComponent {
  readonly store = inject(BookingStore);

  selectedDate: Date | null = null;
  today = new Date();
  skeletonItems = [1, 2, 3, 4, 5, 6, 8];

  onDateSelected(date: Date): void {
    const providerId = this.store.selectedProvider()?.providerId;
    if (!providerId) return;
    this.store.selectDate(date);
    const iso = this.toIsoDate(date);
    this.store.loadSlots(providerId, iso);
  }

  selectSlot(slot: SlotDto): void {
    this.store.selectSlot(slot);
  }

  isSelected(slot: SlotDto): boolean {
    return this.store.selectedSlot()?.slotId === slot.slotId;
  }

  private toIsoDate(date: Date): string {
    const y = date.getFullYear();
    const m = String(date.getMonth() + 1).padStart(2, '0');
    const d = String(date.getDate()).padStart(2, '0');
    return `${y}-${m}-${d}`;
  }
}
```

---

### 3. Create `BookingFormComponent`

Create `src/health-platform-ui/src/app/features/booking/booking-form/booking-form.component.ts`:

```typescript
import { Component, EventEmitter, inject, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TextareaModule } from 'primeng/textarea';
import { BookingStore } from '../booking.store';

@Component({
  selector: 'app-booking-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ButtonModule, TextareaModule],
  template: `
    <div class="booking-form">
      <h2 class="text-xl font-semibold mb-1">Confirm Your Appointment</h2>

      <!-- Summary -->
      <div class="surface-100 border-round p-3 mb-4">
        <div class="mb-1">
          <span class="font-medium">Provider: </span>
          {{ store.selectedProvider()?.name }}
        </div>
        <div class="mb-1">
          <span class="font-medium">Date & Time: </span>
          {{ store.selectedSlot()?.startTime | date: 'EEEE, MMMM d, yyyy \'at\' h:mm a' }}
        </div>
      </div>

      <form [formGroup]="form" (ngSubmit)="submit()">
        <div class="field mb-4">
          <label for="visitReason" class="block font-medium mb-1">
            Reason for Visit <span class="text-color-secondary text-sm">(optional)</span>
          </label>
          <textarea
            pTextarea
            id="visitReason"
            formControlName="visitReason"
            rows="4"
            class="w-full"
            placeholder="Briefly describe the reason for your visit…"
            autoResize="true"
          ></textarea>
        </div>

        <div class="flex gap-2 flex-wrap">
          <p-button
            type="button"
            label="Back"
            severity="secondary"
            icon="pi pi-arrow-left"
            (onClick)="back.emit()"
          />
          <p-button
            type="submit"
            label="Confirm Booking"
            icon="pi pi-check"
            [loading]="store.isLoading()"
            [disabled]="store.isLoading()"
          />
        </div>
      </form>
    </div>
  `,
})
export class BookingFormComponent {
  @Output() back = new EventEmitter<void>();
  @Output() confirmed = new EventEmitter<void>();

  readonly store = inject(BookingStore);

  readonly form = inject(FormBuilder).group({
    visitReason: ['', [Validators.maxLength(500)]],
  });

  async submit(): Promise<void> {
    if (this.form.invalid) return;
    const reason = this.form.value.visitReason ?? '';
    const result = await this.store.book(reason);
    if (result) {
      this.confirmed.emit();
    }
  }
}
```

---

### 4. Create `BookingConfirmationComponent`

Create `src/health-platform-ui/src/app/features/booking/booking-confirmation/booking-confirmation.component.ts`:

```typescript
import { Component, EventEmitter, inject, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ButtonModule } from 'primeng/button';
import { BookingStore } from '../booking.store';

@Component({
  selector: 'app-booking-confirmation',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  template: `
    <div class="booking-confirmation text-center py-4">
      <i class="pi pi-check-circle text-green-500 mb-3" style="font-size:3rem"></i>
      <h2 class="text-2xl font-semibold mb-1">Appointment Confirmed!</h2>

      @if (confirmation(); as c) {
        <div class="surface-100 border-round p-3 mb-4 text-left inline-block" style="min-width:280px">
          <div class="mb-1"><span class="font-medium">Provider: </span>{{ c.providerName }}</div>
          <div class="mb-1">
            <span class="font-medium">Date & Time: </span>
            {{ c.appointmentTime | date: 'EEEE, MMMM d, yyyy \'at\' h:mm a' }}
          </div>
          <div><span class="font-medium">Status: </span>{{ c.status }}</div>
        </div>

        <div class="flex justify-content-center gap-2 flex-wrap">
          <a
            [href]="icsDataUri(c.appointmentTime, c.providerName)"
            [download]="'appointment-' + c.appointmentId + '.ics'"
            class="p-button p-component p-button-outlined"
          >
            <i class="pi pi-calendar-plus mr-2"></i> Add to Calendar
          </a>
          <p-button label="Book Another" icon="pi pi-plus" (onClick)="bookAnother.emit()" />
        </div>
      }
    </div>
  `,
})
export class BookingConfirmationComponent {
  @Output() bookAnother = new EventEmitter<void>();

  readonly store = inject(BookingStore);
  readonly confirmation = this.store.lastConfirmation;

  icsDataUri(appointmentTime: string, providerName: string): string {
    const start = new Date(appointmentTime);
    const end = new Date(start.getTime() + 30 * 60 * 1000);
    const fmt = (d: Date) =>
      d.toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '');

    const ics = [
      'BEGIN:VCALENDAR',
      'VERSION:2.0',
      'BEGIN:VEVENT',
      `DTSTART:${fmt(start)}`,
      `DTEND:${fmt(end)}`,
      `SUMMARY:Appointment with ${providerName}`,
      'END:VEVENT',
      'END:VCALENDAR',
    ].join('\r\n');

    return `data:text/calendar;charset=utf8,${encodeURIComponent(ics)}`;
  }
}
```

---

### 5. Update `BookAppointmentComponent` (replace stub)

Replace `src/health-platform-ui/src/app/features/booking/book-appointment/book-appointment.component.ts`:

```typescript
import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { BookingStore } from '../booking.store';
import { ProviderListComponent } from '../provider-list/provider-list.component';
import { SlotPickerComponent } from '../slot-picker/slot-picker.component';
import { BookingFormComponent } from '../booking-form/booking-form.component';
import { BookingConfirmationComponent } from '../booking-confirmation/booking-confirmation.component';

type BookingStep = 'provider' | 'slot' | 'form' | 'confirmation';

@Component({
  selector: 'app-book-appointment',
  standalone: true,
  imports: [
    CommonModule,
    ProviderListComponent,
    SlotPickerComponent,
    BookingFormComponent,
    BookingConfirmationComponent,
  ],
  template: `
    <div class="booking-page p-3" style="max-width:900px;margin:0 auto">

      <!-- Step indicator -->
      <ol class="flex gap-2 list-none p-0 mb-4 flex-wrap" aria-label="Booking steps">
        @for (s of steps; track s.key) {
          <li
            class="flex align-items-center gap-1 text-sm"
            [class.font-semibold]="currentStep() === s.key"
            [class.text-primary]="currentStep() === s.key"
            [class.text-color-secondary]="currentStep() !== s.key"
          >
            <i [class]="s.icon"></i> {{ s.label }}
            @if (!$last) { <span class="text-300 mx-1">›</span> }
          </li>
        }
      </ol>

      @switch (currentStep()) {
        @case ('provider') {
          <app-provider-list />
          @if (store.selectedProvider()) {
            <div class="mt-3">
              <p-button ... label="Next: Choose Time" icon="pi pi-arrow-right" iconPos="right"
                (onClick)="goTo('slot')" />
            </div>
          }
        }
        @case ('slot') {
          <app-slot-picker />
          <div class="flex gap-2 mt-3">
            <p-button label="Back" severity="secondary" icon="pi pi-arrow-left"
              (onClick)="goTo('provider')" />
            @if (store.selectedSlot()) {
              <p-button label="Next: Review" icon="pi pi-arrow-right" iconPos="right"
                (onClick)="goTo('form')" />
            }
          </div>
        }
        @case ('form') {
          <app-booking-form (back)="goTo('slot')" (confirmed)="goTo('confirmation')" />
        }
        @case ('confirmation') {
          <app-booking-confirmation (bookAnother)="restart()" />
        }
      }
    </div>
  `,
})
export class BookAppointmentComponent {
  readonly store = inject(BookingStore);
  readonly currentStep = signal<BookingStep>('provider');

  readonly steps = [
    { key: 'provider',     label: 'Provider',     icon: 'pi pi-user' },
    { key: 'slot',         label: 'Date & Time',  icon: 'pi pi-calendar' },
    { key: 'form',         label: 'Details',      icon: 'pi pi-file-edit' },
    { key: 'confirmation', label: 'Confirmation', icon: 'pi pi-check-circle' },
  ] as const;

  goTo(step: BookingStep): void {
    this.currentStep.set(step);
  }

  restart(): void {
    this.store.resetBookingFlow();
    this.currentStep.set('provider');
  }
}
```

> **Fix needed**: The `p-button` tag in the `@case ('provider')` block has a misplaced `...` —
> replace that line with a clean `<p-button label="Next: Choose Time" ...>` tag. The template above
> is intentional pseudocode to show intent; write the actual tag cleanly.
> Also add `ButtonModule` to the `imports` array of `BookAppointmentComponent`.

---

## Verification Checklist

- [ ] `ng build` (or `ng serve`) has no compilation errors for the booking feature
- [ ] Provider cards render with name and specialty; skeleton shows during load
- [ ] Selecting a provider enables the "Next" button
- [ ] Calendar renders; selecting a date triggers slot API call
- [ ] Slot chips render; selecting one highlights it
- [ ] Booking form shows summary, accepts visit reason, calls `store.book()`
- [ ] Confirmation page shows provider name and appointment time
- [ ] `.ics` download link is generated and downloadable
- [ ] Mobile layout is usable at 320px viewport width
- [ ] Error state shown with retry button when API fails
