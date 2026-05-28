import { TestBed } from '@angular/core/testing';
import { AppointmentStatus } from '../models/booking.models';
import type { AppointmentItemDto } from '../models/booking.models';
import { IcsService } from './ics.service';

const BASE_APPT: AppointmentItemDto = {
  appointmentId: 'appt-001',
  providerId: 'prov-001',
  providerName: 'Dr. Smith',
  slotTime: '2026-06-15T10:00:00Z',
  endTime: '2026-06-15T10:30:00Z',
  status: AppointmentStatus.Scheduled,
  visitReason: 'Annual checkup', // must NOT appear in ICS output
  patientName: 'Jane Doe',
  intakeStatus: null,
  isIntakeWindowOpen: false,
};

describe('IcsService', () => {
  let svc: IcsService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    svc = TestBed.inject(IcsService);
  });

  it('buildSingle: produces RFC 5545 structure', () => {
    const ics = svc.buildSingle(BASE_APPT);
    expect(ics).toContain('BEGIN:VCALENDAR');
    expect(ics).toContain('BEGIN:VEVENT');
    expect(ics).toContain('DTSTART:20260615T100000Z');
    expect(ics).toContain('DTEND:20260615T103000Z');
    expect(ics).toContain('SUMMARY:Appointment with Dr. Smith');
    expect(ics).toContain('END:VEVENT');
    expect(ics).toContain('END:VCALENDAR');
  });

  it('buildSingle: includes VALARM 1 hour before', () => {
    const ics = svc.buildSingle(BASE_APPT);
    expect(ics).toContain('BEGIN:VALARM');
    expect(ics).toContain('TRIGGER:-PT1H');
    expect(ics).toContain('ACTION:DISPLAY');
    expect(ics).toContain('END:VALARM');
  });

  it('buildSingle: does not include visit reason', () => {
    const ics = svc.buildSingle(BASE_APPT);
    expect(ics).not.toContain('Annual checkup');
    expect(ics).not.toContain('visitReason');
  });

  it('buildBulk: excludes cancelled appointments', () => {
    const cancelled: AppointmentItemDto = {
      ...BASE_APPT,
      appointmentId: 'appt-002',
      status: AppointmentStatus.Cancelled,
    };
    const ics = svc.buildBulk([BASE_APPT, cancelled]);
    // Only one VEVENT for the scheduled appointment
    const eventCount = (ics.match(/BEGIN:VEVENT/g) ?? []).length;
    expect(eventCount).toBe(1);
    expect(ics).toContain('appt-001@healthplatform');
    expect(ics).not.toContain('appt-002@healthplatform');
  });
});
