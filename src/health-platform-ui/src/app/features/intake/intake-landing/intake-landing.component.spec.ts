import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { IntakeLandingComponent } from './intake-landing.component';
import { IntakeFormStore } from '../intake-form.store';
import { IntakeChatStore } from '../intake-chat.store';

describe('IntakeLandingComponent', () => {
  let fixture: ComponentFixture<IntakeLandingComponent>;

  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [IntakeLandingComponent],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        IntakeFormStore,
        IntakeChatStore,
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(IntakeLandingComponent);
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should initialise at step 1', () => {
    fixture.detectChanges();
    expect(fixture.componentInstance['currentStep']()).toBe(1);
  });

  it('should advance to step 2 via nextStep()', () => {
    fixture.detectChanges();
    fixture.componentInstance['nextStep']();
    expect(fixture.componentInstance['currentStep']()).toBe(2);
  });
});
