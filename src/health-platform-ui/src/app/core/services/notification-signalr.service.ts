import { inject, Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { Notification } from '../models/notification.model';
import { environment } from '../../../environments/environment';

/** Shape of the SignalR push payload (mirrors InAppNotificationPayload on the server). */
export type SignalRNotificationPayload = Notification;

@Injectable({ providedIn: 'root' })
export class NotificationSignalRService implements OnDestroy {
  private connection: signalR.HubConnection | null = null;
  private pollingTimer: ReturnType<typeof setInterval> | null = null;

  /**
   * Emits when a new notification arrives via SignalR.
   * Emits `null` as a sentinel when the polling fallback fires, triggering an
   * HTTP refresh in NotificationService.
   */
  readonly received$ = new Subject<SignalRNotificationPayload | null>();

  private readonly auth = inject(AuthService);

  private get hubUrl(): string {
    // Strip trailing '/api' to get the API base, then append the hub path.
    return `${environment.apiUrl.replace(/\/api$/, '')}/hubs/notifications`;
  }

  start(): void {
    if (this.connection) return;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, {
        accessTokenFactory: () => this.auth.getToken() ?? '',
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (ctx) =>
          // Exponential back-off: 2 s → 4 s → 8 s → 16 s → 30 s cap
          Math.min(1_000 * Math.pow(2, ctx.previousRetryCount + 1), 30_000),
      })
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    this.connection.on('Notification', (payload: SignalRNotificationPayload) => {
      this.received$.next(payload);
    });

    this.connection.onclose(() => {
      // Permanent disconnect → fall back to HTTP polling
      this.startPollingFallback();
    });

    this.connection
      .start()
      .then(() => this.stopPollingFallback())
      .catch(() => this.startPollingFallback());
  }

  stop(): void {
    this.stopPollingFallback();
    this.connection?.stop();
    this.connection = null;
  }

  ngOnDestroy(): void {
    this.stop();
  }

  private startPollingFallback(): void {
    if (this.pollingTimer) return;
    // Emit null every 30 s so NotificationService refreshes from the API
    this.pollingTimer = setInterval(() => this.received$.next(null), 30_000);
  }

  private stopPollingFallback(): void {
    if (this.pollingTimer) {
      clearInterval(this.pollingTimer);
      this.pollingTimer = null;
    }
  }
}
