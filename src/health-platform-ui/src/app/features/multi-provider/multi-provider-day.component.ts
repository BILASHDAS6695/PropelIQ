import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { MultiProviderDayStore } from './multi-provider-day.store';
import { CalendarAppointmentDto } from '../../core/models/calendar.models';
import { BookingService } from '../../core/services/booking.service';
import { ToastService } from '../../shared/services/toast.service';

const DAY_START_HOUR = 8;
const DAY_END_HOUR = 18;
const PX_PER_MINUTE = 3;
const GRID_HEIGHT_PX = (DAY_END_HOUR - DAY_START_HOUR) * 60 * PX_PER_MINUTE; // 1800

type StatusSeverity = 'info' | 'success' | 'danger' | 'secondary' | 'warn' | 'contrast';

@Component({
  selector: 'app-multi-provider-day',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    DialogModule,
    InputTextModule,
    SkeletonModule,
    TagModule,
  ],
  styles: [
    `
      .mp-page {
        max-width: 100%;
        padding: 1rem;
      }
      .mp-grid-wrapper {
        overflow-x: auto;
        overflow-y: auto;
        max-height: 80vh;
        border: 1px solid var(--surface-border);
        border-radius: 8px;
        position: relative;
      }
      .mp-grid {
        display: grid;
        min-width: max-content;
      }
      .time-header-cell {
        position: sticky;
        top: 0;
        left: 0;
        z-index: 4;
        background: var(--surface-card);
        border-bottom: 1px solid var(--surface-border);
        border-right: 1px solid var(--surface-border);
        height: 48px;
        width: 72px;
      }
      .provider-header-cell {
        position: sticky;
        top: 0;
        z-index: 3;
        background: var(--surface-card);
        border-bottom: 2px solid var(--primary-color);
        border-right: 1px solid var(--surface-border);
        height: 48px;
        padding: 0.5rem 0.75rem;
        display: flex;
        flex-direction: column;
        justify-content: center;
        min-width: 160px;
      }
      .time-col {
        position: sticky;
        left: 0;
        z-index: 2;
        background: var(--surface-card);
        border-right: 1px solid var(--surface-border);
        width: 72px;
      }
      .time-cell {
        height: 45px;
        padding: 0 0.5rem;
        display: flex;
        align-items: flex-start;
        justify-content: flex-end;
        font-size: 0.7rem;
        color: var(--text-color-secondary);
        border-bottom: 1px solid var(--surface-border);
        padding-top: 4px;
      }
      .provider-col {
        position: relative;
        border-right: 1px solid var(--surface-border);
        min-width: 160px;
      }
      .slot-cell {
        position: absolute;
        left: 0;
        right: 0;
        height: 45px;
        border-bottom: 1px solid var(--surface-100);
        box-sizing: border-box;
      }
      .slot-available {
        background: var(--surface-50);
        cursor: pointer;
        transition: background 0.12s;
      }
      .slot-available:hover {
        background: color-mix(in srgb, var(--primary-color) 8%, transparent);
      }
      .slot-blocked {
        background: repeating-linear-gradient(
          45deg,
          transparent,
          transparent 4px,
          var(--surface-200) 4px,
          var(--surface-200) 8px
        );
        cursor: not-allowed;
      }
      .appt-block {
        position: absolute;
        left: 4px;
        right: 4px;
        border-radius: 4px;
        padding: 2px 6px;
        font-size: 0.75rem;
        overflow: hidden;
        cursor: grab;
        z-index: 1;
        border-left: 3px solid;
        background: var(--surface-card);
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
        transition: box-shadow 0.12s;
      }
      .appt-block:hover {
        box-shadow: 0 2px 8px rgba(0, 0, 0, 0.18);
      }
      .appt-block:active {
        cursor: grabbing;
      }
      .status-scheduled,
      .status-booked {
        border-color: #3b82f6;
        background: color-mix(in srgb, #3b82f6 10%, white);
      }
      .status-completed {
        border-color: #22c55e;
        background: color-mix(in srgb, #22c55e 10%, white);
      }
      .status-cancelled {
        border-color: #ef4444;
        background: color-mix(in srgb, #ef4444 10%, white);
      }
      .status-arrived,
      .status-inprogress {
        border-color: #f59e0b;
        background: color-mix(in srgb, #f59e0b 10%, white);
      }
      .status-noshow {
        border-color: #9ca3af;
        background: color-mix(in srgb, #9ca3af 10%, white);
      }
      .not-available-overlay {
        position: absolute;
        inset: 0;
        display: flex;
        align-items: center;
        justify-content: center;
        color: var(--text-color-secondary);
        font-size: 0.875rem;
        pointer-events: none;
        background: color-mix(in srgb, var(--surface-200) 40%, transparent);
      }
      @media print {
        .mp-header,
        .mp-selector,
        .mp-actions {
          display: none !important;
        }
        .mp-grid-wrapper {
          overflow: visible !important;
          max-height: none !important;
          border: none;
        }
        .appt-block {
          box-shadow: none !important;
          cursor: default;
        }
        .slot-blocked {
          background: #e5e7eb !important;
        }
      }
    `,
  ],
  template: `
    <div class="mp-page">
      <!-- Header -->
      <div class="mp-header flex align-items-center justify-content-between mb-3 flex-wrap gap-2">
        <h1 class="text-2xl font-semibold m-0">Staff Schedule</h1>
        <div class="flex align-items-center gap-2 flex-wrap">
          <p-button
            icon="pi pi-chevron-left"
            severity="secondary"
            [text]="true"
            (onClick)="store.navigateDay('prev')"
          />
          <p-button label="Today" severity="secondary" size="small" (onClick)="store.goToToday()" />
          <p-button
            icon="pi pi-chevron-right"
            severity="secondary"
            [text]="true"
            (onClick)="store.navigateDay('next')"
          />
          <span class="font-medium text-lg">
            {{ store.currentDate() | date: 'EEEE, MMMM d, yyyy' }}
          </span>
          <p-button
            label="Print"
            icon="pi pi-print"
            severity="secondary"
            size="small"
            [outlined]="true"
            class="mp-actions"
            (onClick)="printSchedule()"
          />
        </div>
      </div>

      <!-- Provider selector -->
      <div class="mp-selector surface-card border-round p-3 mb-3">
        <div
          class="flex align-items-center justify-content-between cursor-pointer"
          (click)="selectorExpanded.set(!selectorExpanded())"
          role="button"
          tabindex="0"
          (keydown.enter)="selectorExpanded.set(!selectorExpanded())"
          aria-label="Toggle provider selector"
        >
          <span class="font-semibold">
            Providers
            <span class="text-color-secondary text-sm ml-1">
              ({{ store.selectedProviderIds().length }} selected)
            </span>
          </span>
          <div class="flex align-items-center gap-2">
            @if (store.selectedProviderIds().length > 5) {
              <span class="text-xs font-medium" style="color: var(--orange-500)">
                <i class="pi pi-exclamation-triangle mr-1"></i>Scroll to see all columns
              </span>
            }
            <i [class]="'pi ' + (selectorExpanded() ? 'pi-chevron-up' : 'pi-chevron-down')"></i>
          </div>
        </div>

        @if (selectorExpanded()) {
          <div class="flex flex-wrap gap-3 mt-3">
            @for (p of store.allProviders(); track p.providerId) {
              <div class="flex align-items-center gap-2">
                <p-checkbox
                  [ngModel]="store.selectedProviderIds().includes(p.providerId)"
                  (ngModelChange)="onProviderToggle(p.providerId)"
                  [binary]="true"
                  [inputId]="'prov-' + p.providerId"
                  [disabled]="
                    store.selectedProviderIds().includes(p.providerId) &&
                    store.selectedProviderIds().length === 1
                  "
                />
                <label [for]="'prov-' + p.providerId" class="cursor-pointer text-sm">
                  {{ p.name }}
                  @if (p.specialty) {
                    <span class="text-color-secondary ml-1">({{ p.specialty }})</span>
                  }
                </label>
              </div>
            }
          </div>
        }
      </div>

      <!-- Loading skeleton -->
      @if (store.isLoading() && !hasAnyData()) {
        <div class="flex gap-3">
          @for (i of [1, 2, 3]; track i) {
            <p-skeleton width="200px" height="400px" />
          }
        </div>
      }

      <!-- Time grid -->
      @if (!store.isLoading() || hasAnyData()) {
        <div class="mp-grid-wrapper">
          <div class="mp-grid" [style.grid-template-columns]="gridTemplateColumns()">
            <!-- Header row -->
            <div class="time-header-cell"></div>
            @for (p of selectedProviders(); track p.providerId) {
              <div class="provider-header-cell">
                <span class="font-semibold text-sm">{{ p.name }}</span>
                @if (p.specialty) {
                  <span class="text-xs text-color-secondary">{{ p.specialty }}</span>
                }
              </div>
            }

            <!-- Time column -->
            <div class="time-col">
              @for (label of timeLabels; track label) {
                <div class="time-cell">{{ label }}</div>
              }
            </div>

            <!-- Provider columns -->
            @for (p of selectedProviders(); track p.providerId) {
              <div
                class="provider-col"
                [style.height.px]="gridHeightPx"
                [attr.data-provider-id]="p.providerId"
                (dragover)="onDragOver($event)"
                (drop)="onDrop($event, p.providerId)"
              >
                <!-- Not-available overlay -->
                @if (!hasSchedule(p.providerId)) {
                  <div class="not-available-overlay">
                    <div class="text-center">
                      <i class="pi pi-ban mb-2" style="font-size: 1.5rem; display: block"></i>
                      Not Available
                    </div>
                  </div>
                }

                <!-- Slot background cells -->
                @for (slot of timeSlots; track slot.minutes) {
                  <div
                    class="slot-cell"
                    [class.slot-available]="isSlotAvailable(p.providerId, slot.minutes)"
                    [class.slot-blocked]="!isSlotAvailable(p.providerId, slot.minutes)"
                    [style.top.px]="slot.minutes * pxPerMinute"
                    role="button"
                    [tabindex]="isSlotAvailable(p.providerId, slot.minutes) ? 0 : -1"
                    [attr.aria-label]="'Book slot at ' + slotTimeLabel(slot.minutes)"
                    (click)="onSlotClick(p.providerId, slot.minutes)"
                    (keydown.enter)="onSlotClick(p.providerId, slot.minutes)"
                    (keydown.space)="onSlotClick(p.providerId, slot.minutes)"
                  ></div>
                }

                <!-- Appointment blocks -->
                @for (appt of appointmentsFor(p.providerId); track appt.appointmentId) {
                  <div
                    class="appt-block"
                    [ngClass]="apptBlockClass(appt.status)"
                    [style.top.px]="apptTop(appt)"
                    [style.height.px]="apptHeight(appt)"
                    draggable="true"
                    (dragstart)="onDragStart($event, appt, p.providerId)"
                    (click)="$event.stopPropagation()"
                    role="button"
                    tabindex="0"
                    [attr.aria-label]="
                      appt.patientName +
                      ' with ' +
                      appt.providerName +
                      ' at ' +
                      (appt.slotTime | date: 'h:mm a')
                    "
                  >
                    <div
                      class="appt-title font-medium"
                      style="white-space: nowrap; overflow: hidden; text-overflow: ellipsis"
                    >
                      {{ appt.patientName }}
                    </div>
                    <div class="appt-time" style="font-size:0.68rem; opacity: 0.8">
                      {{ appt.slotTime | date: 'h:mm' }}–{{ appt.endTime | date: 'h:mm a' }}
                    </div>
                    <p-tag
                      [value]="appt.status"
                      [severity]="statusSeverity(appt.status)"
                      styleClass="text-xs"
                      style="position:absolute;top:2px;right:4px;font-size:0.6rem"
                    />
                  </div>
                }
              </div>
            }
          </div>
        </div>
      }

      <!-- Quick-book dialog (wired in Task 002) -->
      <p-dialog
        [visible]="quickBookVisible()"
        (visibleChange)="quickBookVisible.set($event)"
        header="Quick Book Appointment"
        [modal]="true"
        [draggable]="false"
        [resizable]="false"
        [style]="{ width: '380px' }"
      >
        @if (quickBookSlotLabel(); as label) {
          <p class="mb-3 text-color-secondary text-sm">
            <i class="pi pi-clock mr-1"></i>{{ label }}
          </p>
        }
        <div class="field mb-3">
          <label for="qbPatient" class="block font-medium mb-1 text-sm">Patient Name</label>
          <input
            pInputText
            id="qbPatient"
            [ngModel]="quickBookPatientName()"
            (ngModelChange)="quickBookPatientName.set($event)"
            placeholder="Full patient name"
            class="w-full"
          />
        </div>
        <div class="field mb-3">
          <label for="qbReason" class="block font-medium mb-1 text-sm">Visit Reason</label>
          <input
            pInputText
            id="qbReason"
            [ngModel]="quickBookVisitReason()"
            (ngModelChange)="quickBookVisitReason.set($event)"
            placeholder="e.g. Annual checkup"
            class="w-full"
          />
        </div>
        <ng-template pTemplate="footer">
          <p-button
            label="Cancel"
            severity="secondary"
            [outlined]="true"
            (onClick)="closeQuickBook()"
          />
          <p-button
            label="Book"
            icon="pi pi-check"
            [loading]="quickBookLoading()"
            [disabled]="!quickBookPatientName().trim()"
            (onClick)="confirmQuickBook()"
          />
        </ng-template>
      </p-dialog>
    </div>
  `,
})
export class MultiProviderDayComponent implements OnInit {
  protected readonly store = inject(MultiProviderDayStore);
  private readonly bookSvc = inject(BookingService);
  private readonly toast = inject(ToastService);

  protected readonly pxPerMinute = PX_PER_MINUTE;
  protected readonly gridHeightPx = GRID_HEIGHT_PX;

  protected selectorExpanded = signal(true);

  // Quick-book state
  protected quickBookVisible = signal(false);
  protected quickBookProviderId = signal<string | null>(null);
  protected quickBookSlotMinutes = signal(0);
  protected quickBookPatientName = signal('');
  protected quickBookVisitReason = signal('');
  protected quickBookLoading = signal(false);

  // Drag state
  private dragAppointmentId = '';
  private dragFromProviderId = '';

  protected readonly selectedProviders = computed(() =>
    this.store
      .allProviders()
      .filter((p) => this.store.selectedProviderIds().includes(p.providerId)),
  );

  protected readonly gridTemplateColumns = computed(
    () => `72px repeat(${this.selectedProviders().length}, minmax(160px, 1fr))`,
  );

  protected readonly timeLabels: string[] = Array.from({ length: 40 }, (_, i) => {
    const totalMin = DAY_START_HOUR * 60 + i * 15;
    const h = Math.floor(totalMin / 60);
    const m = totalMin % 60;
    return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
  });

  protected readonly timeSlots: { minutes: number }[] = Array.from({ length: 40 }, (_, i) => ({
    minutes: i * 15,
  }));

  protected readonly quickBookSlotLabel = computed(() => {
    const mins = this.quickBookSlotMinutes();
    const total = DAY_START_HOUR * 60 + mins;
    const h = Math.floor(total / 60);
    const m = total % 60;
    const timeStr = `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
    const prov = this.store.allProviders().find((p) => p.providerId === this.quickBookProviderId());
    return prov ? `${timeStr} with ${prov.name}` : timeStr;
  });

  async ngOnInit(): Promise<void> {
    await this.store.init();
  }

  protected appointmentsFor(providerId: string): CalendarAppointmentDto[] {
    return this.store.appointmentsByProvider()[providerId] ?? [];
  }

  protected hasSchedule(providerId: string): boolean {
    const appts = this.store.appointmentsByProvider()[providerId] ?? [];
    const slots = this.store.slotsByProvider()[providerId] ?? [];
    return appts.length > 0 || slots.length > 0;
  }

  protected hasAnyData(): boolean {
    return Object.keys(this.store.appointmentsByProvider()).length > 0;
  }

  protected isSlotAvailable(providerId: string, minutesFromDayStart: number): boolean {
    const slots = this.store.slotsByProvider()[providerId] ?? [];
    return slots.some((s) => {
      const d = new Date(s.startTime);
      const slotMin = (d.getHours() - DAY_START_HOUR) * 60 + d.getMinutes();
      return slotMin === minutesFromDayStart && s.status === 'Available';
    });
  }

  protected apptTop(appt: CalendarAppointmentDto): number {
    const start = new Date(appt.slotTime);
    const minFromDayStart = (start.getHours() - DAY_START_HOUR) * 60 + start.getMinutes();
    return minFromDayStart * PX_PER_MINUTE;
  }

  protected apptHeight(appt: CalendarAppointmentDto): number {
    const duration = (new Date(appt.endTime).getTime() - new Date(appt.slotTime).getTime()) / 60000;
    return Math.max(duration * PX_PER_MINUTE, 18);
  }

  protected apptBlockClass(status: string): string {
    return `status-${status.toLowerCase()}`;
  }

  protected statusSeverity(status: string): StatusSeverity {
    const map: Record<string, StatusSeverity> = {
      Scheduled: 'info',
      Booked: 'info',
      Arrived: 'warn',
      Completed: 'success',
      Cancelled: 'danger',
      NoShow: 'secondary',
      InProgress: 'contrast',
    };
    return map[status] ?? 'secondary';
  }

  protected onProviderToggle(providerId: string): void {
    this.store.toggleProvider(providerId);
    void this.store.loadForDate(this.store.currentDate());
  }

  // Quick-book
  protected onSlotClick(providerId: string, minutesFromDayStart: number): void {
    if (!this.isSlotAvailable(providerId, minutesFromDayStart)) return;
    this.quickBookProviderId.set(providerId);
    this.quickBookSlotMinutes.set(minutesFromDayStart);
    this.quickBookPatientName.set('');
    this.quickBookVisitReason.set('');
    this.quickBookVisible.set(true);
  }

  protected closeQuickBook(): void {
    this.quickBookVisible.set(false);
  }

  protected async confirmQuickBook(): Promise<void> {
    const providerId = this.quickBookProviderId();
    const mins = this.quickBookSlotMinutes();
    if (!providerId) return;

    const slot = (this.store.slotsByProvider()[providerId] ?? []).find((s) => {
      const d = new Date(s.startTime);
      return (
        (d.getHours() - DAY_START_HOUR) * 60 + d.getMinutes() === mins && s.status === 'Available'
      );
    });

    if (!slot) {
      this.toast.error('Unavailable', 'This slot is no longer available.');
      this.closeQuickBook();
      return;
    }

    this.quickBookLoading.set(true);
    try {
      await firstValueFrom(
        this.bookSvc.bookAppointment(slot.slotId, this.quickBookVisitReason() || 'Walk-in'),
      );
      this.toast.success('Booked', 'Appointment created successfully.');
      this.closeQuickBook();
      await this.store.loadForDate(this.store.currentDate());
    } catch {
      this.toast.error('Error', 'Could not create the appointment.');
    } finally {
      this.quickBookLoading.set(false);
    }
  }

  // Drag and drop
  protected onDragStart(
    event: DragEvent,
    appt: CalendarAppointmentDto,
    fromProviderId: string,
  ): void {
    this.dragAppointmentId = appt.appointmentId;
    this.dragFromProviderId = fromProviderId;
    event.dataTransfer?.setData('text/plain', appt.appointmentId);
  }

  protected onDragOver(event: DragEvent): void {
    event.preventDefault();
  }

  protected async onDrop(event: DragEvent, toProviderId: string): Promise<void> {
    event.preventDefault();
    if (
      !this.dragAppointmentId ||
      !this.dragFromProviderId ||
      this.dragFromProviderId === toProviderId
    ) {
      this.dragAppointmentId = '';
      this.dragFromProviderId = '';
      return;
    }

    const offsetY = event.offsetY;
    const snappedMinutes = Math.floor(offsetY / PX_PER_MINUTE / 15) * 15;

    const targetSlot = (this.store.slotsByProvider()[toProviderId] ?? []).find((s) => {
      const d = new Date(s.startTime);
      const slotMin = (d.getHours() - DAY_START_HOUR) * 60 + d.getMinutes();
      return slotMin === snappedMinutes && s.status === 'Available';
    });

    if (!targetSlot) {
      this.toast.error('Blocked', 'Cannot reassign: the target slot is not available.');
      this.dragAppointmentId = '';
      this.dragFromProviderId = '';
      return;
    }

    const appointmentId = this.dragAppointmentId;
    const fromProviderId = this.dragFromProviderId;
    this.dragAppointmentId = '';
    this.dragFromProviderId = '';

    try {
      await firstValueFrom(this.bookSvc.rescheduleAppointment(appointmentId, targetSlot.slotId));
      this.store.updateAppointmentProvider(appointmentId, fromProviderId, toProviderId);
      await this.store.loadForDate(this.store.currentDate());
      this.toast.success('Reassigned', 'Appointment moved successfully.');
    } catch {
      this.toast.error('Error', 'Could not reassign the appointment.');
    }
  }

  protected printSchedule(): void {
    window.print();
  }

  protected slotTimeLabel(minutesFromDayStart: number): string {
    const total = DAY_START_HOUR * 60 + minutesFromDayStart;
    const h = Math.floor(total / 60);
    const m = total % 60;
    return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}`;
  }
}
