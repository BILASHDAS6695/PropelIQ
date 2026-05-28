import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { DatePipe, NgClass } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { BadgeModule } from 'primeng/badge';
import { Popover, PopoverModule } from 'primeng/popover';
import { DividerModule } from 'primeng/divider';
import { TooltipModule } from 'primeng/tooltip';
import { NotificationService } from '../../../core/services/notification.service';
import { NOTIFICATION_ICONS } from '../../../core/models/notification.model';

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
            <i [class]="iconFor(n.type)" class="mt-1 text-primary"></i>
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
export class NotificationBellComponent implements OnInit {
  readonly svc = inject(NotificationService);

  ngOnInit(): void {
    this.svc.init();
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
}
