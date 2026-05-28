import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, Input, OnChanges, Output, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { SkeletonModule } from 'primeng/skeleton';
import { SwapService } from '../../../../core/services/swap.service';
import { AppointmentItemDto, SwappableSlotDto } from '../../../../core/models/booking.models';

@Component({
  selector: 'app-swap-slot-browser',
  standalone: true,
  imports: [CommonModule, DialogModule, ButtonModule, SkeletonModule],
  template: `
    <p-dialog
      header="Choose a Slot to Swap"
      [(visible)]="visible"
      [modal]="true"
      [draggable]="false"
      [resizable]="false"
      [style]="{ width: 'min(480px, 95vw)' }"
      (onHide)="dismissed.emit()"
    >
      @if (loading()) {
        @for (i of [1, 2, 3]; track i) {
          <div class="mb-2">
            <p-skeleton height="2.5rem" />
          </div>
        }
      } @else if (error()) {
        <div class="text-center py-4 text-color-secondary">
          <i
            class="pi pi-exclamation-circle mb-2"
            style="font-size:1.5rem;display:block"
            aria-hidden="true"
          ></i>
          Failed to load available slots. Please try again.
        </div>
        <div class="flex justify-content-end mt-3">
          <p-button label="Close" severity="secondary" (onClick)="dismissed.emit()" />
        </div>
      } @else if (slots().length === 0) {
        <div class="text-center py-4 text-color-secondary" role="status">
          <i
            class="pi pi-calendar-times mb-2"
            style="font-size:1.5rem;display:block"
            aria-hidden="true"
          ></i>
          No swap options available for this appointment.
        </div>
        <div class="flex justify-content-end mt-3">
          <p-button label="Close" severity="secondary" (onClick)="dismissed.emit()" />
        </div>
      } @else {
        <p class="text-sm text-color-secondary mb-3">
          Your current appointment:
          <strong>{{ appointment.slotTime | date: 'h:mm a, EEE MMM d' }}</strong
          >. Select a slot to offer in exchange:
        </p>
        <ul class="list-none m-0 p-0" role="listbox" aria-label="Available swap slots">
          @for (slot of slots(); track slot.appointmentId) {
            <li
              class="flex align-items-center justify-content-between p-2 border-1 border-round mb-2 cursor-pointer"
              [class.slot-selected]="selectedSlot()?.appointmentId === slot.appointmentId"
              (click)="selectedSlot.set(slot)"
              (keyup.enter)="selectedSlot.set(slot)"
              tabindex="0"
              role="option"
              [attr.aria-selected]="selectedSlot()?.appointmentId === slot.appointmentId"
              [attr.aria-label]="'Swap with slot at ' + (slot.slotTime | date: 'h:mm a, EEE MMM d')"
            >
              <span class="font-medium">{{ slot.slotTime | date: 'h:mm a' }}</span>
              <span class="text-color-secondary text-sm">{{
                slot.slotTime | date: 'EEE, MMM d'
              }}</span>
            </li>
          }
        </ul>
        <div class="flex justify-content-end gap-2 mt-3">
          <p-button
            label="Cancel"
            severity="secondary"
            [outlined]="true"
            (onClick)="dismissed.emit()"
          />
          <p-button
            label="Next"
            icon="pi pi-arrow-right"
            iconPos="right"
            [disabled]="!selectedSlot()"
            (onClick)="slotSelected.emit(selectedSlot()!)"
          />
        </div>
      }
    </p-dialog>
  `,
  styles: [
    `
      .slot-selected {
        border-color: var(--p-primary-color) !important;
        background-color: var(--p-primary-50, #eff6ff);
      }
    `,
  ],
})
export class SwapSlotBrowserComponent implements OnChanges {
  @Input({ required: true }) appointment!: AppointmentItemDto;
  @Input() visible = false;

  @Output() slotSelected = new EventEmitter<SwappableSlotDto>();
  @Output() dismissed = new EventEmitter<void>();

  private readonly swapSvc = inject(SwapService);

  readonly slots = signal<SwappableSlotDto[]>([]);
  readonly selectedSlot = signal<SwappableSlotDto | null>(null);
  readonly loading = signal(true);
  readonly error = signal(false);

  ngOnChanges(): void {
    if (!this.visible) return;
    // Reset state each time the dialog opens
    this.loading.set(true);
    this.error.set(false);
    this.selectedSlot.set(null);

    this.swapSvc.getSwappableSlots(this.appointment.appointmentId).subscribe({
      next: (data) => {
        this.slots.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }
}
