import {
  AfterViewChecked,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  OnInit,
  signal,
  ViewChild,
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { IntakeChatStore } from '../intake-chat.store';
import { IntakeModeToggleComponent } from '../intake-mode-toggle/intake-mode-toggle.component';

const QUICK_REPLIES = [
  'No known allergies',
  'No current medications',
  'No significant medical history',
  'Symptoms started recently',
  'Same issue as before',
  'Feeling better overall',
] as const;

@Component({
  selector: 'app-intake-chat',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    InputTextModule,
    SkeletonModule,
    TagModule,
    IntakeModeToggleComponent,
  ],
  styles: [
    `
      .chat-page {
        max-width: 720px;
        margin: 0 auto;
        padding: 1.5rem 1rem;
        display: flex;
        flex-direction: column;
        height: calc(100vh - 80px);
      }
      .chat-messages {
        flex: 1;
        overflow-y: auto;
        display: flex;
        flex-direction: column;
        gap: 0.75rem;
        padding-bottom: 1rem;
      }
      .bubble-row {
        display: flex;
      }
      .bubble-row.user {
        justify-content: flex-end;
      }
      .bubble-row.assistant {
        justify-content: flex-start;
      }
      .bubble {
        max-width: 75%;
        padding: 0.625rem 1rem;
        border-radius: 1rem;
        font-size: 0.9375rem;
        line-height: 1.5;
        white-space: pre-wrap;
        word-break: break-word;
      }
      .bubble.user {
        background: var(--primary-color);
        color: var(--primary-color-text);
        border-bottom-right-radius: 0.25rem;
      }
      .bubble.assistant {
        background: var(--surface-card);
        border: 1px solid var(--surface-border);
        border-bottom-left-radius: 0.25rem;
      }
      .input-row {
        display: flex;
        gap: 0.5rem;
        padding-top: 0.75rem;
        border-top: 1px solid var(--surface-border);
      }
      .input-row input {
        flex: 1;
      }
      .complete-card {
        background: var(--surface-card);
        border: 1px solid var(--green-300);
        border-radius: 8px;
        padding: 1.25rem;
        margin-top: 1rem;
      }
      .quick-replies {
        display: flex;
        flex-wrap: wrap;
        gap: 0.375rem;
        padding: 0.5rem 0 0.25rem;
      }
      .retry-row {
        display: flex;
        align-items: center;
        gap: 0.5rem;
        padding-top: 0.25rem;
      }
      @media (max-width: 640px) {
        .chat-page {
          padding: 0.5rem;
          height: 100dvh;
          max-width: 100%;
        }
      }
    `,
  ],
  template: `
    <div class="chat-page">
      <app-intake-mode-toggle />
      <h2 class="text-xl font-semibold mb-3">
        <i class="pi pi-comments mr-2"></i>Pre-Visit Intake
      </h2>

      <!-- Message thread -->
      <div class="chat-messages" #scrollContainer>
        @for (msg of store.messages(); track $index) {
          <div
            class="bubble-row"
            [class.user]="msg.role === 'user'"
            [class.assistant]="msg.role === 'assistant'"
          >
            <div
              class="bubble"
              [class.user]="msg.role === 'user'"
              [class.assistant]="msg.role === 'assistant'"
            >
              {{ msg.content }}
            </div>
            @if (store.failedAtIndex() === $index) {
              <div class="retry-row">
                <span class="text-red-500 text-xs">Message not sent</span>
                <p-button
                  label="Retry"
                  icon="pi pi-refresh"
                  severity="danger"
                  size="small"
                  [text]="true"
                  (onClick)="store.retryLast()"
                  aria-label="Retry sending failed message"
                />
              </div>
            }
          </div>
        }

        @if (store.isLoading()) {
          <div class="bubble-row assistant">
            <p-skeleton width="240px" height="36px" borderRadius="1rem" />
          </div>
        }
      </div>

      <!-- Completion summary -->
      @if (store.isComplete()) {
        <div class="complete-card">
          <p class="font-semibold text-green-600 mb-2">
            <i class="pi pi-check-circle mr-1"></i>Intake complete — thank you!
          </p>
          <div class="flex flex-wrap gap-2">
            @for (entry of collectedEntries(); track entry.key) {
              @if (entry.value) {
                <p-tag [value]="entry.key + ': ' + entry.value" severity="success" />
              }
            }
          </div>
        </div>
      }

      <!-- Quick-reply suggestions -->
      @if (!store.isLoading() && !store.isComplete() && store.messages().length > 0) {
        <div class="quick-replies" role="group" aria-label="Quick reply suggestions">
          @for (reply of quickReplies; track reply) {
            <p-button
              [label]="reply"
              severity="secondary"
              size="small"
              [outlined]="true"
              (onClick)="submit(reply)"
              [attr.aria-label]="'Quick reply: ' + reply"
            />
          }
        </div>
      }

      <!-- Input row -->
      @if (!store.isComplete()) {
        <div class="input-row">
          <input
            pInputText
            [(ngModel)]="inputText"
            placeholder="Type your response…"
            (keydown.enter)="submit()"
            [disabled]="store.isLoading()"
            aria-label="Intake message input"
          />
          <p-button
            icon="pi pi-send"
            [loading]="store.isLoading()"
            [disabled]="!inputText().trim()"
            (onClick)="submit()"
            aria-label="Send message"
          />
        </div>
      }
    </div>
  `,
})
export class IntakeChatComponent implements OnInit, AfterViewChecked {
  protected readonly store = inject(IntakeChatStore);

  @ViewChild('scrollContainer') private scrollContainer!: ElementRef<HTMLElement>;

  protected inputText = signal('');
  protected readonly quickReplies = QUICK_REPLIES;

  protected collectedEntries(): { key: string; value: string | null }[] {
    return Object.entries(this.store.collected()).map(([key, value]) => ({ key, value }));
  }

  async ngOnInit(): Promise<void> {
    if (this.store.messages().length === 0) {
      await this.store.startSession();
    }
  }

  ngAfterViewChecked(): void {
    if (this.scrollContainer) {
      const el = this.scrollContainer.nativeElement;
      el.scrollTop = el.scrollHeight;
    }
  }

  protected async submit(override?: string): Promise<void> {
    const text = override ?? this.inputText().trim();
    if (!text) return;
    this.inputText.set('');
    await this.store.sendMessage(text);
  }
}
