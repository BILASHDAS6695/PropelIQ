import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SwappableSlotDto, SwapRequestDto, SwapHistoryItemDto } from '../models/booking.models';

@Injectable({ providedIn: 'root' })
export class SwapService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  /**
   * GET /appointments/{id}/swappable-slots
   * Returns anonymized booked slots for the same provider (time only).
   */
  getSwappableSlots(appointmentId: string): Observable<SwappableSlotDto[]> {
    return this.http.get<SwappableSlotDto[]>(
      `${this.base}/appointments/${appointmentId}/swappable-slots`,
    );
  }

  /**
   * POST /appointments/{id}/swap-requests
   * Initiates a swap request: caller offers their slot for the target slot.
   */
  initiateSwapRequest(
    appointmentId: string,
    targetAppointmentId: string,
  ): Observable<SwapRequestDto> {
    return this.http.post<SwapRequestDto>(
      `${this.base}/appointments/${appointmentId}/swap-requests`,
      { targetAppointmentId },
    );
  }

  /**
   * DELETE /appointments/{id}/swap-requests/{swapRequestId}
   * Cancels a pending swap request (requester only).
   */
  cancelSwapRequest(appointmentId: string, swapRequestId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.base}/appointments/${appointmentId}/swap-requests/${swapRequestId}`,
    );
  }

  /**
   * GET /appointments/{id}/swap-requests
   * Fetches historical swap requests for an appointment (requester-side history).
   *
   * NOTE: The backend exposes GET /api/appointments/{id}/swap-requests for this
   * purpose, returning all past requests where this appointment was the offering side.
   */
  getSwapHistory(appointmentId: string): Observable<SwapHistoryItemDto[]> {
    return this.http.get<SwapHistoryItemDto[]>(
      `${this.base}/appointments/${appointmentId}/swap-requests`,
    );
  }

  /**
   * POST /appointments/{id}/swap-requests/{swapRequestId}/respond
   * Target patient accepts or declines a pending swap request.
   * Maps to US-029 Task 003 endpoint.
   */
  respondToSwapRequest(
    appointmentId: string,
    swapRequestId: string,
    accept: boolean,
    reason?: string,
  ): Observable<void> {
    return this.http.post<void>(
      `${this.base}/appointments/${appointmentId}/swap-requests/${swapRequestId}/respond`,
      { accept, reason: reason ?? null },
    );
  }
}
