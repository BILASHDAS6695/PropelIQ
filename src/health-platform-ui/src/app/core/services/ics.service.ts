import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';
import { AppointmentItemDto, AppointmentStatus } from '../models/booking.models';

/** Minimum shape both DTO types satisfy for ICS generation. */
export interface IcsAppointment {
  appointmentId: string;
  providerName: string;
  slotTime: string; // ISO-8601
  endTime: string; // ISO-8601
}

@Injectable({ providedIn: 'root' })
export class IcsService {
  private readonly location = environment.clinicAddress;

  /** Returns a single VCALENDAR string for one appointment. */
  buildSingle(appt: IcsAppointment): string {
    return this.wrapCalendar([this.buildVEvent(appt)]);
  }

  /**
   * Returns a single VCALENDAR string containing one VEVENT per appointment.
   * Cancelled appointments are silently excluded.
   */
  buildBulk(appts: AppointmentItemDto[]): string {
    const events = appts
      .filter((a) => a.status !== AppointmentStatus.Cancelled)
      .map((a) => this.buildVEvent(a));
    return this.wrapCalendar(events);
  }

  /**
   * Triggers a browser download of the provided ICS content.
   * No server round-trip; purely client-side.
   */
  download(filename: string, icsContent: string): void {
    const blob = new Blob([icsContent], { type: 'text/calendar;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename.endsWith('.ics') ? filename : `${filename}.ics`;
    a.click();
    URL.revokeObjectURL(url);
  }

  private wrapCalendar(events: string[]): string {
    return [
      'BEGIN:VCALENDAR',
      'VERSION:2.0',
      'PRODID:-//HealthPlatform//Calendar//EN',
      'CALSCALE:GREGORIAN',
      'METHOD:PUBLISH',
      ...events,
      'END:VCALENDAR',
    ].join('\r\n');
  }

  private buildVEvent(appt: IcsAppointment): string {
    const start = this.toUtcString(new Date(appt.slotTime));
    const end = this.toUtcString(new Date(appt.endTime));
    const now = this.toUtcString(new Date());
    return [
      'BEGIN:VEVENT',
      `UID:${appt.appointmentId}@healthplatform`,
      `DTSTAMP:${now}`,
      `DTSTART:${start}`,
      `DTEND:${end}`,
      `SUMMARY:Appointment with ${this.escapeText(appt.providerName)}`,
      `LOCATION:${this.escapeText(this.location)}`,
      'BEGIN:VALARM',
      'TRIGGER:-PT1H',
      'ACTION:DISPLAY',
      'DESCRIPTION:Upcoming appointment reminder',
      'END:VALARM',
      'END:VEVENT',
    ].join('\r\n');
  }

  private toUtcString(date: Date): string {
    return date
      .toISOString()
      .replace(/[-:]/g, '')
      .replace(/\.\d{3}/, '');
  }

  private escapeText(text: string): string {
    return text.replace(/\\/g, '\\\\').replace(/;/g, '\\;').replace(/,/g, '\\,');
  }
}
