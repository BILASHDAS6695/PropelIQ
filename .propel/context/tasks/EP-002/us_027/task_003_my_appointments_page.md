# Task 003: My Appointments Page

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-027 |
| **Epic** | EP-002 |
| **Layer** | Angular — Feature Components (my-appointments page) |
| **Priority** | High |
| **Estimated Effort** | 60 minutes |
| **Dependencies** | Task 001 (BookingStore, AppointmentItemDto) |

## Objective

Replace the `MyAppointmentsComponent` stub with a fully functional page that shows a patient's
appointments split into **Upcoming** and **Past** tabs. Each appointment shows status badge, provider
name, date/time, and action buttons (cancel for upcoming, no action for past).

Two deliverables:

1. **`AppointmentCardComponent`** — reusable card for a single appointment.
2. **`MyAppointmentsComponent`** — page container with tabs, skeleton loading, empty states,
   and cancel confirmation dialog.

---

## Acceptance Criteria Covered

- AC: Patient can view all their appointments (upcoming + past)
- AC: Status badges for each appointment
- AC: Cancel button for upcoming appointments
- AC: Confirmation dialog before cancelling
- AC: Skeleton loading while fetching
- AC: Empty state messages when no appointments exist
- AC: Responsive mobile-first layout

---

## Implementation Steps

### 1. Create `AppointmentCardComponent`

Create `src/health-platform-ui/src/app/features/booking/appointment-card/appointment-card.component.ts`:

```typescript
import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { AppointmentItemDto, AppointmentStatus } from '../../../core/models/booking.models';

type TagSeverity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast';

@Component({
  selector: 'app-appointment-card',
  standalone: true,
  imports: [CommonModule, CardModule, ButtonModule, TagModule],
  template: `
    <p-card styleClass="appointment-card mb-3">
      <div class="flex justify-content-between align-items-start flex-wrap gap-2">
        <div>
          <div class="font-semibold text-lg mb-1">{{ appointment.providerName }}</div>
          <div class="text-color-secondary mb-1">
            <i class="pi pi-calendar mr-1"></i>
            {{ appointment.slotTime | date: 'EEE, MMM d, yyyy' }}
            &nbsp;
            <i class="pi pi-clock mr-1"></i>
            {{ appointment.slotTime | date: 'h:mm a' }}
          </div>
          @if (appointment.visitReason) {
            <div class="text-sm text-color-secondary">
              <i class="pi pi-file-edit mr-1"></i>{{ appointment.visitReason }}
            </div>
          }
        </div>
        <div class="flex flex-column align-items-end gap-2">
          <p-tag [value]="statusLabel(appointment.status)" [severity]="statusSeverity(appointment.status)" />
          @if (showCancel) {
            <p-button
              label="Cancel"
              severity="danger"
              size="small"
              icon="pi pi-times"
              [outlined]="true"
              (onClick)="cancel.emit(appointment)"
            />
          }
          @if (showReschedule) {
            <p-button
              label="Reschedule"
              severity="secondary"
              size="small"
              icon="pi pi-calendar-plus"
              [outlined]="true"
              (onClick)="reschedule.emit(appointment)"
            />
          }
        </div>
      </div>
    </p-card>
  `,
})
export class AppointmentCardComponent {
  @Input({ required: true }) appointment!: AppointmentItemDto;
  @Input() showCancel = false;
  @Input() showReschedule = false;

  @Output() cancel = new EventEmitter<AppointmentItemDto>();
  @Output() reschedule = new EventEmitter<AppointmentItemDto>();

  statusLabel(status: AppointmentStatus | string): string {
    const labels: Record<string, string> = {
      Scheduled:  'Scheduled',
      Booked:     'Booked',
      Arrived:    'Arrived',
      InProgress: 'In Progress',
      Completed:  'Completed',
      Cancelled:  'Cancelled',
      NoShow:     'No Show',
      WalkIn:     'Walk-In',
    };
    return labels[status] ?? status;
  }

  statusSeverity(status: AppointmentStatus | string): TagSeverity {
    const map: Record<string, TagSeverity> = {
      Scheduled:  'info',
      Booked:     'info',
      Arrived:    'warn',
      InProgress: 'warn',
      Completed:  'success',
      Cancelled:  'secondary',
      NoShow:     'danger',
      WalkIn:     'contrast',
    };
    return map[status] ?? 'secondary';
  }
}
```

---

### 2. Update `MyAppointmentsComponent` (replace stub)

Replace `src/health-platform-ui/src/app/features/booking/my-appointments/my-appointments.component.ts`:

```typescript
import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SkeletonModule } from 'primeng/skeleton';
import { TabsModule } from 'primeng/tabs';
import { BookingStore } from '../booking.store';
import { AppointmentCardComponent } from '../appointment-card/appointment-card.component';
import { AppointmentItemDto, AppointmentStatus } from '../../../core/models/booking.models';

const UPCOMING_STATUSES: string[] = [
  AppointmentStatus.Scheduled,
  AppointmentStatus.Booked,
];

const PAST_STATUSES: string[] = [
  AppointmentStatus.Completed,
  AppointmentStatus.Cancelled,
  AppointmentStatus.NoShow,
  AppointmentStatus.Arrived,
  AppointmentStatus.InProgress,
];

@Component({
  selector: 'app-my-appointments',
  standalone: true,
  imports: [
    CommonModule,
    TabsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    SkeletonModule,
    AppointmentCardComponent,
  ],
  template: `
    <div class="my-appointments p-3" style="max-width:800px;margin:0 auto">
      <div class="flex justify-content-between align-items-center mb-4">
        <h1 class="text-2xl font-semibold m-0">My Appointments</h1>
        <p-button
          label="Book New"
          icon="pi pi-plus"
          routerLink="/booking"
          size="small"
        />
      </div>

      <!-- Skeleton while loading -->
      @if (store.isLoading()) {
        @for (i of skeletonItems; track i) {
          <div class="surface-100 border-round p-3 mb-3">
            <p-skeleton height="1.5rem" styleClass="mb-2" />
            <p-skeleton height="1rem" width="50%" />
          </div>
        }
      } @else {
        <p-tabs [value]="activeTab()" (valueChange)="activeTab.set($event)">
          <p-tablist>
            <p-tab value="upcoming">
              Upcoming
              @if (upcomingCount() > 0) {
                <span class="ml-1 text-sm text-primary">({{ upcomingCount() }})</span>
              }
            </p-tab>
            <p-tab value="past">Past</p-tab>
          </p-tablist>

          <p-tabpanels>
            <p-tabpanel value="upcoming">
              @if (upcomingAppointments().length === 0) {
                <div class="text-center py-5 text-color-secondary">
                  <i class="pi pi-calendar mb-3" style="font-size:2rem;display:block"></i>
                  No upcoming appointments.
                  <div class="mt-2">
                    <a routerLink="/booking" class="text-primary cursor-pointer">Book one now</a>
                  </div>
                </div>
              } @else {
                @for (appt of upcomingAppointments(); track appt.appointmentId) {
                  <app-appointment-card
                    [appointment]="appt"
                    [showCancel]="true"
                    (cancel)="openCancelDialog($event)"
                  />
                }
              }
            </p-tabpanel>

            <p-tabpanel value="past">
              @if (pastAppointments().length === 0) {
                <div class="text-center py-5 text-color-secondary">
                  <i class="pi pi-history mb-3" style="font-size:2rem;display:block"></i>
                  No past appointments.
                </div>
              } @else {
                @for (appt of pastAppointments(); track appt.appointmentId) {
                  <app-appointment-card [appointment]="appt" />
                }
              }
            </p-tabpanel>
          </p-tabpanels>
        </p-tabs>
      }
    </div>

    <!-- Cancel Confirmation Dialog -->
    <p-dialog
      [(visible)]="cancelDialogVisible"
      header="Cancel Appointment"
      [modal]="true"
      [draggable]="false"
      [resizable]="false"
      styleClass="w-full"
      [style]="{ 'max-width': '450px' }"
    >
      @if (appointmentToCancel()) {
        <p class="mb-3">
          Are you sure you want to cancel your appointment with
          <strong>{{ appointmentToCancel()!.providerName }}</strong> on
          {{ appointmentToCancel()!.slotTime | date: 'MMMM d, yyyy \'at\' h:mm a' }}?
        </p>
        <div class="field mb-3">
          <label for="cancelReason" class="block font-medium mb-1">Reason (optional)</label>
          <input
            pInputText
            id="cancelReason"
            [(ngModel)]="cancelReason"
            placeholder="e.g. Schedule conflict"
            class="w-full"
          />
        </div>
      }
      <ng-template pTemplate="footer">
        <p-button label="Keep Appointment" severity="secondary" (onClick)="closeCancelDialog()" />
        <p-button
          label="Yes, Cancel"
          severity="danger"
          [loading]="store.isLoading()"
          (onClick)="confirmCancel()"
        />
      </ng-template>
    </p-dialog>
  `,
})
export class MyAppointmentsComponent implements OnInit {
  readonly store = inject(BookingStore);
  readonly router = inject(Router);

  readonly activeTab = signal<string>('upcoming');
  readonly skeletonItems = [1, 2, 3];
  cancelDialogVisible = false;
  cancelReason = '';
  readonly appointmentToCancel = signal<AppointmentItemDto | null>(null);

  readonly upcomingAppointments = computed(() =>
    this.store
      .myAppointments()
      .filter((a) => UPCOMING_STATUSES.includes(a.status))
      .sort((a, b) => new Date(a.slotTime).getTime() - new Date(b.slotTime).getTime()),
  );

  readonly pastAppointments = computed(() =>
    this.store
      .myAppointments()
      .filter((a) => PAST_STATUSES.includes(a.status))
      .sort((a, b) => new Date(b.slotTime).getTime() - new Date(a.slotTime).getTime()),
  );

  readonly upcomingCount = computed(() => this.upcomingAppointments().length);

  ngOnInit(): void {
    this.store.loadMyAppointments();
  }

  openCancelDialog(appointment: AppointmentItemDto): void {
    this.appointmentToCancel.set(appointment);
    this.cancelReason = '';
    this.cancelDialogVisible = true;
  }

  closeCancelDialog(): void {
    this.cancelDialogVisible = false;
    this.appointmentToCancel.set(null);
  }

  async confirmCancel(): Promise<void> {
    const appt = this.appointmentToCancel();
    if (!appt) return;
    await this.store.cancel(appt.appointmentId, this.cancelReason || 'Patient requested cancellation');
    this.closeCancelDialog();
  }
}
```

> **Note**: `routerLink` used inside the template — add `RouterLink` to the `imports` array of
> `MyAppointmentsComponent`. Also add `FormsModule` for `[(ngModel)]` on the cancel reason input.
> Import `NgModel` via `FormsModule`.

---

### 3. Backend — `GET /api/appointments/mine` (if not yet implemented)

If the endpoint was not implemented in Task 001, add it now to `AppointmentsController.cs`:

```csharp
/// <summary>Returns all appointments for the currently authenticated patient.</summary>
[HttpGet("mine")]
[Authorize(Policy = PolicyNames.Patient)]
[ProducesResponseType(typeof(IReadOnlyList<PatientAppointmentDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetMine(CancellationToken ct)
{
    var results = await _sender.Send(new GetMyAppointmentsQuery(), ct);
    return Ok(results);
}
```

`GetMyAppointmentsQuery` handler:
- Inject `ICurrentUserService` to get `UserId` (Guid).
- Query `PatientProfile` by `UserId`, then load that patient's `Appointments` ordered by `SlotTime` descending.
- Map to `PatientAppointmentDto` matching the `AppointmentItemDto` shape:

```csharp
public sealed record PatientAppointmentDto(
    Guid           AppointmentId,
    Guid           ProviderId,
    string         ProviderName,
    DateTimeOffset SlotTime,
    DateTimeOffset EndTime,
    string         Status,
    string?        VisitReason,
    string         PatientName);
```

---

## Verification Checklist

- [ ] `ng build` has no compilation errors for `my-appointments` and `appointment-card`
- [ ] Navigating to `/booking/appointments` loads and shows the My Appointments page
- [ ] Skeleton renders during `store.isLoading() === true`
- [ ] Upcoming tab shows appointments with `Scheduled`/`Booked` status
- [ ] Past tab shows appointments with `Completed`/`Cancelled`/`NoShow` status
- [ ] Status badges have correct colors (`success` for Completed, `danger` for NoShow, etc.)
- [ ] Cancel button opens confirmation dialog
- [ ] Confirming cancel calls `store.cancel()` and removes appointment from list
- [ ] Empty state message shown when a tab has no appointments
- [ ] "Book New" button navigates to `/booking`
- [ ] Layout is readable on 320px viewport
