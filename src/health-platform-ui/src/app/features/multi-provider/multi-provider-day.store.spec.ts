import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { MessageService } from 'primeng/api';
import { MultiProviderDayStore } from './multi-provider-day.store';

describe('MultiProviderDayStore', () => {
  let store: InstanceType<typeof MultiProviderDayStore>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [MultiProviderDayStore, provideHttpClient(), MessageService],
    });
    store = TestBed.inject(MultiProviderDayStore);
  });

  it('should have correct initial state', () => {
    expect(store.selectedProviderIds()).toEqual([]);
    expect(store.allProviders()).toEqual([]);
    expect(store.isLoading()).toBe(false);
  });

  it('toggleProvider: adds provider when not selected', () => {
    store.toggleProvider('prov-001');
    expect(store.selectedProviderIds()).toContain('prov-001');
  });

  it('toggleProvider: removes provider when already selected', () => {
    store.toggleProvider('prov-001');
    store.toggleProvider('prov-001');
    expect(store.selectedProviderIds()).not.toContain('prov-001');
  });

  it('navigateDay: advances currentDate by 1 when direction is next', () => {
    const before = store.currentDate().getDate();
    store.navigateDay('next');
    expect(store.currentDate().getDate()).toBe(before + 1);
  });
});
