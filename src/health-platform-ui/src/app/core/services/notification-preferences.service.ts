import { inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { NotificationPreferences, DEFAULT_PREFERENCES } from '../models/notification.model';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';

@Injectable({ providedIn: 'root' })
export class NotificationPreferencesService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);

  private readonly _prefs = signal<NotificationPreferences>({ ...DEFAULT_PREFERENCES });
  readonly prefs = this._prefs.asReadonly();

  private get userId(): string {
    return this.auth.user()?.id ?? '';
  }

  async load(): Promise<void> {
    if (!this.userId) return;
    try {
      const data = await firstValueFrom(
        this.http.get<NotificationPreferences>(
          `${environment.apiUrl}/users/${this.userId}/notification-preferences`,
        ),
      );
      this._prefs.set(data);
    } catch {
      // Non-fatal: keep defaults
    }
  }

  async save(prefs: NotificationPreferences): Promise<void> {
    await firstValueFrom(
      this.http.put(`${environment.apiUrl}/users/${this.userId}/notification-preferences`, prefs),
    );
    this._prefs.set({ ...prefs });
  }
}
