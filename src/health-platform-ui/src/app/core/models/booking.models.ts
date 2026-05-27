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
