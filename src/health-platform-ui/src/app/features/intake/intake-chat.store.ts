import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { IntakeService } from '../../core/services/intake.service';
import { ToastService } from '../../shared/services/toast.service';
import { IntakeChatResponse } from '../../core/models/intake.models';

export interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
}

interface IntakeChatState {
  messages: ChatMessage[];
  sessionId: string | null;
  appointmentId: string | null;
  isLoading: boolean;
  isComplete: boolean;
  collected: Record<string, string | null>;
  failedAtIndex: number | null;
}

const initialState: IntakeChatState = {
  messages: [],
  sessionId: null,
  appointmentId: null,
  isLoading: false,
  isComplete: false,
  collected: {},
  failedAtIndex: null,
};

export const IntakeChatStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods(
    (
      store,
      intakeSvc = inject(IntakeService),
      toast = inject(ToastService),
      router = inject(Router),
    ) => ({
      async startSession(patientId?: string, appointmentId?: string): Promise<void> {
        patchState(store, { isLoading: true, appointmentId: appointmentId ?? null });
        try {
          const response = await firstValueFrom(
            intakeSvc.chat({ message: '', patientId, appointmentId }),
          );
          patchState(store, {
            sessionId: response.sessionId,
            messages: [{ role: 'assistant', content: response.reply }],
            collected: response.collected,
            isLoading: false,
          });
        } catch {
          patchState(store, { isLoading: false });
          toast.error('Error', 'Could not start intake session.');
        }
      },

      async sendMessage(text: string): Promise<void> {
        if (!text.trim()) return;
        const userMsg: ChatMessage = { role: 'user', content: text };
        patchState(store, {
          messages: [...store.messages(), userMsg],
          isLoading: true,
        });

        try {
          const response: IntakeChatResponse = await firstValueFrom(
            intakeSvc.chat({ sessionId: store.sessionId(), message: text }),
          );

          const assistantMsg: ChatMessage = {
            role: 'assistant',
            content: response.reply,
          };

          patchState(store, {
            messages: [...store.messages(), assistantMsg],
            sessionId: response.sessionId,
            collected: response.collected,
            isComplete: response.isComplete,
            isLoading: false,
            failedAtIndex: null,
          });

          if (response.fallbackRequired) {
            toast.warn('Notice', 'Switching to form-based intake.');
            const aid = store.appointmentId();
            void router.navigate(['/intake/form'], {
              queryParams: aid ? { appointmentId: aid } : {},
            });
          }
        } catch {
          patchState(store, {
            isLoading: false,
            failedAtIndex: store.messages().length - 1,
          });
          toast.warn('Message not sent', 'Tap the Retry button to resend.');
        }
      },

      async retryLast(): Promise<void> {
        const idx = store.failedAtIndex();
        if (idx === null) return;
        const msg = store.messages()[idx];
        if (!msg || msg.role !== 'user') return;
        patchState(store, {
          messages: store.messages().filter((_, i) => i !== idx),
          failedAtIndex: null,
        });
        await this.sendMessage(msg.content);
      },

      reset(): void {
        patchState(store, initialState);
      },
    }),
  ),
);
