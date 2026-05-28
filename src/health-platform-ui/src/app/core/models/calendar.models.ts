export interface CalendarAppointmentDto {
  appointmentId: string;
  providerId: string;
  providerName: string;
  patientName: string;
  slotTime: string; // ISO-8601 DateTimeOffset
  endTime: string; // ISO-8601 DateTimeOffset
  status: string; // 'Scheduled' | 'Booked' | 'Completed' | 'Cancelled' | 'NoShow' | ...
  visitReason: string | null;
}
