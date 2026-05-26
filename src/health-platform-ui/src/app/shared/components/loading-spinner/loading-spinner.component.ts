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
        <p-progressSpinner strokeWidth="4" animationDuration="0.8s" styleClass="loading-spinner" />
      </div>
    }
  `,
  styles: [
    `
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
    `,
  ],
})
export class LoadingSpinnerComponent {
  readonly loadingService = inject(LoadingService);
}
