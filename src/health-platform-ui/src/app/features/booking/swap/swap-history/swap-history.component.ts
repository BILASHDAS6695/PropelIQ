import { CommonModule } from '@angular/common';
import { Component, inject, Input, OnChanges, signal } from '@angular/core';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { SwapService } from '../../../../core/services/swap.service';
import {
  SwapHistoryItemDto,
  SwapRequestStatus,
} from '../../../../core/models/booking.models';

type TagSeverity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast';

const STATUS_SEVERITY: Record<SwapRequestStatus, TagSeverity> = {
  [SwapRequestStatus.Pending]:   'info',
  [SwapRequestStatus.Accepted]:  'success',
  [SwapRequestStatus.Declined]:  'danger',
  [SwapRequestStatus.Cancelled]: 'secondary',
  [SwapRequestStatus.Expired]:   'warn',
};

@Component({
  selector: 'app-swap-history',
  standalone: true,
  imports: [CommonModule, SkeletonModule, TagModule],
  template: `
    <div class="swap-history mt-3">
      <div class="font-semibold text-sm mb-2">Swap History</div>

      @if (loading()) {
        <p-skeleton height="2rem" />
      } @else if (history().length === 0) {
        <p class="text-sm text-color-secondary m-0">No swap requests for this appointment.</p>
      } @else {
        <ul class="list-none m-0 p-0" role="list" aria-label="Swap request history">
          @for (item of history(); track item.swapRequestId) {
            <li
              class="flex align-items-center justify-content-between py-2 border-bottom-1 surface-border gap-2"
              role="listitem"
            >
              <div class="text-sm flex-1 min-w-0">
                <span>
                  Offered
                  <strong>{{ item.requesterSlotTime | date: 'h:mm a, MMM d' }}</strong>
                  for
                  <strong>{{ item.targetSlotTime | date: 'h:mm a, MMM d' }}</strong>
                </span>
              </div>
              <p-tag
                [value]="item.status"
                [severity]="statusSeverity(item.status)"
                styleClass="flex-shrink-0"
              />
            </li>
          }
        </ul>
      }
    </div>
  `,
})
export class SwapHistoryComponent implements OnChanges {
  @Input({ required: true }) appointmentId!: string;

  private readonly swapSvc = inject(SwapService);

  readonly history = signal<SwapHistoryItemDto[]>([]);
  readonly loading = signal(true);

  ngOnChanges(): void {
    this.loading.set(true);
    this.swapSvc.getSwapHistory(this.appointmentId).subscribe({
      next: (data) => {
        this.history.set(data);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  statusSeverity(status: SwapRequestStatus): TagSeverity {
    return STATUS_SEVERITY[status] ?? 'secondary';
  }
}
