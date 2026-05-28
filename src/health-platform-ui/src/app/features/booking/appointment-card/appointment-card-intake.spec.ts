import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { AppointmentCardComponent } from './appointment-card.component';
import { AppointmentItemDto, AppointmentStatus } from '../../../core/models/booking.models';

function makeAppt(overrides: Partial<AppointmentItemDto> = {}): AppointmentItemDto {
  return {
    appointmentId: 'appt-1',
    providerId: 'prov-1',
    providerName: 'Dr. Smith',
    slotTime: new Date().toISOString(),
    endTime: new Date().toISOString(),
    status: AppointmentStatus.Scheduled,
    visitReason: null,
    patientName: 'Jane Doe',
    intakeStatus: null,
    isIntakeWindowOpen: false,
    ...overrides,
  };
}

describe('AppointmentCardComponent — intake', () => {
  let fixture: ComponentFixture<AppointmentCardComponent>;
  let component: AppointmentCardComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppointmentCardComponent],
      providers: [provideHttpClient(), provideNoopAnimations(), MessageService],
    }).compileComponents();
    fixture = TestBed.createComponent(AppointmentCardComponent);
    component = fixture.componentInstance;
    component.appointment = makeAppt();
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('canCompleteIntake is true when window open and status is Scheduled with no intake record', () => {
    component.appointment = makeAppt({ intakeStatus: null, isIntakeWindowOpen: true });
    expect(component.canCompleteIntake).toBe(true);
  });

  it('intakeStatusLabel returns "Intake Completed" for Completed status', () => {
    expect(component.intakeStatusLabel('Completed')).toBe('Intake Completed');
  });

  it('intakeStatusLabel returns "Intake Not Started" for null status', () => {
    expect(component.intakeStatusLabel(null)).toBe('Intake Not Started');
  });

  it('canCompleteIntake is false when isIntakeWindowOpen is false', () => {
    component.appointment = makeAppt({ intakeStatus: null, isIntakeWindowOpen: false });
    expect(component.canCompleteIntake).toBe(false);
  });

  it('canCompleteIntake is false when intakeStatus is Completed', () => {
    component.appointment = makeAppt({ intakeStatus: 'Completed', isIntakeWindowOpen: true });
    expect(component.canCompleteIntake).toBe(false);
  });
});
