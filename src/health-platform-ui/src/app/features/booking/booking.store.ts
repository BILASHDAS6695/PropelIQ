import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { BookingService } from '../../core/services/booking.service';
import { ToastService } from '../../shared/services/toast.service';
import {
  AppointmentItemDto,
  BookingConfirmationDto,
  ProviderSummaryDto,
  SlotDto,
} from '../../core/models/booking.models';

interface BookingState {
  providers: ProviderSummaryDto[];
  selectedProvider: ProviderSummaryDto | null;
  availableSlots: SlotDto[];
  selectedDate: Date | null;
  selectedSlot: SlotDto | null;
  lastConfirmation: BookingConfirmationDto | null;
  myAppointments: AppointmentItemDto[];
  isLoading: boolean;
  slotsLoading: boolean;
  error: string | null;
}

const initialState: BookingState = {
  providers: [],
  selectedProvider: null,
  availableSlots: [],
  selectedDate: null,
  selectedSlot: null,
  lastConfirmation: null,
  myAppointments: [],
  isLoading: false,
  slotsLoading: false,
  error: null,
};

export const BookingStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store, bookingService = inject(BookingService), toast = inject(ToastService)) => ({
    async loadProviders(specialty?: string, name?: string): Promise<void> {
      patchState(store, { isLoading: true, error: null });
      try {
        const providers = await firstValueFrom(bookingService.getProviders(specialty, name));
        patchState(store, { providers, isLoading: false });
      } catch {
        patchState(store, { isLoading: false, error: 'Failed to load providers.' });
        toast.error('Error', 'Failed to load providers.');
      }
    },

    selectProvider(provider: ProviderSummaryDto): void {
      patchState(store, {
        selectedProvider: provider,
        availableSlots: [],
        selectedDate: null,
        selectedSlot: null,
      });
    },

    selectDate(date: Date): void {
      patchState(store, { selectedDate: date, selectedSlot: null, availableSlots: [] });
    },

    async loadSlots(providerId: string, date: string): Promise<void> {
      patchState(store, { slotsLoading: true, error: null });
      try {
        const availableSlots = await firstValueFrom(
          bookingService.getAvailableSlots(providerId, date),
        );
        patchState(store, { availableSlots, slotsLoading: false });
      } catch {
        patchState(store, { slotsLoading: false, error: 'Failed to load slots.' });
        toast.error('Error', 'Failed to load available slots.');
      }
    },

    selectSlot(slot: SlotDto): void {
      patchState(store, { selectedSlot: slot });
    },

    async book(visitReason: string): Promise<BookingConfirmationDto | null> {
      const slot = store.selectedSlot();
      if (!slot) return null;
      patchState(store, { isLoading: true, error: null });
      try {
        const confirmation = await firstValueFrom(
          bookingService.bookAppointment(slot.slotId, visitReason),
        );
        patchState(store, { lastConfirmation: confirmation, isLoading: false });
        toast.success('Booked', 'Your appointment has been confirmed.');
        return confirmation;
      } catch {
        patchState(store, { isLoading: false, error: 'Booking failed.' });
        toast.error('Error', 'Could not book the appointment. Please try again.');
        return null;
      }
    },

    async loadMyAppointments(): Promise<void> {
      patchState(store, { isLoading: true, error: null });
      try {
        const myAppointments = await firstValueFrom(bookingService.getMyAppointments());
        patchState(store, { myAppointments, isLoading: false });
      } catch {
        patchState(store, { isLoading: false, error: 'Failed to load appointments.' });
        toast.error('Error', 'Failed to load your appointments.');
      }
    },

    async cancel(appointmentId: string, reason: string): Promise<void> {
      patchState(store, { isLoading: true });
      try {
        await firstValueFrom(bookingService.cancelAppointment(appointmentId, reason));
        const myAppointments = store
          .myAppointments()
          .filter((a) => a.appointmentId !== appointmentId);
        patchState(store, { myAppointments, isLoading: false });
        toast.success('Cancelled', 'Your appointment has been cancelled.');
      } catch {
        patchState(store, { isLoading: false });
        toast.error('Error', 'Could not cancel the appointment.');
      }
    },

    async reschedule(appointmentId: string, newSlotId: string): Promise<void> {
      patchState(store, { isLoading: true });
      try {
        await firstValueFrom(bookingService.rescheduleAppointment(appointmentId, newSlotId));
        patchState(store, { isLoading: false });
        toast.success('Rescheduled', 'Your appointment has been rescheduled.');
      } catch {
        patchState(store, { isLoading: false });
        toast.error('Error', 'Could not reschedule the appointment.');
      }
    },

    resetBookingFlow(): void {
      patchState(store, {
        selectedProvider: null,
        availableSlots: [],
        selectedDate: null,
        selectedSlot: null,
        lastConfirmation: null,
      });
    },
  })),
);
