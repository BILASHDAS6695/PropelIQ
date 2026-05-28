import {
  ChangeDetectionStrategy,
  Component,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { DatePipe, NgClass } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { BadgeModule } from 'primeng/badge';
import { Popover, PopoverModule } from 'primeng/popover';
import { DividerModule } from 'primeng/divider';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { NotificationService } from '../../../core/services/notification.service';
import { NOTIFICATION_ICONS } from '../../../core/models/notification.model';
import { SwapService } from '../../../core/services/swap.service';

const SWAP_URL_RE = /\/appointments\/([0-9a-f-]+)\/swap-requests\/([0-9a-f-]+)$/i;

function parseSwapIds(
  actionUrl: string | null,
): { appointmentId: string; swapRequestId: string } | null {
  if (!actionUrl) return null;
  const m = SWAP_URL_RE.exec(actionUrl);
  if (!m) return null;
  return { appointmentId: m[1], swapRequestId: m[2] };
}

@Component({
  selector: 'app-notification-bell',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    NgClass,
    RouterModule,
    ButtonModule,
    BadgeModule,
    PopoverModule,
    DividerModule,
    ToastModule,
    TooltipModule,
  ],
  template: `
    <p-button
      icon="pi pi-bell"
      [text]="true"
      [rounded]="true"
      [badge]="svc.badgeLabel()"
      badgeSeverity="danger"
      aria-label="Notifications"
      aria-haspopup="true"
      (onClick)="panel.toggle($event)"
    />

    <p-popover #panel [style]="{ width: '360px' }">
      <!-- Header row -->
      <div class="flex justify-content-between align-items-center px-3 pt-2 pb-1">
        <span class="font-semibold text-base">Notifications</span>
        @if (svc.unread() > 0) {
          <p-button
            label="Mark all read"
            [text]="true"
            size="small"
            (onClick)="markAllRead(panel)"
          />
        }
      </div>
      <p-divider styleClass="my-1" />

      <!-- Notification list -->
      <ul class="list-none m-0 p-0 notification-list" role="list">
        @for (n of svc.items(); track n.id) {
          <li
            [ngClass]="{ unread: !n.isRead }"
            class="notification-item flex gap-2 px-3 py-2 cursor-pointer"
            [attr.aria-label]="n.title"
            tabindex="0"
            role="listitem"
            (click)="onItemClick(n.id, n.actionUrl, panel)"
            (keyup.enter)="onItemClick(n.id, n.actionUrl, panel)"
          >
            <i [class]="iconFor(n.type)" class="mt-1 text-primary" aria-hidden="true"></i>
            <div class="flex-1 min-w-0">
              <p
                class="m-0 font-medium text-sm white-space-nowrap overflow-hidden text-overflow-ellipsis"
              >
                {{ n.title }}
              </p>
              <p
                class="m-0 text-color-secondary text-xs mt-1 white-space-nowrap overflow-hidden text-overflow-ellipsis"
              >
                {{ n.message }}
              </p>
              <span class="text-color-secondary text-xs">
                {{ n.sentAt | date: 'short' }}
              </span>

              <!-- Inline swap actions — only for pending SwapRequest notifications -->
              @if (n.type === 'SwapRequest' && parseSwapIds(n.actionUrl)) {
                <div class="flex gap-2 mt-2">
                  <p-button
                    label="Accept"
                    severity="success"
                    size="small"
                    icon="pi pi-check"
                    [loading]="respondingId() === n.id"
                    [disabled]="respondingId() !== null && respondingId() !== n.id"
                    (onClick)="respondToSwap($event, n.id, n.actionUrl, true, panel)"
                    aria-label="Accept swap request"
                  />
                  <p-button
                    label="Decline"
                    severity="danger"
                    size="small"
                    icon="pi pi-times"
                    [outlined]="true"
                    [loading]="respondingId() === n.id"
                    [disabled]="respondingId() !== null && respondingId() !== n.id"
                    (onClick)="respondToSwap($event, n.id, n.actionUrl, false, panel)"
                    aria-label="Decline swap request"
                  />
                </div>
              }
            </div>
            @if (!n.isRead) {
              <span
                class="unread-dot flex-shrink-0 mt-1"
                aria-label="Unread"
                pTooltip="Unread"
                tooltipPosition="left"
              ></span>
            }
          </li>
        } @empty {
          <li class="px-3 py-4 text-center text-color-secondary text-sm" role="listitem">
            No notifications
          </li>
        }
      </ul>
      <p-divider styleClass="my-1" />
      <div class="flex justify-content-center px-3 pb-2">
        <a
          routerLink="/notification-preferences"
          class="pref-link text-xs text-color-secondary flex align-items-center gap-1"
          (click)="panel.hide()"
          aria-label="Manage notification preferences"
        >
          <i class="pi pi-sliders-h"></i> Preferences
        </a>
      </div>
    </p-popover>
  `,
  styles: [
    `
      :host ::ng-deep .p-popover-content {
        padding: 0;
      }
      .notification-list {
        max-height: 420px;
        overflow-y: auto;
      }
      .notification-item {
        border-bottom: 1px solid var(--p-content-border-color);
        transition: background 0.15s;
      }
      .notification-item:hover,
      .notification-item:focus {
        background: var(--p-content-hover-background);
        outline: none;
      }
      .notification-item.unread {
        background: color-mix(in srgb, var(--p-blue-100) 40%, transparent);
      }
      .unread-dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        background: var(--p-primary-color);
      }
      .pref-link {
        text-decoration: none;
        transition: color 0.15s;
      }
      .pref-link:hover {
        color: var(--p-primary-color);
      }
    `,
  ],
})
export class NotificationBellComponent implements OnInit, OnDestroy {
  readonly svc = inject(NotificationService);
  private readonly swapSvc = inject(SwapService);
  private readonly toast = inject(MessageService);

  /** Tracks which notification ID has an in-flight respond request. */
  readonly respondingId = signal<string | null>(null);

  /** Expose module-level fn to template (ChangeDetectionStrategy.OnPush safe). */
  protected readonly parseSwapIds = parseSwapIds;

  private readonly focusHandler = (): void => this.svc.loadFromApi();

  ngOnInit(): void {
    this.svc.init();
    window.addEventListener('focus', this.focusHandler);
  }

  ngOnDestroy(): void {
    window.removeEventListener('focus', this.focusHandler);
  }

  iconFor(type: string): string {
    return NOTIFICATION_ICONS[type as keyof typeof NOTIFICATION_ICONS] ?? 'pi pi-bell';
  }

  onItemClick(id: string, _actionUrl: string | null, panel: Popover): void {
    this.svc.markRead(id);
    panel.hide();
  }

  markAllRead(panel: Popover): void {
    this.svc.markRead();
    panel.hide();
  }

  respondToSwap(
    event: Event,
    notificationId: string,
    actionUrl: string | null,
    accept: boolean,
    panel: Popover,
  ): void {
    event.stopPropagation();

    const ids = parseSwapIds(actionUrl);
    if (!ids) {
      this.toast.add({
        severity: 'warn',
        summary: 'Action unavailable',
        detail: 'Unable to identify the swap request. Please refresh.',
        life: 5_000,
      });
      return;
    }

    this.respondingId.set(notificationId);

    this.swapSvc.respondToSwapRequest(ids.appointmentId, ids.swapRequestId, accept).subscribe({
      next: () => {
        this.respondingId.set(null);
        this.svc.markRead(notificationId);
        this.svc.loadFromApi();
        panel.hide();
        this.toast.add({
          severity: 'success',
          summary: accept ? 'Swap accepted' : 'Swap declined',
          detail: accept
            ? 'Your appointment time has been updated.'
            : 'The requester has been notified.',
          life: 5_000,
        });
      },
      error: (err) => {
        this.respondingId.set(null);
        const detail =
          err?.status === 409
            ? 'This swap request has already expired or been actioned.'
            : 'Something went wrong. Please try again.';
        this.toast.add({ severity: 'error', summary: 'Action failed', detail, life: 6_000 });
      },
    });
  }
}
