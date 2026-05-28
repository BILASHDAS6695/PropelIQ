import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CardModule } from 'primeng/card';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ButtonModule } from 'primeng/button';
import { DividerModule } from 'primeng/divider';
import { MessageService } from 'primeng/api';
import { NotificationPreferencesService } from '../../core/services/notification-preferences.service';
import { NotificationPreferences } from '../../core/models/notification.model';

@Component({
  selector: 'app-notification-preferences',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, CardModule, ToggleSwitchModule, ButtonModule, DividerModule],
  template: `
    <div class="preferences-page">
      <p-card header="Notification Preferences">
        <p class="subtitle">
          Choose which notifications you receive and through which channels. Security notifications
          (account lockout, password expiry) are always delivered.
        </p>

        <p-divider />

        <!-- Email channel -->
        <section aria-labelledby="email-heading">
          <h3 id="email-heading" class="channel-heading"><i class="pi pi-envelope"></i> Email</h3>
          <div class="pref-row">
            <label for="emailReminders">Appointment reminders</label>
            <p-toggleswitch inputId="emailReminders" [(ngModel)]="draft().emailReminders" />
          </div>
          <div class="pref-row">
            <label for="emailSwap">Slot swap notifications</label>
            <p-toggleswitch inputId="emailSwap" [(ngModel)]="draft().emailSwap" />
          </div>
          <div class="pref-row">
            <label for="emailGeneral">General notifications</label>
            <p-toggleswitch inputId="emailGeneral" [(ngModel)]="draft().emailGeneral" />
          </div>
        </section>

        <p-divider />

        <!-- In-app channel -->
        <section aria-labelledby="inapp-heading">
          <h3 id="inapp-heading" class="channel-heading"><i class="pi pi-bell"></i> In-App</h3>
          <div class="pref-row">
            <label for="inAppReminders">Appointment reminders</label>
            <p-toggleswitch inputId="inAppReminders" [(ngModel)]="draft().inAppReminders" />
          </div>
          <div class="pref-row">
            <label for="inAppSwap">Slot swap notifications</label>
            <p-toggleswitch inputId="inAppSwap" [(ngModel)]="draft().inAppSwap" />
          </div>
          <div class="pref-row">
            <label for="inAppGeneral">General notifications</label>
            <p-toggleswitch inputId="inAppGeneral" [(ngModel)]="draft().inAppGeneral" />
          </div>
        </section>

        <p-divider />

        <div class="actions">
          <p-button
            label="Save preferences"
            icon="pi pi-check"
            [loading]="saving()"
            (onClick)="save()"
          />
        </div>
      </p-card>
    </div>
  `,
  styles: [
    `
      .preferences-page {
        max-width: 560px;
        margin: 2rem auto;
        padding: 0 1rem;
      }
      .subtitle {
        color: var(--text-color-secondary);
        margin-bottom: 0.5rem;
        font-size: 0.875rem;
      }
      .channel-heading {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        font-size: 1rem;
        font-weight: 600;
        margin-bottom: 1rem;
        color: var(--text-color);
      }
      .pref-row {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 0.5rem 0;
        font-size: 0.875rem;
      }
      .actions {
        display: flex;
        justify-content: flex-end;
      }
    `,
  ],
})
export class NotificationPreferencesComponent implements OnInit {
  private readonly svc = inject(NotificationPreferencesService);
  private readonly toast = inject(MessageService);

  readonly draft = signal<NotificationPreferences>({ ...this.svc.prefs() });
  readonly saving = signal(false);

  async ngOnInit(): Promise<void> {
    await this.svc.load();
    this.draft.set({ ...this.svc.prefs() });
  }

  async save(): Promise<void> {
    this.saving.set(true);
    try {
      await this.svc.save({ ...this.draft() });
      this.toast.add({
        severity: 'success',
        summary: 'Saved',
        detail: 'Notification preferences updated.',
        life: 3_000,
      });
    } catch {
      this.toast.add({
        severity: 'error',
        summary: 'Error',
        detail: 'Failed to save preferences. Please try again.',
        life: 5_000,
      });
    } finally {
      this.saving.set(false);
    }
  }
}
