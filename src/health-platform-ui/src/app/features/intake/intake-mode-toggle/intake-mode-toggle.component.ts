import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { IntakeChatStore } from '../intake-chat.store';
import { IntakeFormStore } from '../intake-form.store';

@Component({
  selector: 'app-intake-mode-toggle',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonModule, RouterModule],
  template: `
    <div class="flex gap-2 mb-4">
      <p-button
        label="Chat"
        icon="pi pi-comments"
        [outlined]="isFormMode()"
        (onClick)="switchToChat()"
        aria-label="Switch to conversational intake"
      />
      <p-button
        label="Form"
        icon="pi pi-list"
        [outlined]="!isFormMode()"
        (onClick)="switchToForm()"
        aria-label="Switch to form-based intake"
      />
    </div>
  `,
})
export class IntakeModeToggleComponent {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly chatStore = inject(IntakeChatStore);
  private readonly formStore = inject(IntakeFormStore);

  protected isFormMode(): boolean {
    return this.router.url.includes('/form');
  }

  protected switchToForm(): void {
    if (this.isFormMode()) return;
    const collected = this.chatStore.collected();
    if (Object.values(collected).some((v) => v !== null)) {
      this.formStore.prefill(collected);
    }
    const appointmentId =
      this.route.snapshot.queryParamMap.get('appointmentId') ??
      this.chatStore.appointmentId();
    void this.router.navigate(['/intake/form'], {
      queryParams: appointmentId ? { appointmentId } : {},
    });
  }

  protected switchToChat(): void {
    if (!this.isFormMode()) return;
    void this.router.navigate(['/intake']);
  }
}
