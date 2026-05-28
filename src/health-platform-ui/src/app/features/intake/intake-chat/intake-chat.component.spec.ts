import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { IntakeChatComponent } from './intake-chat.component';
import { IntakeChatStore } from '../intake-chat.store';

describe('IntakeChatComponent', () => {
  let fixture: ComponentFixture<IntakeChatComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IntakeChatComponent],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        IntakeChatStore,
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(IntakeChatComponent);
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('should expose a non-empty quickReplies list', () => {
    expect(fixture.componentInstance['quickReplies'].length).toBeGreaterThan(0);
  });

  it('should have retryLast method on the injected store', () => {
    const store = TestBed.inject(IntakeChatStore);
    expect(typeof store.retryLast).toBe('function');
  });
});
