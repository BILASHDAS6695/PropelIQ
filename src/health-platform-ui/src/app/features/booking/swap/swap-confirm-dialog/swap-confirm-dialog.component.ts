import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { AppointmentItemDto, SwappableSlotDto } from '../../../../core/models/booking.models';

@Component({
  selector: 'app-swap-confirm-dialog',
  standalone: true,
  imports: [CommonModule, DialogModule, ButtonModule],
  template: `
    <p-dialog
      header="Confirm Slot Swap"
      [(visible)]="visible"
      [modal]="true"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: 'min(420px, 95vw)' }"
      (onHide)="back.emit()"
    >
      <p class="mb-3 line-height-3">
        Offer your
        <strong>{{ appointment.slotTime | date: 'h:mm a, EEE MMM d' }}</strong>
        appointment in exchange for the
        <strong>{{ targetSlot.slotTime | date: 'h:mm a, EEE MMM d' }}</strong>
        slot?
      </p>
      <p class="text-sm text-color-secondary mb-0">
        The other patient must accept this request. You may cancel it at any time while it remains
        pending.
      </p>
      <div class="flex justify-content-end gap-2 mt-4">
        <p-button
          label="Back"
          severity="secondary"
          [outlined]="true"
          [disabled]="submitting"
          (onClick)="back.emit()"
        />
        <p-button
          label="Send Request"
          icon="pi pi-check"
          [loading]="submitting"
          (onClick)="confirm.emit()"
        />
      </div>
    </p-dialog>
  `,
})
export class SwapConfirmDialogComponent {
  @Input({ required: true }) appointment!: AppointmentItemDto;
  @Input({ required: true }) targetSlot!: SwappableSlotDto;
  @Input() visible = false;
  @Input() submitting = false;

  @Output() confirm = new EventEmitter<void>();
  @Output() back = new EventEmitter<void>();
}
