import { computed } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';

export interface AuthUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  role: 'patient' | 'staff' | 'admin';
}

interface AuthState {
  user: AuthUser | null;
  isLoading: boolean;
}

const initialState: AuthState = {
  user: null,
  isLoading: false,
};

export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ user }) => ({
    isAuthenticated: computed(() => user() !== null),
    userRole: computed(() => user()?.role ?? null),
    userId: computed(() => user()?.id ?? null),
  })),
  withMethods((store) => ({
    setUser(user: AuthUser): void {
      patchState(store, { user });
    },
    clearUser(): void {
      patchState(store, { user: null });
    },
    setLoading(isLoading: boolean): void {
      patchState(store, { isLoading });
    },
  })),
);
