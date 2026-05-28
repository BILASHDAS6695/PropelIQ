import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { CalendarService } from '../../core/services/calendar.service';
import { BookingService } from '../../core/services/booking.service';
import { ToastService } from '../../shared/services/toast.service';
import { CalendarAppointmentDto } from '../../core/models/calendar.models';
import { ProviderSummaryDto, SlotDto } from '../../core/models/booking.models';

interface MultiProviderDayState {
  currentDate: Date;
  allProviders: ProviderSummaryDto[];
  selectedProviderIds: string[];
  appointmentsByProvider: Record<string, CalendarAppointmentDto[]>;
  slotsByProvider: Record<string, SlotDto[]>;
  isLoading: boolean;
}

const initialState: MultiProviderDayState = {
  currentDate: new Date(),
  allProviders: [],
  selectedProviderIds: [],
  appointmentsByProvider: {},
  slotsByProvider: {},
  isLoading: false,
};

export const MultiProviderDayStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods(
    (
      store,
      calSvc = inject(CalendarService),
      bookSvc = inject(BookingService),
      toast = inject(ToastService),
    ) => ({
      async init(): Promise<void> {
        patchState(store, { isLoading: true });
        try {
          const allProviders = await firstValueFrom(bookSvc.getProviders());
          const selectedProviderIds = allProviders
            .slice(0, Math.min(3, allProviders.length))
            .map((p) => p.providerId);
          patchState(store, { allProviders, selectedProviderIds, isLoading: false });
          await this.loadForDate(store.currentDate());
        } catch {
          patchState(store, { isLoading: false });
          toast.error('Error', 'Failed to load providers.');
        }
      },

      toggleProvider(providerId: string): void {
        const current = store.selectedProviderIds();
        const next = current.includes(providerId)
          ? current.filter((id) => id !== providerId)
          : [...current, providerId];
        patchState(store, { selectedProviderIds: next });
      },

      async setDate(date: Date): Promise<void> {
        patchState(store, { currentDate: date });
        await this.loadForDate(date);
      },

      navigateDay(direction: 'prev' | 'next'): void {
        const d = new Date(store.currentDate());
        d.setDate(d.getDate() + (direction === 'next' ? 1 : -1));
        patchState(store, { currentDate: d });
        void this.loadForDate(d);
      },

      goToToday(): void {
        const today = new Date();
        patchState(store, { currentDate: today });
        void this.loadForDate(today);
      },

      async loadForDate(date: Date): Promise<void> {
        const providerIds = store.selectedProviderIds();
        if (providerIds.length === 0) return;
        patchState(store, { isLoading: true });

        const dayStart = new Date(date.getFullYear(), date.getMonth(), date.getDate(), 0, 0, 0);
        const dayEnd = new Date(date.getFullYear(), date.getMonth(), date.getDate(), 23, 59, 59);
        const dateStr = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;

        try {
          const results = await Promise.all(
            providerIds.map(async (pid) => {
              const [appointments, slots] = await Promise.all([
                firstValueFrom(calSvc.getAppointments(dayStart, dayEnd, pid)),
                firstValueFrom(bookSvc.getAvailableSlots(pid, dateStr)),
              ]);
              return { pid, appointments, slots };
            }),
          );

          const appointmentsByProvider: Record<string, CalendarAppointmentDto[]> = {};
          const slotsByProvider: Record<string, SlotDto[]> = {};
          for (const { pid, appointments, slots } of results) {
            appointmentsByProvider[pid] = appointments;
            slotsByProvider[pid] = slots;
          }

          patchState(store, { appointmentsByProvider, slotsByProvider, isLoading: false });
        } catch {
          patchState(store, { isLoading: false });
          toast.error('Error', 'Failed to load schedule data.');
        }
      },

      updateAppointmentProvider(
        appointmentId: string,
        fromProviderId: string,
        toProviderId: string,
      ): void {
        const apptsByProvider = { ...store.appointmentsByProvider() };
        const appt = apptsByProvider[fromProviderId]?.find(
          (a) => a.appointmentId === appointmentId,
        );
        if (!appt) return;
        apptsByProvider[fromProviderId] = (apptsByProvider[fromProviderId] ?? []).filter(
          (a) => a.appointmentId !== appointmentId,
        );
        apptsByProvider[toProviderId] = [
          ...(apptsByProvider[toProviderId] ?? []),
          { ...appt, providerId: toProviderId },
        ];
        patchState(store, { appointmentsByProvider: apptsByProvider });
      },
    }),
  ),
);
