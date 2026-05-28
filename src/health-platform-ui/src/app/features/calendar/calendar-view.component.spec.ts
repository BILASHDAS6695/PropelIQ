import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { CalendarViewComponent } from './calendar-view.component';

describe('CalendarViewComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CalendarViewComponent],
      providers: [provideHttpClient(), provideRouter([]), provideNoopAnimations(), MessageService],
    }).compileComponents();
  });

  it('should create', () => {
    const fixture = TestBed.createComponent(CalendarViewComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });
});
