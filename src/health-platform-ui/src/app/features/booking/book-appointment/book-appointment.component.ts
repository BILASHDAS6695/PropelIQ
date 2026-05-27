import { CommonModule } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { BookingConfirmationComponent } from '../booking-confirmation/booking-confirmation.component';
import { BookingFormComponent } from '../booking-form/booking-form.component';
import { BookingStore } from '../booking.store';
import { ProviderListComponent } from '../provider-list/provider-list.component';
import { SlotPickerComponent } from '../slot-picker/slot-picker.component';

type BookingStep = 'provider' | 'slot' | 'form' | 'confirmation';

@Component({
  selector: 'app-book-appointment',
  standalone: true,
  imports: [
    CommonModule,
    ButtonModule,
    ProviderListComponent,
    SlotPickerComponent,
    BookingFormComponent,
    BookingConfirmationComponent,
  ],
  template: `
    <div class="booking-page p-3" style="max-width:900px;margin:0 auto">
      <!-- Step indicator -->
      <ol class="flex gap-2 list-none p-0 mb-4 flex-wrap" aria-label="Booking steps">
        @for (s of steps; track s.key; let last = $last) {
          <li
            class="flex align-items-center gap-1 text-sm"
            [class.font-semibold]="currentStep() === s.key"
            [class.text-primary]="currentStep() === s.key"
            [class.text-color-secondary]="currentStep() !== s.key"
          >
            <i [class]="s.icon"></i> {{ s.label }}
            @if (!last) {
              <span class="text-300 mx-1">›</span>
            }
          </li>
        }
      </ol>

      @switch (currentStep()) {
        @case ('provider') {
          <app-provider-list />
          @if (store.selectedProvider()) {
            <div class="mt-3">
              <p-button
                label="Next: Choose Time"
                icon="pi pi-arrow-right"
                iconPos="right"
                (onClick)="goTo('slot')"
              />
            </div>
          }
        }
        @case ('slot') {
          <app-slot-picker />
          <div class="flex gap-2 mt-3">
            <p-button
              label="Back"
              severity="secondary"
              icon="pi pi-arrow-left"
              (onClick)="goTo('provider')"
            />
            @if (store.selectedSlot()) {
              <p-button
                label="Next: Review"
                icon="pi pi-arrow-right"
                iconPos="right"
                (onClick)="goTo('form')"
              />
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
    { key: 'provider', label: 'Provider', icon: 'pi pi-user' },
    { key: 'slot', label: 'Date & Time', icon: 'pi pi-calendar' },
    { key: 'form', label: 'Details', icon: 'pi pi-file-edit' },
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
