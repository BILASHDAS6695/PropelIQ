import { Component, inject } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InactivityTimerService } from '../../../core/services/inactivity-timer.service';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-inactivity-warning',
  standalone: true,
  imports: [DialogModule, ButtonModule],
  template: `
    <p-dialog
      header="Session Expiring Soon"
      [visible]="timer.showWarning()"
      [modal]="true"
      [closable]="false"
      [style]="{ width: '420px' }"
    >
      <p class="mb-4">
        Your session will expire in 2 minutes due to inactivity.<br />
        Click <strong>Stay Signed In</strong> to continue.
      </p>
      <ng-template pTemplate="footer">
        <p-button label="Sign Out Now" severity="secondary" (onClick)="signOut()" />
        <p-button label="Stay Signed In" (onClick)="staySignedIn()" class="ml-2" />
      </ng-template>
    </p-dialog>
  `,
})
export class InactivityWarningComponent {
  protected readonly timer = inject(InactivityTimerService);
  private readonly auth = inject(AuthService);

  staySignedIn(): void {
    this.timer.showWarning.set(false);
    document.dispatchEvent(new Event('mousemove'));
  }

  signOut(): void {
    this.timer.stop();
    this.auth.logout();
  }
}
