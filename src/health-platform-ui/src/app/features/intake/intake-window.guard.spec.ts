import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { MessageService } from 'primeng/api';
import { IntakeWindowService } from '../../core/services/intake-window.service';

describe('intakeWindowGuard', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideRouter([]), MessageService, IntakeWindowService],
    });
  });

  it('IntakeWindowService should be available via DI', () => {
    const svc = TestBed.inject(IntakeWindowService);
    expect(svc).toBeTruthy();
  });
});
