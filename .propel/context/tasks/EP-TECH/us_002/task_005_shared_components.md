# Task 005: Shared UI Components — Loading Spinner & Toast Notifications

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-002 |
| **Epic** | EP-TECH |
| **Layer** | Frontend / Shared |
| **Priority** | High |
| **Estimated Effort** | 2 hours |
| **Dependencies** | Task 001, Task 002 |

## Objective

Create reusable shared UI components: a loading spinner with overlay mode, and a toast notification service with auto-dismiss support — both using PrimeNG primitives and Angular signals.

## Implementation Steps

### 1. Create Loading Service

**File:** `src/app/shared/services/loading.service.ts`

```typescript
import { Injectable, signal, computed } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class LoadingService {
  private readonly activeRequests = signal(0);

  readonly isLoading = computed(() => this.activeRequests() > 0);

  show(): void {
    this.activeRequests.update(count => count + 1);
  }

  hide(): void {
    this.activeRequests.update(count => Math.max(0, count - 1));
  }

  reset(): void {
    this.activeRequests.set(0);
  }
}
```

### 2. Create Loading Spinner Component

**File:** `src/app/shared/components/loading-spinner/loading-spinner.component.ts`

```typescript
import { Component, inject } from '@angular/core';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { LoadingService } from '../../services/loading.service';

@Component({
  selector: 'app-loading-spinner',
  standalone: true,
  imports: [ProgressSpinnerModule],
  template: `
    @if (loadingService.isLoading()) {
      <div class="loading-overlay" role="status" aria-label="Loading">
        <p-progressSpinner
          strokeWidth="4"
          animationDuration="0.8s"
          styleClass="loading-spinner"
        />
      </div>
    }
  `,
  styles: [`
    .loading-overlay {
      position: fixed;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      background: rgba(255, 255, 255, 0.7);
      z-index: 9999;
      backdrop-filter: blur(2px);
    }
  `],
})
export class LoadingSpinnerComponent {
  readonly loadingService = inject(LoadingService);
}
```

### 3. Create Toast Notification Service

**File:** `src/app/shared/services/toast.service.ts`

```typescript
import { Injectable } from '@angular/core';
import { MessageService } from 'primeng/api';

export type ToastSeverity = 'success' | 'info' | 'warn' | 'error';

export interface ToastOptions {
  severity: ToastSeverity;
  summary: string;
  detail?: string;
  life?: number;
  sticky?: boolean;
}

@Injectable({ providedIn: 'root' })
export class ToastService {
  constructor(private readonly messageService: MessageService) {}

  show(options: ToastOptions): void {
    this.messageService.add({
      severity: options.severity,
      summary: options.summary,
      detail: options.detail,
      life: options.life ?? 4000,
      sticky: options.sticky ?? false,
    });
  }

  success(summary: string, detail?: string): void {
    this.show({ severity: 'success', summary, detail });
  }

  info(summary: string, detail?: string): void {
    this.show({ severity: 'info', summary, detail });
  }

  warn(summary: string, detail?: string): void {
    this.show({ severity: 'warn', summary, detail });
  }

  error(summary: string, detail?: string): void {
    this.show({ severity: 'error', summary, detail, life: 6000 });
  }

  clear(): void {
    this.messageService.clear();
  }
}
```

### 4. Register MessageService & Add Toast to Layout

Update `app.config.ts` providers:

```typescript
import { MessageService } from 'primeng/api';

// Add to providers array:
MessageService,
```

Update `app.component.ts`:

```typescript
import { Component } from '@angular/core';
import { AppLayoutComponent } from './layout/app-layout.component';
import { ToastModule } from 'primeng/toast';
import { LoadingSpinnerComponent } from './shared/components/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [AppLayoutComponent, ToastModule, LoadingSpinnerComponent],
  template: `
    <p-toast position="top-right" />
    <app-loading-spinner />
    <app-layout />
  `,
})
export class AppComponent {}
```

### 5. Create Shared Barrel Export

**File:** `src/app/shared/index.ts`

```typescript
// Components
export { LoadingSpinnerComponent } from './components/loading-spinner/loading-spinner.component';

// Services
export { LoadingService } from './services/loading.service';
export { ToastService } from './services/toast.service';
```

### 6. Create Loading Interceptor (Optional Enhancement)

**File:** `src/app/core/interceptors/loading.interceptor.ts`

```typescript
import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { LoadingService } from '../../shared/services/loading.service';

export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  const loadingService = inject(LoadingService);

  // Skip loading indicator for background requests
  if (req.headers.has('X-Skip-Loading')) {
    return next(req);
  }

  loadingService.show();
  return next(req).pipe(finalize(() => loadingService.hide()));
};
```

Register in `app.config.ts` interceptors array:

```typescript
provideHttpClient(withInterceptors([authInterceptor, errorInterceptor, loadingInterceptor])),
```

## Acceptance Criteria

- [ ] `LoadingService` tracks concurrent requests via signal counter
- [ ] `LoadingSpinnerComponent` renders full-screen overlay with PrimeNG spinner
- [ ] Spinner only visible when `isLoading()` is true
- [ ] `ToastService` wraps PrimeNG `MessageService` with typed severity methods
- [ ] Toast auto-dismisses (4s default, 6s for errors)
- [ ] `app-loading-spinner` and `p-toast` rendered at app root level
- [ ] `loadingInterceptor` auto-shows/hides spinner for HTTP requests
- [ ] `X-Skip-Loading` header skips the loading indicator
- [ ] Spinner has `role="status"` and `aria-label` for accessibility
- [ ] All components tree-shakeable and standalone

## Verification

```bash
ng build --configuration production  # No compile errors
ng serve  # Trigger toast via console: inject(ToastService).success('Test')
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-002 AC-5 | Loading spinner, toast notifications |
| TR-001 | Standalone components |
| TR-002 | Signals-based state |
