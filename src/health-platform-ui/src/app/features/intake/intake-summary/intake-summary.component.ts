import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { DividerModule } from 'primeng/divider';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';

import { IntakeSummaryDto, IntakeStatus } from '../../../core/models/intake.models';
import { IntakeService } from '../../../core/services/intake.service';
import { ToastService } from '../../../shared/services/toast.service';

type TagSeverity = 'warn' | 'success' | 'info' | 'danger' | 'secondary' | 'contrast';

const STATUS_SEVERITY: Record<IntakeStatus, TagSeverity> = {
  Draft: 'warn',
  Completed: 'success',
  ReviewedByProvider: 'info',
  Orphaned: 'danger',
};

const STATUS_LABEL: Record<IntakeStatus, string> = {
  Draft: 'Draft',
  Completed: 'Completed',
  ReviewedByProvider: 'Reviewed by Provider',
  Orphaned: 'Orphaned',
};

@Component({
  selector: 'app-intake-summary',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonModule, CardModule, DatePipe, DividerModule, SkeletonModule, TagModule],
  template: `
    <div class="intake-summary-container p-4 max-w-3xl mx-auto">
      @if (loading()) {
        <p-card>
          <p-skeleton height="2rem" styleClass="mb-3" />
          <p-skeleton height="1rem" styleClass="mb-2" />
          <p-skeleton height="1rem" styleClass="mb-2" />
          <p-skeleton height="1rem" />
        </p-card>
      } @else if (summary()) {
        <p-card>
          <ng-template pTemplate="header">
            <div class="flex align-items-center justify-content-between p-3">
              <h2 class="m-0 text-xl font-semibold">Intake Summary</h2>
              <p-tag [value]="statusLabel()" [severity]="statusSeverity()" />
            </div>
          </ng-template>

          <!-- Section 1: Chief Complaint -->
          <section class="mb-4">
            <h3 class="text-sm font-semibold text-500 uppercase mb-2">Chief Complaint</h3>
            <p class="m-0">{{ summary()!.data?.chiefComplaint || '—' }}</p>
          </section>

          <p-divider />

          <!-- Section 2: Symptoms & Duration -->
          <section class="mb-4">
            <h3 class="text-sm font-semibold text-500 uppercase mb-2">Symptoms &amp; Duration</h3>
            <p class="m-0 mb-1">
              <span class="font-medium">Symptoms: </span>
              {{ summary()!.data?.symptoms?.join(', ') || '—' }}
            </p>
            <p class="m-0">
              <span class="font-medium">Duration: </span>
              {{ summary()!.data?.duration || '—' }}
            </p>
          </section>

          <p-divider />

          <!-- Section 3: Severity -->
          <section class="mb-4">
            <h3 class="text-sm font-semibold text-500 uppercase mb-2">Severity</h3>
            <p class="m-0">{{ summary()!.data?.severity ?? '—' }} / 10</p>
          </section>

          <p-divider />

          <!-- Section 4: Medications -->
          <section class="mb-4">
            <h3 class="text-sm font-semibold text-500 uppercase mb-2">Current Medications</h3>
            <p class="m-0">{{ summary()!.data?.medications?.join(', ') || 'None reported' }}</p>
          </section>

          <p-divider />

          <!-- Section 5: Allergies -->
          <section class="mb-4">
            <h3 class="text-sm font-semibold text-500 uppercase mb-2">Allergies</h3>
            <p class="m-0">{{ summary()!.data?.allergies?.join(', ') || 'None reported' }}</p>
          </section>

          <p-divider />

          <!-- Section 6: Medical History -->
          <section class="mb-4">
            <h3 class="text-sm font-semibold text-500 uppercase mb-2">Medical History</h3>
            <p class="m-0" style="white-space: pre-wrap">
              {{ summary()!.data?.medicalHistory || '—' }}
            </p>
          </section>

          <!-- Footer: timestamps + actions -->
          <ng-template pTemplate="footer">
            <div class="flex flex-column gap-2 p-2">
              @if (summary()!.completedAt) {
                <p class="m-0 text-sm text-500">
                  Submitted: {{ summary()!.completedAt | date: 'medium' }}
                </p>
              }
              @if (summary()!.reviewedAt) {
                <p class="m-0 text-sm text-500">
                  Reviewed: {{ summary()!.reviewedAt | date: 'medium' }}
                </p>
              }

              <div class="flex gap-2 mt-2">
                <p-button
                  label="Back"
                  severity="secondary"
                  icon="pi pi-arrow-left"
                  (onClick)="router.navigate(['/intake'])"
                />
                @if (summary()!.status === 'Completed') {
                  <p-button
                    label="Mark as Reviewed"
                    icon="pi pi-check"
                    [loading]="marking()"
                    (onClick)="markReviewed()"
                  />
                }
              </div>
            </div>
          </ng-template>
        </p-card>
      } @else {
        <p-card>
          <div class="text-center p-4">
            <p class="text-500">Intake record not found.</p>
            <p-button label="Back" severity="secondary" (onClick)="router.navigate(['/intake'])" />
          </div>
        </p-card>
      }
    </div>
  `,
})
export class IntakeSummaryComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly intakeService = inject(IntakeService);
  private readonly toast = inject(ToastService);
  readonly router = inject(Router);

  readonly summary = signal<IntakeSummaryDto | null>(null);
  readonly loading = signal(true);
  readonly marking = signal(false);

  readonly statusSeverity = signal<TagSeverity>('secondary');
  readonly statusLabel = signal<string>('');

  async ngOnInit(): Promise<void> {
    const appointmentId = this.route.snapshot.paramMap.get('appointmentId') ?? '';
    try {
      const data = await firstValueFrom(this.intakeService.getIntakeSummary(appointmentId));
      this.summary.set(data);
      this.statusSeverity.set(STATUS_SEVERITY[data.status]);
      this.statusLabel.set(STATUS_LABEL[data.status]);
    } catch {
      this.toast.error('Load failed', 'Unable to load intake summary.');
    } finally {
      this.loading.set(false);
    }
  }

  async markReviewed(): Promise<void> {
    const s = this.summary();
    if (!s) return;
    this.marking.set(true);
    try {
      await firstValueFrom(this.intakeService.markReviewed(s.appointmentId));
      this.summary.set({ ...s, status: 'ReviewedByProvider' });
      this.statusSeverity.set(STATUS_SEVERITY['ReviewedByProvider']);
      this.statusLabel.set(STATUS_LABEL['ReviewedByProvider']);
      this.toast.success('Reviewed', 'Intake marked as reviewed by provider.');
    } catch {
      this.toast.error('Action failed', 'Unable to mark intake as reviewed.');
    } finally {
      this.marking.set(false);
    }
  }
}
