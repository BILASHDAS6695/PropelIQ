import {
  ChangeDetectionStrategy,
  Component,
  computed,
  HostListener,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DrawerModule } from 'primeng/drawer';
import { SelectModule } from 'primeng/select';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { CalendarStore, CalendarViewMode } from './calendar.store';
import { CalendarAppointmentDto } from '../../core/models/calendar.models';
import { AuthService } from '../../core/auth/auth.service';
import { BookingService } from '../../core/services/booking.service';
import { IcsService } from '../../core/services/ics.service';

type StatusSeverity = 'info' | 'success' | 'danger' | 'secondary' | 'warn' | 'contrast';

interface ProviderOption {
  label: string;
  value: string;
}

@Component({
  selector: 'app-calendar-view',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    DatePickerModule,
    DrawerModule,
    SelectModule,
    SkeletonModule,
    TagModule,
  ],
  styles: [
    `
      .calendar-page {
        max-width: 900px;
        margin: 0 auto;
        padding: 1rem;
      }
      .view-tabs {
        display: flex;
        gap: 0.5rem;
      }
      .appt-dot {
        width: 6px;
        height: 6px;
        border-radius: 50%;
        background: var(--primary-color);
        display: inline-block;
        margin: 0 1px;
      }
      .appt-block {
        border-left: 4px solid;
        padding: 0.5rem 0.75rem;
        border-radius: 4px;
        margin-bottom: 0.5rem;
        cursor: pointer;
        background: var(--surface-card);
        transition: box-shadow 0.15s;
      }
      .appt-block:hover {
        box-shadow: 0 2px 8px rgba(0, 0, 0, 0.12);
      }
      .status-scheduled {
        border-color: #3b82f6;
      }
      .status-booked {
        border-color: #3b82f6;
      }
      .status-completed {
        border-color: #22c55e;
      }
      .status-cancelled {
        border-color: #ef4444;
      }
      .status-noshow {
        border-color: #9ca3af;
      }
      .status-arrived {
        border-color: #f59e0b;
      }
      .status-inprogress {
        border-color: #8b5cf6;
      }
      .empty-state {
        text-align: center;
        padding: 3rem 1rem;
        color: var(--text-color-secondary);
      }
    `,
  ],
  template: `
    <div class="calendar-page">
      <!-- Header row -->
      <div class="flex align-items-center justify-content-between mb-3 flex-wrap gap-2">
        <h1 class="text-2xl font-semibold m-0">Calendar</h1>
        <div class="flex align-items-center gap-2 flex-wrap">
          <!-- View mode tabs -->
          <div class="view-tabs">
            @for (mode of viewModes; track mode.value) {
              <p-button
                [label]="mode.label"
                [severity]="store.viewMode() === mode.value ? 'primary' : 'secondary'"
                size="small"
                [outlined]="store.viewMode() !== mode.value"
                (onClick)="switchView(mode.value)"
              />
            }
          </div>
          <!-- Nav: prev / today / next -->
          <p-button
            icon="pi pi-chevron-left"
            severity="secondary"
            [text]="true"
            (onClick)="store.navigate('prev'); void loadCurrentRange()"
          />
          <p-button label="Today" severity="secondary" size="small" (onClick)="onToday()" />
          <p-button
            icon="pi pi-chevron-right"
            severity="secondary"
            [text]="true"
            (onClick)="store.navigate('next'); void loadCurrentRange()"
          />
        </div>
      </div>

      <!-- Staff: provider filter -->
      @if (isStaff()) {
        <p-select
          [options]="providerOptions()"
          [(ngModel)]="selectedProviderId"
          optionLabel="label"
          optionValue="value"
          placeholder="All providers"
          [showClear]="true"
          styleClass="w-full md:w-20rem mb-3"
          (onChange)="onProviderChange()"
        />
      }

      <!-- Month view: desktop — inline DatePicker as navigator -->
      @if (store.viewMode() === 'month' && !isMobile()) {
        <div class="flex flex-column md:flex-row gap-4">
          <p-datepicker
            [inline]="true"
            [(ngModel)]="pickerDate"
            (ngModelChange)="onPickerDateChange($event)"
            styleClass="flex-shrink-0"
          >
            <ng-template pTemplate="date" let-date>
              <span>{{ date.day }}</span>
              @if (dayAppointmentCount(date.year, date.month, date.day); as count) {
                @if (count > 0 && count <= 5) {
                  <span class="appt-dot"></span>
                } @else if (count > 5) {
                  <span
                    class="text-xs font-bold"
                    style="color: var(--primary-color); display: block; line-height: 1"
                    >{{ count }}</span
                  >
                }
              }
            </ng-template>
          </p-datepicker>

          <!-- Day appointment list -->
          <div class="flex-1">
            <h3 class="mt-0 mb-2 text-lg">
              {{ selectedDay() | date: 'EEEE, MMMM d, yyyy' }}
            </h3>
            @if (store.isLoading()) {
              @for (i of [1, 2, 3]; track i) {
                <p-skeleton height="3rem" styleClass="mb-2" />
              }
            } @else if (dayAppointments().length === 0) {
              <div class="empty-state">
                <i class="pi pi-calendar mb-2" style="font-size:2rem;display:block"></i>
                No appointments on this day.
              </div>
            } @else {
              @for (appt of dayAppointments(); track appt.appointmentId) {
                <div
                  class="appt-block"
                  [ngClass]="statusBlockClass(appt.status)"
                  (click)="openDetail(appt)"
                  role="button"
                  tabindex="0"
                  [attr.aria-label]="appt.providerName + ' at ' + (appt.slotTime | date: 'h:mm a')"
                >
                  <div class="flex justify-content-between align-items-center">
                    <span class="font-semibold">{{ appt.providerName }}</span>
                    <p-tag [value]="appt.status" [severity]="statusSeverity(appt.status)" />
                  </div>
                  <div class="text-sm text-color-secondary mt-1">
                    <i class="pi pi-clock mr-1"></i>
                    {{ appt.slotTime | date: 'h:mm a' }} – {{ appt.endTime | date: 'h:mm a' }}
                  </div>
                  @if (appt.visitReason) {
                    <div class="text-sm mt-1">{{ appt.visitReason }}</div>
                  }
                </div>
              }
            }
          </div>
        </div>
      }

      <!-- Month view: mobile — compact date-grouped list -->
      @if (store.viewMode() === 'month' && isMobile()) {
        <div>
          @for (group of appointmentsByDay(); track group.date) {
            <div class="mb-3">
              <h4 class="mt-0 mb-2 text-base font-semibold">
                {{ group.date | date: 'EEE, MMM d' }}
              </h4>
              @for (appt of group.items; track appt.appointmentId) {
                <div
                  class="appt-block"
                  [ngClass]="statusBlockClass(appt.status)"
                  (click)="openDetail(appt)"
                  role="button"
                  tabindex="0"
                  [attr.aria-label]="appt.providerName + ' at ' + (appt.slotTime | date: 'h:mm a')"
                >
                  <div class="flex justify-content-between align-items-center">
                    <span class="font-semibold"
                      >{{ appt.slotTime | date: 'h:mm a' }} — {{ appt.providerName }}</span
                    >
                    <p-tag [value]="appt.status" [severity]="statusSeverity(appt.status)" />
                  </div>
                </div>
              }
            </div>
          }
          @if (store.appointments().length === 0 && !store.isLoading()) {
            <div class="empty-state">
              <i class="pi pi-calendar mb-2" style="font-size:2rem;display:block"></i>
              No appointments this month.
            </div>
          }
        </div>
      }

      <!-- Week / Day views: time-ordered list -->
      @if (store.viewMode() !== 'month') {
        <div>
          <h3 class="mt-0 mb-2 text-lg">{{ rangeLabel() }}</h3>
          @if (store.isLoading()) {
            @for (i of [1, 2, 3, 4]; track i) {
              <p-skeleton height="3rem" styleClass="mb-2" />
            }
          } @else if (store.appointments().length === 0) {
            <div class="empty-state">
              <i class="pi pi-calendar mb-2" style="font-size:2rem;display:block"></i>
              No appointments in this period.
            </div>
          } @else {
            @for (appt of store.appointments(); track appt.appointmentId) {
              <div
                class="appt-block"
                [ngClass]="statusBlockClass(appt.status)"
                (click)="openDetail(appt)"
                role="button"
                tabindex="0"
                [attr.aria-label]="appt.providerName + ' at ' + (appt.slotTime | date: 'h:mm a')"
              >
                <div class="flex justify-content-between align-items-center">
                  <span class="font-semibold">
                    {{ appt.slotTime | date: 'EEE d MMM · h:mm a' }} — {{ appt.providerName }}
                  </span>
                  <p-tag [value]="appt.status" [severity]="statusSeverity(appt.status)" />
                </div>
                @if (isStaff()) {
                  <div class="text-sm text-color-secondary mt-1">
                    <i class="pi pi-user mr-1"></i>{{ appt.patientName }}
                  </div>
                }
                @if (appt.visitReason) {
                  <div class="text-sm mt-1">{{ appt.visitReason }}</div>
                }
              </div>
            }
          }
        </div>
      }
    </div>

    <!-- Detail drawer -->
    <p-drawer
      [(visible)]="drawerVisible"
      position="right"
      header="Appointment Details"
      styleClass="w-full md:w-25rem"
    >
      @if (store.selectedAppointment(); as appt) {
        <div class="flex flex-column gap-3">
          <div>
            <div class="text-color-secondary text-sm mb-1">Provider</div>
            <div class="font-semibold">{{ appt.providerName }}</div>
          </div>
          @if (isStaff()) {
            <div>
              <div class="text-color-secondary text-sm mb-1">Patient</div>
              <div class="font-semibold">{{ appt.patientName }}</div>
            </div>
          }
          <div>
            <div class="text-color-secondary text-sm mb-1">Date &amp; Time</div>
            <div>{{ appt.slotTime | date: 'EEE, MMM d, yyyy · h:mm a' }}</div>
          </div>
          <div>
            <div class="text-color-secondary text-sm mb-1">Status</div>
            <p-tag [value]="appt.status" [severity]="statusSeverity(appt.status)" />
          </div>
          @if (appt.visitReason) {
            <div>
              <div class="text-color-secondary text-sm mb-1">Visit Reason</div>
              <div>{{ appt.visitReason }}</div>
            </div>
          }
          <p-button
            label="Add to Calendar"
            severity="secondary"
            [outlined]="true"
            icon="pi pi-calendar-plus"
            styleClass="w-full"
            (onClick)="addToCalendar(appt)"
          />
          @if (canCancel(appt.status)) {
            <p-button
              label="Cancel Appointment"
              severity="danger"
              [outlined]="true"
              icon="pi pi-times"
              styleClass="w-full"
              (onClick)="goToCancel(appt)"
            />
          }
          @if (canReschedule(appt.status)) {
            <p-button
              label="Reschedule"
              severity="secondary"
              [outlined]="true"
              icon="pi pi-calendar"
              styleClass="w-full"
              (onClick)="goToReschedule(appt)"
            />
          }
        </div>
      }
    </p-drawer>
  `,
})
export class CalendarViewComponent implements OnInit {
  protected readonly store = inject(CalendarStore);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly bookingSvc = inject(BookingService);
  private readonly ics = inject(IcsService);

  protected readonly isMobile = signal(window.innerWidth <= 768);
  protected drawerVisible = false;
  protected pickerDate: Date = new Date();
  protected selectedProviderId: string | null = null;

  protected readonly viewModes: { label: string; value: CalendarViewMode }[] = [
    { label: 'Month', value: 'month' },
    { label: 'Week', value: 'week' },
    { label: 'Day', value: 'day' },
  ];

  protected readonly isStaff = computed(() => {
    const role = this.auth.user()?.role;
    return role === 'staff' || role === 'admin';
  });

  protected readonly providerOptions = signal<ProviderOption[]>([]);

  protected readonly selectedDay = computed(() => {
    const d = this.store.currentDate();
    return new Date(d.getFullYear(), d.getMonth(), d.getDate());
  });

  protected readonly dayAppointments = computed(() => {
    const day = this.selectedDay();
    return this.store.appointments().filter((a) => {
      const t = new Date(a.slotTime);
      return (
        t.getFullYear() === day.getFullYear() &&
        t.getMonth() === day.getMonth() &&
        t.getDate() === day.getDate()
      );
    });
  });

  protected readonly rangeLabel = computed(() => {
    const d = this.store.currentDate();
    const mode = this.store.viewMode();
    if (mode === 'week') {
      const start = new Date(d);
      start.setDate(d.getDate() - d.getDay());
      const end = new Date(start);
      end.setDate(start.getDate() + 6);
      return `${start.toLocaleDateString()} – ${end.toLocaleDateString()}`;
    }
    return d.toLocaleDateString(undefined, {
      weekday: 'long',
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    });
  });

  async ngOnInit(): Promise<void> {
    if (this.isStaff()) {
      const providers = await firstValueFrom(this.bookingSvc.getProviders());
      this.providerOptions.set(providers.map((p) => ({ label: p.name, value: p.providerId })));
    }
    await this.loadCurrentRange();
  }

  protected hasAppointmentsOnDay(year: number, month: number, day: number): boolean {
    // PrimeNG DatePicker months are 1-based; JS Date.getMonth() is 0-based
    return this.store.appointments().some((a) => {
      const t = new Date(a.slotTime);
      return t.getFullYear() === year && t.getMonth() + 1 === month && t.getDate() === day;
    });
  }

  @HostListener('window:resize')
  onResize(): void {
    this.isMobile.set(window.innerWidth <= 768);
  }

  protected dayAppointmentCount(year: number, month: number, day: number): number {
    return this.store.appointments().filter((a) => {
      const t = new Date(a.slotTime);
      return t.getFullYear() === year && t.getMonth() + 1 === month && t.getDate() === day;
    }).length;
  }

  protected readonly appointmentsByDay = computed(() => {
    const groups = new Map<string, CalendarAppointmentDto[]>();
    for (const appt of this.store.appointments()) {
      const key = new Date(appt.slotTime).toDateString();
      if (!groups.has(key)) groups.set(key, []);
      groups.get(key)!.push(appt);
    }
    return [...groups.entries()].map(([date, items]) => ({ date: new Date(date), items }));
  });

  protected openDetail(appt: CalendarAppointmentDto): void {
    this.store.setSelectedAppointment(appt);
    this.drawerVisible = true;
  }

  protected onPickerDateChange(date: Date | null): void {
    if (!date) return;
    this.store.setCurrentDate(date);
    void this.loadCurrentRange();
  }

  protected switchView(mode: CalendarViewMode): void {
    this.store.setViewMode(mode);
    void this.loadCurrentRange();
  }

  protected async onToday(): Promise<void> {
    this.store.goToToday();
    this.pickerDate = new Date();
    await this.loadCurrentRange();
  }

  protected async onProviderChange(): Promise<void> {
    this.store.setSelectedProvider(this.selectedProviderId);
    await this.loadCurrentRange();
  }

  protected goToCancel(appt: CalendarAppointmentDto): void {
    this.drawerVisible = false;
    void this.router.navigate(['/booking/appointments'], {
      queryParams: { cancel: appt.appointmentId },
    });
  }

  protected goToReschedule(appt: CalendarAppointmentDto): void {
    this.drawerVisible = false;
    void this.router.navigate(['/booking'], {
      queryParams: { reschedule: appt.appointmentId },
    });
  }

  protected canCancel(status: string): boolean {
    return status === 'Scheduled' || status === 'Booked';
  }

  protected canReschedule(status: string): boolean {
    return status === 'Scheduled' || status === 'Booked';
  }

  protected addToCalendar(appt: CalendarAppointmentDto): void {
    const content = this.ics.buildSingle(appt);
    this.ics.download(`appointment-${appt.appointmentId}`, content);
  }

  protected statusSeverity(
    status: string,
  ): 'info' | 'success' | 'danger' | 'secondary' | 'warn' | 'contrast' {
    const map: Record<string, StatusSeverity> = {
      Scheduled: 'info',
      Booked: 'info',
      Arrived: 'warn',
      Completed: 'success',
      Cancelled: 'danger',
      NoShow: 'secondary',
      InProgress: 'contrast',
      WalkIn: 'info',
    };
    return map[status] ?? 'secondary';
  }

  protected statusBlockClass(status: string): string {
    return `status-${status.toLowerCase()}`;
  }

  protected loadCurrentRange(): Promise<void> {
    const { from, to } = this.getRangeForCurrentView();
    return this.store.loadRange(from, to, this.selectedProviderId ?? undefined);
  }

  private getRangeForCurrentView(): { from: Date; to: Date } {
    const d = this.store.currentDate();
    const mode = this.store.viewMode();

    if (mode === 'month') {
      const from = new Date(d.getFullYear(), d.getMonth(), 1);
      const to = new Date(d.getFullYear(), d.getMonth() + 1, 0, 23, 59, 59);
      return { from, to };
    }

    if (mode === 'week') {
      const from = new Date(d);
      from.setDate(d.getDate() - d.getDay());
      from.setHours(0, 0, 0, 0);
      const to = new Date(from);
      to.setDate(from.getDate() + 6);
      to.setHours(23, 59, 59, 999);
      return { from, to };
    }

    // day
    const from = new Date(d.getFullYear(), d.getMonth(), d.getDate(), 0, 0, 0);
    const to = new Date(d.getFullYear(), d.getMonth(), d.getDate(), 23, 59, 59);
    return { from, to };
  }
}
