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

@Component({
  selector: 'app-intake-chat',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [CommonModule, FormsModule, ButtonModule, InputTextModule, SkeletonModule, TagModule],
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
    `,
  ],
  template: `
    <div class="chat-page">
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

  protected async submit(): Promise<void> {
    const text = this.inputText().trim();
    if (!text) return;
    this.inputText.set('');
    await this.store.sendMessage(text);
  }
}
