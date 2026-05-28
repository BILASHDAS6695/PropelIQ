import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { AppointmentItemDto, AppointmentStatus } from '../../../core/models/booking.models';
import { IcsService } from '../../../core/services/ics.service';

type TagSeverity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast';

@Component({
  selector: 'app-appointment-card',
  standalone: true,
  imports: [CommonModule, CardModule, ButtonModule, TagModule],
  template: `
    <p-card styleClass="mb-3">
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
          <p-tag
            [value]="statusLabel(appointment.status)"
            [severity]="statusSeverity(appointment.status)"
          />
          @if (showCancel) {
            <p-button
              label="Cancel"
              severity="danger"
              size="small"
              icon="pi pi-times"
              [outlined]="true"
              (onClick)="cancelRequest.emit(appointment)"
            />
          }
          @if (showReschedule) {
            <p-button
              label="Reschedule"
              severity="secondary"
              size="small"
              icon="pi pi-calendar-plus"
              [outlined]="true"
              (onClick)="rescheduleRequest.emit(appointment)"
            />
          }
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
        </div>
      </div>
    </p-card>
  `,
})
export class AppointmentCardComponent {
  @Input({ required: true }) appointment!: AppointmentItemDto;
  @Input() showCancel = false;
  @Input() showReschedule = false;
  @Input() showAddToCalendar = false;

  @Output() cancelRequest = new EventEmitter<AppointmentItemDto>();
  @Output() rescheduleRequest = new EventEmitter<AppointmentItemDto>();

  private readonly ics = inject(IcsService);

  downloadIcs(appt: AppointmentItemDto): void {
    const content = this.ics.buildSingle(appt);
    this.ics.download(`appointment-${appt.appointmentId}`, content);
  }

  statusLabel(status: AppointmentStatus | string): string {
    const labels: Record<string, string> = {
      Scheduled: 'Scheduled',
      Booked: 'Booked',
      Arrived: 'Arrived',
      InProgress: 'In Progress',
      Completed: 'Completed',
      Cancelled: 'Cancelled',
      NoShow: 'No Show',
      WalkIn: 'Walk-In',
    };
    return labels[status] ?? String(status);
  }

  statusSeverity(status: AppointmentStatus | string): TagSeverity {
    const map: Record<string, TagSeverity> = {
      Scheduled: 'info',
      Booked: 'info',
      Arrived: 'warn',
      InProgress: 'warn',
      Completed: 'success',
      Cancelled: 'secondary',
      NoShow: 'danger',
      WalkIn: 'contrast',
    };
    return map[status] ?? 'secondary';
  }
}
