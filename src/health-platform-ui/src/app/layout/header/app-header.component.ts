import { Component, output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { ToolbarModule } from 'primeng/toolbar';
import { AvatarModule } from 'primeng/avatar';
import { NotificationBellComponent } from '../../shared/components/notification-bell/notification-bell.component';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [ButtonModule, ToolbarModule, AvatarModule, NotificationBellComponent],
  template: `
    <p-toolbar styleClass="app-header">
      <ng-template pTemplate="start">
        <p-button
          icon="pi pi-bars"
          [text]="true"
          (onClick)="menuToggle.emit()"
          aria-label="Toggle sidebar"
        />
      </ng-template>
      <ng-template pTemplate="end">
        <p-button icon="pi pi-search" [text]="true" [rounded]="true" aria-label="Search" />
        <app-notification-bell />
        <p-avatar label="SC" shape="circle" styleClass="ml-2" />
      </ng-template>
    </p-toolbar>
  `,
  styles: [
    `
      :host ::ng-deep .app-header {
        border: none;
        border-bottom: 1px solid var(--surface-border);
        border-radius: 0;
        padding: 0.5rem 1.5rem;
      }
    `,
  ],
})
export class AppHeaderComponent {
  menuToggle = output<void>();
}
