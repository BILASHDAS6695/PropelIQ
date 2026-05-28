import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { MultiProviderDayComponent } from './multi-provider-day.component';

describe('MultiProviderDayComponent', () => {
  let fixture: ComponentFixture<MultiProviderDayComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MultiProviderDayComponent],
      providers: [provideHttpClient(), provideRouter([]), provideNoopAnimations(), MessageService],
    }).compileComponents();
    fixture = TestBed.createComponent(MultiProviderDayComponent);
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
