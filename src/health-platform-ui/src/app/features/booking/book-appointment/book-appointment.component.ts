import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
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
      @if (rescheduleId()) {
        <div
          class="flex align-items-center gap-2 mb-3 p-3 surface-100 border-round border-left-3 border-primary"
        >
          <i class="pi pi-calendar-clock text-primary text-xl"></i>
          <span class="font-semibold text-lg">Reschedule Appointment</span>
          <span class="text-color-secondary text-sm ml-1"
            >— select a new provider and time slot</span
          >
        </div>
      }
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
              @if (rescheduleId()) {
                <p-button
                  label="Confirm Reschedule"
                  icon="pi pi-check"
                  [loading]="store.isLoading()"
                  (onClick)="confirmReschedule()"
                />
              } @else {
                <p-button
                  label="Next: Review"
                  icon="pi pi-arrow-right"
                  iconPos="right"
                  (onClick)="goTo('form')"
                />
              }
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
export class BookAppointmentComponent implements OnInit {
  readonly store = inject(BookingStore);
  readonly currentStep = signal<BookingStep>('provider');
  readonly rescheduleId = signal<string | null>(null);

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly steps = [
    { key: 'provider', label: 'Provider', icon: 'pi pi-user' },
    { key: 'slot', label: 'Date & Time', icon: 'pi pi-calendar' },
    { key: 'form', label: 'Details', icon: 'pi pi-file-edit' },
    { key: 'confirmation', label: 'Confirmation', icon: 'pi pi-check-circle' },
  ] as const;

  ngOnInit(): void {
    const id = this.route.snapshot.queryParamMap.get('reschedule');
    if (id) {
      this.rescheduleId.set(id);
      this.store.resetBookingFlow();
    }
  }

  goTo(step: BookingStep): void {
    this.currentStep.set(step);
  }

  async confirmReschedule(): Promise<void> {
    const id = this.rescheduleId();
    const slot = this.store.selectedSlot();
    if (!id || !slot) return;
    await this.store.reschedule(id, slot.slotId);
    if (!this.store.error()) {
      void this.router.navigate(['/booking/appointments']);
    }
  }

  restart(): void {
    this.store.resetBookingFlow();
    this.currentStep.set('provider');
  }
}
