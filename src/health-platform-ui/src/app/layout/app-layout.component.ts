import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AppHeaderComponent } from './header/app-header.component';
import { AppSidebarComponent } from './sidebar/app-sidebar.component';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, AppHeaderComponent, AppSidebarComponent],
  template: `
    <div class="app-layout" [class.sidebar-collapsed]="sidebarCollapsed()">
      <app-sidebar
        [collapsed]="sidebarCollapsed()"
        (toggleCollapse)="sidebarCollapsed.set($event)"
      />
      <div class="app-main">
        <app-header (menuToggle)="sidebarCollapsed.update((v) => !v)" />
        <main class="app-content">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
  styles: [
    `
      .app-layout {
        display: flex;
        height: 100vh;
        overflow: hidden;
      }
      .app-main {
        flex: 1;
        display: flex;
        flex-direction: column;
        overflow: hidden;
        min-width: 0;
      }
      .app-content {
        flex: 1;
        overflow-y: auto;
        padding: 24px;
        background: var(--surface-ground);
      }
    `,
  ],
})
export class AppLayoutComponent {
  sidebarCollapsed = signal(false);
}
