# Task 001: Swap API Models + Angular Service Layer

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-031 |
| **Epic** | EP-003 |
| **Layer** | Angular / Core (models + service) |
| **Priority** | Medium |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | US-028 Task 003 (swappable-slots + swap-requests endpoints), US-029 Task 003 (respond endpoint) |

## Objective

Extend the Angular model layer with swap-specific DTOs and create `SwapService` to
wrap every swap-related API endpoint. All subsequent UI tasks depend on this service —
no API calls belong in components.

## Acceptance Criteria Covered

- AC: Patient views list of other patients' booked slots (swap browser requires `getSwappableSlots`)
- AC: Swap request confirmation dialog (requires `initiateSwapRequest`)
- AC: Requester can cancel a pending swap (requires `cancelSwapRequest`)
- AC: Swap history visible in appointment detail (requires `getSwapHistory`)
- AC: Accept/Decline from notification panel (requires `respondToSwapRequest`)

---

## Implementation Steps

### 1. Extend `booking.models.ts` with Swap-Specific Types

Edit `src/health-platform-ui/src/app/core/models/booking.models.ts`.

Append the following block **after** the `AppointmentStatus` enum (end of file):

```typescript
// ── Slot Swap types ──────────────────────────────────────────────────────────

/** Anonymized booked slot available for swap. Patient identity is never exposed. */
export interface SwappableSlotDto {
  appointmentId: string;
  slotTime: string; // ISO-8601 DateTimeOffset
}

export enum SwapRequestStatus {
  Pending   = 'Pending',
  Accepted  = 'Accepted',
  Declined  = 'Declined',
  Cancelled = 'Cancelled',
  Expired   = 'Expired',
}

/** Result returned from POST /appointments/{id}/swap-requests. */
export interface SwapRequestDto {
  swapRequestId: string;
  requesterSlotTime: string; // ISO-8601
  targetSlotTime: string;    // ISO-8601
  status: SwapRequestStatus;
  expiresAt: string;         // ISO-8601
}

/** One entry in the swap history list for an appointment. */
export interface SwapHistoryItemDto {
  swapRequestId: string;
  requesterSlotTime: string; // ISO-8601
  targetSlotTime: string;    // ISO-8601
  status: SwapRequestStatus;
  expiresAt: string;         // ISO-8601
}
```

---

### 2. Create `SwapService`

Create `src/health-platform-ui/src/app/core/services/swap.service.ts`:

```typescript
import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  SwappableSlotDto,
  SwapRequestDto,
  SwapHistoryItemDto,
} from '../models/booking.models';

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
   * Fetches historical swap requests for an appointment.
   *
   * NOTE: This endpoint returns the requester-side history (all past requests
   * where this appointment was the offering side). The backend exposes
   * GET /api/appointments/{id}/swap-requests for this purpose.
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
```

---

## Verification Checklist

- [ ] `SwappableSlotDto`, `SwapRequestStatus`, `SwapRequestDto`, `SwapHistoryItemDto` present in `booking.models.ts`
- [ ] `SwapService` compiles without errors (`ng build`)
- [ ] Each method targets the correct route from the US-028/US-029 REST contract:
  - `GET /appointments/{id}/swappable-slots`
  - `POST /appointments/{id}/swap-requests`
  - `DELETE /appointments/{id}/swap-requests/{swapRequestId}`
  - `GET /appointments/{id}/swap-requests`
  - `POST /appointments/{id}/swap-requests/{swapRequestId}/respond`
- [ ] No API calls reference hardcoded URLs — all use `environment.apiUrl`
