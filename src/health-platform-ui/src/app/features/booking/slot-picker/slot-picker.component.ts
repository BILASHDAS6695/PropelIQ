import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { SkeletonModule } from 'primeng/skeleton';
import { BookingStore } from '../booking.store';
import { SlotDto } from '../../../core/models/booking.models';

@Component({
  selector: 'app-slot-picker',
  standalone: true,
  imports: [CommonModule, FormsModule, DatePickerModule, ButtonModule, SkeletonModule],
  template: `
    <div class="slot-picker">
      <h2 class="text-xl font-semibold mb-1">Choose a Date &amp; Time</h2>
      <p class="text-color-secondary mb-3">
        Booking with <strong>{{ store.selectedProvider()?.name }}</strong>
      </p>

      <div class="grid">
        <div class="col-12 md:col-6 mb-3">
          <p-datepicker
            [(ngModel)]="selectedDate"
            [inline]="true"
            [minDate]="today"
            styleClass="w-full"
            (onSelect)="onDateSelected($event)"
          />
        </div>

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
                    class="slot-chip"
                    [class.slot-chip--selected]="isSelected(slot)"
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
    .slot-chip {
      padding: 0.5rem 0.75rem;
      border-radius: 1.5rem;
      border: 1px solid var(--p-surface-300, #d1d5db);
      background: transparent;
      font-size: 0.875rem;
      cursor: pointer;
      transition: background 0.2s, border-color 0.2s;
    }
    .slot-chip:hover:not(.slot-chip--selected) {
      background: var(--p-primary-50, #eff6ff);
      border-color: var(--p-primary-300, #93c5fd);
    }
    .slot-chip--selected {
      background: var(--p-primary-500, #3b82f6);
      border-color: var(--p-primary-500, #3b82f6);
      color: #fff;
    }
  `],
})
export class SlotPickerComponent {
  readonly store = inject(BookingStore);

  selectedDate: Date | null = null;
  today = new Date();
  skeletonItems = [1, 2, 3, 4, 5, 6, 7];

  onDateSelected(date: Date): void {
    const providerId = this.store.selectedProvider()?.providerId;
    if (!providerId) return;
    this.store.selectDate(date);
    this.store.loadSlots(providerId, this.toIsoDate(date));
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
