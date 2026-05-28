import { Component, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

interface NavItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive],
  template: `
    <aside class="sidebar" [class.collapsed]="collapsed()">
      <div class="sidebar-brand">
        <span class="brand-icon pi pi-heart-fill"></span>
        @if (!collapsed()) {
          <span class="brand-text">HealthPlatform</span>
        }
      </div>
      <nav class="sidebar-nav">
        @for (item of navItems; track item.route) {
          <a
            class="nav-item"
            [routerLink]="item.route"
            routerLinkActive="active"
            [attr.aria-label]="item.label"
          >
            <i [class]="'pi ' + item.icon"></i>
            @if (!collapsed()) {
              <span class="nav-label">{{ item.label }}</span>
            }
          </a>
        }
      </nav>
    </aside>
  `,
  styles: [
    `
      .sidebar {
        width: 240px;
        height: 100vh;
        background: var(--surface-card);
        border-right: 1px solid var(--surface-border);
        display: flex;
        flex-direction: column;
        transition: width 0.2s ease;
        flex-shrink: 0;
      }
      .sidebar.collapsed {
        width: 64px;
      }
      .sidebar-brand {
        padding: 1rem;
        display: flex;
        align-items: center;
        gap: 0.75rem;
        font-weight: 700;
        color: var(--primary-color);
      }
      .brand-icon {
        font-size: 1.25rem;
      }
      .sidebar-nav {
        flex: 1;
        padding: 0.5rem;
        display: flex;
        flex-direction: column;
        gap: 2px;
      }
      .nav-item {
        display: flex;
        align-items: center;
        gap: 0.75rem;
        padding: 0.625rem 0.75rem;
        border-radius: 6px;
        text-decoration: none;
        color: var(--text-color-secondary);
        font-size: 0.875rem;
        transition: all 0.15s;
      }
      .nav-item:hover {
        background: var(--surface-ground);
        color: var(--text-color);
      }
      .nav-item.active {
        background: color-mix(in srgb, var(--primary-color) 10%, transparent);
        color: var(--primary-color);
        font-weight: 500;
      }
    `,
  ],
})
export class AppSidebarComponent {
  collapsed = input(false);
  toggleCollapse = output<boolean>();

  navItems: NavItem[] = [
    { label: 'Dashboard', icon: 'pi-home', route: '/dashboard' },
    { label: 'Book Appointment', icon: 'pi-calendar-plus', route: '/booking' },
    { label: 'My Appointments', icon: 'pi-calendar', route: '/booking/appointments' },
    { label: 'Calendar', icon: 'pi-calendar-times', route: '/booking/calendar' },
    { label: 'Intake', icon: 'pi-comments', route: '/intake' },
    { label: 'Documents', icon: 'pi-file', route: '/clinical' },
    { label: 'Notifications', icon: 'pi-sliders-h', route: '/notification-preferences' },
    { label: 'Admin', icon: 'pi-cog', route: '/admin' },
  ];
}
