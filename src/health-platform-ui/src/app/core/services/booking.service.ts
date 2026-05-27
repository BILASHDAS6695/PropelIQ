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
    if (name) params = params.set('name', name);
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
