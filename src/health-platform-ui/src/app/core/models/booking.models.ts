export interface ProviderSummaryDto {
  providerId: string;
  name: string;
  specialty: string | null;
}

export interface SlotDto {
  slotId: string;
  providerId: string;
  startTime: string; // ISO-8601 DateTimeOffset string
  endTime: string;
  status: string; // 'Available' | 'Booked' etc.
}

export interface BookingConfirmationDto {
  appointmentId: string;
  providerId: string;
  providerName: string;
  appointmentTime: string; // ISO-8601 DateTimeOffset string
  status: string;
  conflictWarning: string | null;
}

export interface AppointmentItemDto {
  appointmentId: string;
  providerId: string;
  providerName: string;
  slotTime: string; // ISO-8601 DateTimeOffset string
  endTime: string;
  status: AppointmentStatus;
  visitReason: string | null;
  patientName: string;
}

export enum AppointmentStatus {
  Scheduled = 'Scheduled',
  Booked = 'Booked',
  Arrived = 'Arrived',
  Completed = 'Completed',
  Cancelled = 'Cancelled',
  NoShow = 'NoShow',
  WalkIn = 'WalkIn',
  InProgress = 'InProgress',
}

// ── Slot Swap types ──────────────────────────────────────────────────────────

/** Anonymized booked slot available for swap. Patient identity is never exposed. */
export interface SwappableSlotDto {
  appointmentId: string;
  slotTime: string; // ISO-8601 DateTimeOffset
}

export enum SwapRequestStatus {
  Pending = 'Pending',
  Accepted = 'Accepted',
  Declined = 'Declined',
  Cancelled = 'Cancelled',
  Expired = 'Expired',
}

/** Result returned from POST /appointments/{id}/swap-requests. */
export interface SwapRequestDto {
  swapRequestId: string;
  requesterSlotTime: string; // ISO-8601
  targetSlotTime: string; // ISO-8601
  status: SwapRequestStatus;
  expiresAt: string; // ISO-8601
}

/** One entry in the swap history list for an appointment. */
export interface SwapHistoryItemDto {
  swapRequestId: string;
  requesterSlotTime: string; // ISO-8601
  targetSlotTime: string; // ISO-8601
  status: SwapRequestStatus;
  expiresAt: string; // ISO-8601
}
