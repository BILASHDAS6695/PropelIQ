import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SkeletonModule } from 'primeng/skeleton';
import { TabsModule } from 'primeng/tabs';
import { AppointmentCardComponent } from '../appointment-card/appointment-card.component';
import { BookingStore } from '../booking.store';
import { AppointmentItemDto, AppointmentStatus } from '../../../core/models/booking.models';

const UPCOMING_STATUSES: string[] = [AppointmentStatus.Scheduled, AppointmentStatus.Booked];

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
    FormsModule,
    RouterLink,
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
          size="small"
          (onClick)="router.navigate(['/booking'])"
        />
      </div>

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
          {{ appointmentToCancel()!.slotTime | date: "MMMM d, yyyy 'at' h:mm a" }}?
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
        <p-button
          label="Keep Appointment"
          severity="secondary"
          (onClick)="closeCancelDialog()"
        />
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
    await this.store.cancel(
      appt.appointmentId,
      this.cancelReason || 'Patient requested cancellation',
    );
    this.closeCancelDialog();
  }
}
