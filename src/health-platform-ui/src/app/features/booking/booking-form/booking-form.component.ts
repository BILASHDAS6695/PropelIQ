import { CommonModule } from '@angular/common';
import { Component, EventEmitter, inject, Output } from '@angular/core';
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

      <div class="surface-100 border-round p-3 mb-4">
        <div class="mb-1">
          <span class="font-medium">Provider: </span>
          {{ store.selectedProvider()?.name }}
        </div>
        <div class="mb-1">
          <span class="font-medium">Date &amp; Time: </span>
          {{ store.selectedSlot()?.startTime | date: "EEEE, MMMM d, yyyy 'at' h:mm a" }}
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
            [autoResize]="true"
          ></textarea>
          @if (form.controls.visitReason.errors?.['maxlength']) {
            <small class="p-error">Maximum 500 characters.</small>
          }
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
            [disabled]="store.isLoading() || form.invalid"
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
