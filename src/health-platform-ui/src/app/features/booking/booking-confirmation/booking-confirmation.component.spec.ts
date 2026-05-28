import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { MessageService } from 'primeng/api';
import { BookingConfirmationComponent } from './booking-confirmation.component';

describe('BookingConfirmationComponent', () => {
  let fixture: ComponentFixture<BookingConfirmationComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [BookingConfirmationComponent],
      providers: [provideHttpClient(), MessageService],
    }).compileComponents();
    fixture = TestBed.createComponent(BookingConfirmationComponent);
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
