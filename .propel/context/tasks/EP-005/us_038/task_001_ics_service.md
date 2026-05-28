# Task 001: IcsService — RFC 5545 Calendar Generator

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-038 |
| **Epic** | EP-005 |
| **Layer** | Frontend — core service + environment config |
| **Priority** | High |
| **Estimated Effort** | 20 minutes |
| **Dependencies** | None — pure TypeScript utility, no backend changes |

## Objective

1. **Add `clinicAddress` to environment config** — used as the ICS `LOCATION` field.
2. **Add `IcsService`** — RFC 5545-compliant calendar file generator with two
   public methods: `buildSingle(appt)` and `buildBulk(appts)`.
3. **Add `ics.service.spec.ts`** — 4 unit tests verifying RFC fields, alarm
   presence, absence of medical data, and bulk-export filtering.

---

## Acceptance Criteria Covered

- AC: Generates standard .ics file (iCalendar format RFC 5545)
- AC: ICS contains: event title, start/end time, location (clinic address)
- AC: ICS includes reminder alarm (1 hour before)
- AC: ICS file does not contain sensitive medical information (no visit reason)
- AC: Bulk export excludes cancelled appointments

---

## Design Notes

- **No server-side file storage** — the service returns a raw ICS string; the
  caller constructs a `data:text/calendar` URI or uses the provided `download()`
  helper to trigger a browser download.
- **Appointment type** — `AppointmentItemDto` and `CalendarAppointmentDto` do not
  currently expose an appointment type field. Summary defaults to
  `"Appointment with {providerName}"`. Update when backend exposes the field.
- **Visit reason** — must never appear in `DESCRIPTION`, `SUMMARY`, `COMMENT`, or
  any other ICS field (AC: no sensitive medical information).
- **Time format** — all timestamps emitted as UTC in the compact format
  `YYYYMMDDTHHMMSSZ`.
- **UID** — generated as `{appointmentId}@healthplatform` for idempotency.

---

## Implementation Steps

### 1. Add `clinicAddress` to environments

Edit `src/health-platform-ui/src/environments/environment.ts`:

```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5013/api',
  appName: 'HealthPlatform',
  clinicAddress: 'HealthPlatform Clinic, 123 Medical Drive, Boston MA 02101',
};
```

Edit `src/health-platform-ui/src/environments/environment.prod.ts`:

```typescript
export const environment = {
  production: true,
  apiUrl: '/api',
  appName: 'HealthPlatform',
  clinicAddress: 'HealthPlatform Clinic, 123 Medical Drive, Boston MA 02101',
};
```

---

### 2. Add `IcsService`

Create `src/health-platform-ui/src/app/core/services/ics.service.ts`:

```typescript
import { Injectable } from '@angular/core';
import { environment } from '../../../../environments/environment';
import { AppointmentItemDto, AppointmentStatus } from '../models/booking.models';
import { CalendarAppointmentDto } from '../models/calendar.models';

/** Minimum shape both DTO types satisfy for ICS generation. */
export interface IcsAppointment {
  appointmentId: string;
  providerName: string;
  slotTime: string;  // ISO-8601
  endTime: string;   // ISO-8601
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
    return date.toISOString().replace(/[-:]/g, '').replace(/\.\d{3}/, '');
  }

  private escapeText(text: string): string {
    return text.replace(/\\/g, '\\\\').replace(/;/g, '\\;').replace(/,/g, '\\,');
  }
}
```

---

### 3. Add `ics.service.spec.ts`

Create `src/health-platform-ui/src/app/core/services/ics.service.spec.ts`:

```typescript
import { TestBed } from '@angular/core/testing';
import { IcsService } from './ics.service';
import { AppointmentStatus } from '../models/booking.models';
import type { AppointmentItemDto } from '../models/booking.models';

const BASE_APPT: AppointmentItemDto = {
  appointmentId: 'appt-001',
  providerId: 'prov-001',
  providerName: 'Dr. Smith',
  slotTime: '2026-06-15T10:00:00Z',
  endTime: '2026-06-15T10:30:00Z',
  status: AppointmentStatus.Scheduled,
  visitReason: 'Annual checkup',  // must NOT appear in ICS output
  patientName: 'Jane Doe',
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
```

---

## Verification

```bash
cd src/health-platform-ui
npx ng test --no-watch
```

Expected: all existing tests pass + 4 new `IcsService` tests green.

```bash
npx ng lint
```

Expected: `All files pass linting.`
