# Task 002: PrimeNG Installation & App Shell Layout

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-002 |
| **Epic** | EP-TECH |
| **Layer** | Frontend / UI |
| **Priority** | Critical |
| **Estimated Effort** | 3 hours |
| **Dependencies** | Task 001 |

## Objective

Install and configure PrimeNG as the UI framework with a base theme, then build the app shell layout containing a header toolbar, collapsible navigation sidebar, and main content area.

## Implementation Steps

### 1. Install PrimeNG & Dependencies

```bash
npm install primeng primeicons primeflex @primeng/themes
```

### 2. Configure PrimeNG in app.config.ts

**File:** `src/app/app.config.ts`

```typescript
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeng/themes/aura';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    provideAnimationsAsync(),
    providePrimeNG({
      theme: {
        preset: Aura,
        options: {
          prefix: 'p',
          darkModeSelector: '.dark-mode',
        },
      },
    }),
  ],
};
```

### 3. Import PrimeNG Styles

**File:** `src/styles.scss`

```scss
// PrimeNG
@import 'primeicons/primeicons.css';
@import 'primeflex/primeflex.css';

// Custom theme overrides
:root {
  --primary-color: #6366f1;
  --primary-color-text: #ffffff;
  --surface-ground: #f8fafc;
  --surface-card: #ffffff;
  --surface-border: #e2e8f0;
  --text-color: #0f172a;
  --text-color-secondary: #64748b;
  --font-family: 'Inter', -apple-system, BlinkMacSystemFont, sans-serif;
}

body {
  margin: 0;
  font-family: var(--font-family);
  background: var(--surface-ground);
  color: var(--text-color);
}
```

### 4. Create App Shell Layout Component

**File:** `src/app/layout/app-layout.component.ts`

```typescript
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
        <app-header (menuToggle)="sidebarCollapsed.update(v => !v)" />
        <main class="app-content">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
  styles: [`
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
  `],
})
export class AppLayoutComponent {
  sidebarCollapsed = signal(false);
}
```

### 5. Create Header Component

**File:** `src/app/layout/header/app-header.component.ts`

```typescript
import { Component, output } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { ToolbarModule } from 'primeng/toolbar';
import { AvatarModule } from 'primeng/avatar';
import { BadgeModule } from 'primeng/badge';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [ButtonModule, ToolbarModule, AvatarModule, BadgeModule],
  template: `
    <p-toolbar styleClass="app-header">
      <div class="p-toolbar-group-start">
        <p-button
          icon="pi pi-bars"
          [text]="true"
          (onClick)="menuToggle.emit()"
          aria-label="Toggle sidebar"
        />
      </div>
      <div class="p-toolbar-group-end">
        <p-button icon="pi pi-search" [text]="true" [rounded]="true" aria-label="Search" />
        <p-button icon="pi pi-bell" [text]="true" [rounded]="true" badge="3" aria-label="Notifications" />
        <p-avatar label="SC" shape="circle" styleClass="ml-2" />
      </div>
    </p-toolbar>
  `,
  styles: [`
    :host ::ng-deep .app-header {
      border: none;
      border-bottom: 1px solid var(--surface-border);
      border-radius: 0;
      padding: 0.5rem 1.5rem;
    }
  `],
})
export class AppHeaderComponent {
  menuToggle = output<void>();
}
```

### 6. Create Sidebar Component

**File:** `src/app/layout/sidebar/app-sidebar.component.ts`

```typescript
import { Component, input, output } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { NgFor, NgClass } from '@angular/common';

interface NavItem {
  label: string;
  icon: string;
  route: string;
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, NgFor, NgClass],
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
  styles: [`
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
    .sidebar.collapsed { width: 64px; }
    .sidebar-brand {
      padding: 1rem;
      display: flex;
      align-items: center;
      gap: 0.75rem;
      font-weight: 700;
      color: var(--primary-color);
    }
    .brand-icon { font-size: 1.25rem; }
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
    .nav-item:hover { background: var(--surface-ground); color: var(--text-color); }
    .nav-item.active {
      background: color-mix(in srgb, var(--primary-color) 10%, transparent);
      color: var(--primary-color);
      font-weight: 500;
    }
  `],
})
export class AppSidebarComponent {
  collapsed = input(false);
  toggleCollapse = output<boolean>();

  navItems: NavItem[] = [
    { label: 'Dashboard', icon: 'pi-home', route: '/dashboard' },
    { label: 'Book Appointment', icon: 'pi-calendar-plus', route: '/booking' },
    { label: 'My Appointments', icon: 'pi-calendar', route: '/booking/appointments' },
    { label: 'Intake', icon: 'pi-comments', route: '/intake' },
    { label: 'Documents', icon: 'pi-file', route: '/clinical' },
    { label: 'Admin', icon: 'pi-cog', route: '/admin' },
  ];
}
```

### 7. Wire Layout into App Component

**File:** `src/app/app.component.ts`

```typescript
import { Component } from '@angular/core';
import { AppLayoutComponent } from './layout/app-layout.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [AppLayoutComponent],
  template: `<app-layout />`,
})
export class AppComponent {}
```

## Acceptance Criteria

- [ ] PrimeNG installed and configured with Aura theme preset
- [ ] PrimeIcons and PrimeFlex available globally
- [ ] Custom CSS variables align with design system tokens (Indigo primary `#6366F1`)
- [ ] App shell renders: sidebar + header + content area
- [ ] Sidebar is collapsible via hamburger button in header
- [ ] Sidebar navigation items use `routerLink` with active state styling
- [ ] Header contains menu toggle, search, notifications, and avatar
- [ ] Layout is responsive (sidebar collapses on mobile)
- [ ] `ng build` succeeds with zero errors

## Verification

```bash
ng serve  # Visual inspection of layout
ng build --configuration production
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| TR-003 | PrimeNG configured |
| US-002 AC-2 | App shell with header, sidebar, content |
| US-002 AC-6 | PrimeNG installed with base theme |
