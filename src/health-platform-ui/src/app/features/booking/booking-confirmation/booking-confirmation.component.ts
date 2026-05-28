import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { BookingConfirmationDto } from '../../../core/models/booking.models';
import { IcsService } from '../../../core/services/ics.service';
import { BookingStore } from '../booking.store';

@Component({
  selector: 'app-booking-confirmation',
  standalone: true,
  imports: [CommonModule, ButtonModule],
  template: `
    <div class="booking-confirmation text-center py-4">
      <i class="pi pi-check-circle text-green-500 mb-3" style="font-size:3rem;display:block"></i>
      <h2 class="text-2xl font-semibold mb-4">Appointment Confirmed!</h2>

      @if (store.lastConfirmation(); as c) {
        <div
          class="surface-100 border-round p-3 mb-4 text-left inline-block"
          style="min-width:280px;max-width:420px"
        >
          <div class="mb-1"><span class="font-medium">Provider: </span>{{ c.providerName }}</div>
          <div class="mb-1">
            <span class="font-medium">Date &amp; Time: </span>
            {{ c.appointmentTime | date: "EEEE, MMMM d, yyyy 'at' h:mm a" }}
          </div>
          <div><span class="font-medium">Status: </span>{{ c.status }}</div>
        </div>

        <div class="flex justify-content-center gap-2 flex-wrap">
          <p-button
            label="Add to Calendar"
            icon="pi pi-calendar-plus"
            severity="secondary"
            [outlined]="true"
            (onClick)="addToCalendar(c)"
          />
          <p-button label="Book Another" icon="pi pi-plus" (onClick)="bookAnother.emit()" />
        </div>
      }
    </div>
  `,
})
export class BookingConfirmationComponent {
  @Output() bookAnother = new EventEmitter<void>();

  readonly store = inject(BookingStore);
  readonly ics = inject(IcsService);

  addToCalendar(c: BookingConfirmationDto): void {
    const start = new Date(c.appointmentTime);
    const end = new Date(start.getTime() + 30 * 60 * 1000);
    const content = this.ics.buildSingle({
      appointmentId: c.appointmentId,
      providerName: c.providerName,
      slotTime: start.toISOString(),
      endTime: end.toISOString(),
    });
    this.ics.download(`appointment-${c.appointmentId}`, content);
  }
}
