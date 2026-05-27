# Task 001: Booking API Service, Shared Models & Signal Store

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-027 |
| **Epic** | EP-002 |
| **Layer** | Angular — Core Services + Models + State (no UI) |
| **Priority** | High |
| **Estimated Effort** | 45 minutes |
| **Dependencies** | None (pure Angular, backend already implemented) |

## Objective

Three deliverables that form the data layer for all booking UI components:

1. **`booking.models.ts`** — TypeScript interfaces and enums matching the backend DTOs exactly.
2. **`booking.service.ts`** — `HttpClient`-based service wrapping every booking-related API call.
3. **`booking.store.ts`** — `@ngrx/signals` signal store managing all booking state: providers, selected
   provider, available slots, selected date/slot, my appointments, loading and error flags.

---

## Acceptance Criteria Covered

- AC: Provider list with name and specialty (models + service `getProviders`)
- AC: Available time slots per provider per date (service `getAvailableSlots`)
- AC: Booking confirmation returned to caller (service `bookAppointment`)
- AC: Patient can view their appointments (service `getMyAppointments`)
- AC: Patient can cancel an appointment (service `cancelAppointment`)
- AC: Loading states tracked centrally in signal store

---

## Implementation Steps

### 1. Create shared models

Create `src/health-platform-ui/src/app/core/models/booking.models.ts`:

```typescript
export interface ProviderSummaryDto {
  providerId: string;
  name: string;
  specialty: string | null;
}

export interface SlotDto {
  slotId: string;
  providerId: string;
  startTime: string;   // ISO-8601 string from API
  endTime: string;
  status: string;      // 'Available' | 'Booked' etc.
}

export interface BookingConfirmationDto {
  appointmentId: string;
  providerId: string;
  providerName: string;
  appointmentTime: string;  // ISO-8601 string
  status: string;
  conflictWarning: string | null;
}

export interface AppointmentItemDto {
  appointmentId: string;
  providerId: string;
  providerName: string;
  slotTime: string;          // ISO-8601 string
  endTime: string;
  status: AppointmentStatus;
  visitReason: string | null;
  patientName: string;
}

export enum AppointmentStatus {
  Scheduled = 'Scheduled',
  Booked    = 'Booked',
  Arrived   = 'Arrived',
  Completed = 'Completed',
  Cancelled = 'Cancelled',
  NoShow    = 'NoShow',
  WalkIn    = 'WalkIn',
  InProgress = 'InProgress',
}
```

> **Note**: `slotId` and `providerId` are GUIDs serialised as lowercase strings by the backend.
> `startTime` / `endTime` / `slotTime` / `appointmentTime` are ISO-8601 `DateTimeOffset` strings.
> The `AppointmentItemDto` matches the `TodayAppointmentItemDto` shape from the backend where
> overlapping. For "My Appointments" the endpoint returns a list with the same fields.

---

### 2. Create Booking HTTP service

Create `src/health-platform-ui/src/app/core/services/booking.service.ts`:

```typescript
import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AppointmentItemDto,
  BookingConfirmationDto,
  ProviderSummaryDto,
  SlotDto,
} from '../models/booking.models';

@Injectable({ providedIn: 'root' })
export class BookingService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  getProviders(specialty?: string, name?: string): Observable<ProviderSummaryDto[]> {
    let params = new HttpParams();
    if (specialty) params = params.set('specialty', specialty);
    if (name)      params = params.set('name', name);
    return this.http.get<ProviderSummaryDto[]>(`${this.base}/providers`, { params });
  }

  getAvailableSlots(providerId: string, date: string): Observable<SlotDto[]> {
    const params = new HttpParams().set('date', date);
    return this.http.get<SlotDto[]>(`${this.base}/providers/${providerId}/slots`, { params });
  }

  bookAppointment(slotId: string, visitReason: string): Observable<BookingConfirmationDto> {
    return this.http.post<BookingConfirmationDto>(`${this.base}/appointments`, {
      slotId,
      visitReason,
    });
  }

  getMyAppointments(): Observable<AppointmentItemDto[]> {
    return this.http.get<AppointmentItemDto[]>(`${this.base}/appointments/mine`);
  }

  cancelAppointment(appointmentId: string, reason: string, note?: string): Observable<void> {
    return this.http.post<void>(`${this.base}/appointments/${appointmentId}/cancel`, {
      reason,
      note,
    });
  }

  rescheduleAppointment(appointmentId: string, newSlotId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/appointments/${appointmentId}/reschedule`, {
      newSlotId,
    });
  }
}
```

> **Note**: `getMyAppointments` calls `GET /api/appointments/mine` — if that endpoint does not yet
> exist on the backend (it may need to be added as part of this US), see the backend note in the
> Acceptance Criteria. The `authInterceptor` already attaches the JWT bearer token automatically.

---

### 3. Create Booking Signal Store

Create `src/health-platform-ui/src/app/features/booking/booking.store.ts`:

```typescript
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
```

---

## Backend Note — `GET /api/appointments/mine`

The "My Appointments" feature requires a patient-scoped `GET` endpoint.
If this endpoint does not exist, add it to `AppointmentsController`:

```csharp
[HttpGet("mine")]
[Authorize(Policy = PolicyNames.Patient)]
[ProducesResponseType(typeof(IReadOnlyList<PatientAppointmentDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetMine(CancellationToken ct)
{
    var results = await _sender.Send(new GetMyAppointmentsQuery(), ct);
    return Ok(results);
}
```

`GetMyAppointmentsQuery` should resolve the current patient via `ICurrentUserService.UserId` and
return a list of `PatientAppointmentDto` records with fields matching `AppointmentItemDto` above.

---

## Verification Checklist

- [ ] `booking.models.ts` compiles with no TypeScript errors
- [ ] `BookingService` injects `HttpClient`; `authInterceptor` attaches JWT automatically
- [ ] `BookingStore` state initialises correctly; `isLoading` starts `false`
- [ ] `loadProviders()`, `loadSlots()`, `book()`, `loadMyAppointments()`, `cancel()` all call correct API paths
- [ ] Toast messages shown on success and error
- [ ] No direct `subscribe()` calls — all async via `firstValueFrom`
