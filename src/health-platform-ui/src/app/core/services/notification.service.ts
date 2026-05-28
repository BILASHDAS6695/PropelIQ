import { computed, inject, Injectable, OnDestroy, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { MessageService } from 'primeng/api';
import { Subscription } from 'rxjs';
import {
  GetNotificationsResult,
  HIGH_PRIORITY_TYPES,
  Notification,
} from '../models/notification.model';
import { NotificationSignalRService } from './notification-signalr.service';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class NotificationService implements OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly signalr = inject(NotificationSignalRService);
  private readonly toast = inject(MessageService);

  private readonly _items = signal<Notification[]>([]);
  private readonly _unread = signal<number>(0);

  readonly items = this._items.asReadonly();
  readonly unread = this._unread.asReadonly();

  /** Badge label: "99+" when overflow, digit string when non-zero, undefined to hide. */
  readonly badgeLabel = computed<string | undefined>(() => {
    const count = this._unread();
    if (count <= 0) return undefined;
    return count > 99 ? '99+' : String(count);
  });

  private sub?: Subscription;
  private initialised = false;

  init(): void {
    if (this.initialised) return;
    this.initialised = true;

    this.loadFromApi();
    this.signalr.start();

    this.sub = this.signalr.received$.subscribe((payload) => {
      if (payload === null) {
        // Polling fallback — refresh from API
        this.loadFromApi();
        return;
      }
      // Prepend new notification, cap list at 20
      this._items.update((list) => [payload, ...list].slice(0, 20));
      if (!payload.isRead) {
        this._unread.update((n) => n + 1);
      }

      if (HIGH_PRIORITY_TYPES.includes(payload.type)) {
        this.toast.add({
          severity: 'warn',
          summary: payload.title,
          detail: payload.message,
          life: 8_000,
        });
      }
    });
  }

  loadFromApi(): void {
    this.http
      .get<GetNotificationsResult>(`${environment.apiUrl}/notifications`)
      .subscribe((res) => {
        this._items.set(res.items);
        this._unread.set(res.unreadCount);
      });
  }

  markRead(notificationId?: string): void {
    this.http
      .post<number>(`${environment.apiUrl}/notifications/mark-read`, {
        targetId: notificationId ?? null,
      })
      .subscribe(() => {
        if (notificationId) {
          this._items.update((list) =>
            list.map((n) => (n.id === notificationId ? { ...n, isRead: true } : n)),
          );
          this._unread.update((count) => Math.max(0, count - 1));
        } else {
          this._items.update((list) => list.map((n) => ({ ...n, isRead: true })));
          this._unread.set(0);
        }
      });
  }

  ngOnDestroy(): void {
    this.sub?.unsubscribe();
    this.signalr.stop();
  }
}
