import { provideHttpClient } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { MessageService } from 'primeng/api';
import { IntakeSummaryComponent } from './intake-summary.component';

describe('IntakeSummaryComponent', () => {
  let fixture: ComponentFixture<IntakeSummaryComponent>;
  let component: IntakeSummaryComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IntakeSummaryComponent],
      providers: [provideHttpClient(), provideRouter([]), provideNoopAnimations(), MessageService],
    }).compileComponents();

    fixture = TestBed.createComponent(IntakeSummaryComponent);
    component = fixture.componentInstance;
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should start in loading state', () => {
    expect(component.loading()).toBe(true);
    expect(component.summary()).toBeNull();
  });
});
