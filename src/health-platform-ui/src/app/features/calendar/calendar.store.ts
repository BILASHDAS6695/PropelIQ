import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { CalendarService } from '../../core/services/calendar.service';
import { ToastService } from '../../shared/services/toast.service';
import { CalendarAppointmentDto } from '../../core/models/calendar.models';

export type CalendarViewMode = 'month' | 'week' | 'day';

interface CalendarState {
  viewMode: CalendarViewMode;
  currentDate: Date;
  appointments: CalendarAppointmentDto[];
  isLoading: boolean;
  selectedAppointment: CalendarAppointmentDto | null;
  selectedProviderId: string | null;
}

const initialState: CalendarState = {
  viewMode: 'month',
  currentDate: new Date(),
  appointments: [],
  isLoading: false,
  selectedAppointment: null,
  selectedProviderId: null,
};

export const CalendarStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store, svc = inject(CalendarService), toast = inject(ToastService)) => ({
    setViewMode(mode: CalendarViewMode): void {
      patchState(store, { viewMode: mode });
    },

    setCurrentDate(date: Date): void {
      patchState(store, { currentDate: date });
    },

    setSelectedAppointment(appt: CalendarAppointmentDto | null): void {
      patchState(store, { selectedAppointment: appt });
    },

    setSelectedProvider(providerId: string | null): void {
      patchState(store, { selectedProviderId: providerId });
    },

    async loadRange(from: Date, to: Date, providerId?: string): Promise<void> {
      patchState(store, { isLoading: true });
      try {
        const appointments = await firstValueFrom(
          svc.getAppointments(from, to, providerId ?? undefined),
        );
        patchState(store, { appointments, isLoading: false });
      } catch {
        patchState(store, { isLoading: false });
        toast.error('Error', 'Failed to load calendar appointments.');
      }
    },

    navigate(direction: 'prev' | 'next'): void {
      const current = store.currentDate();
      const mode = store.viewMode();
      const d = new Date(current);

      if (mode === 'month') {
        d.setMonth(d.getMonth() + (direction === 'next' ? 1 : -1));
      } else if (mode === 'week') {
        d.setDate(d.getDate() + (direction === 'next' ? 7 : -7));
      } else {
        d.setDate(d.getDate() + (direction === 'next' ? 1 : -1));
      }

      patchState(store, { currentDate: d });
    },

    goToToday(): void {
      patchState(store, { currentDate: new Date() });
    },
  })),
);
