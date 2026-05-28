import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { MessageService } from 'primeng/api';
import { CalendarStore } from './calendar.store';

describe('CalendarStore', () => {
  let store: InstanceType<typeof CalendarStore>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [CalendarStore, provideHttpClient(), MessageService],
    });
    store = TestBed.inject(CalendarStore);
  });

  it('should initialise with month view and today', () => {
    expect(store.viewMode()).toBe('month');
    const today = new Date();
    expect(store.currentDate().getDate()).toBe(today.getDate());
  });

  it('navigate(next) advances by one month in month view', () => {
    const before = store.currentDate().getMonth();
    store.navigate('next');
    expect(store.currentDate().getMonth()).toBe((before + 1) % 12);
  });

  it('navigate(prev) retreats by one month in month view', () => {
    const before = store.currentDate().getMonth();
    store.navigate('prev');
    const expected = before === 0 ? 11 : before - 1;
    expect(store.currentDate().getMonth()).toBe(expected);
  });

  it('goToToday resets currentDate to today', () => {
    store.navigate('next');
    store.goToToday();
    const today = new Date();
    expect(store.currentDate().getDate()).toBe(today.getDate());
    expect(store.currentDate().getMonth()).toBe(today.getMonth());
  });

  it('setViewMode changes the view mode', () => {
    store.setViewMode('week');
    expect(store.viewMode()).toBe('week');
  });
});
