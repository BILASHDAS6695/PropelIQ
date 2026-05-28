# Task 001: Angular — Chat Quick-Reply Chips & Network Error Retry

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-044 |
| **Epic** | EP-006 |
| **Layer** | Angular Frontend — `IntakeChatStore`, `IntakeChatComponent` |
| **Priority** | High |
| **Estimated Effort** | 25 minutes |
| **Dependencies** | None — `IntakeChatStore` and `IntakeChatComponent` exist from US-042 |

## Objective

1. **Track failed message in `IntakeChatStore`** — when `sendMessage` fails, record the index of the failed user message so the UI can show an inline retry affordance
2. **Add `retryLast()` method to `IntakeChatStore`** — removes the failed message from the list and re-sends its text
3. **Add quick-reply suggestion chips to `IntakeChatComponent`** — 6 hardcoded common-response chips rendered above the input row; clicking a chip sends it immediately as the user's message
4. **Show inline "Message not sent — Retry" UI** on the failed message bubble

---

## Acceptance Criteria Covered

- AC: Network error during chat → "Message not sent, tap to retry"
- AC: Quick-reply suggestions for common responses (e.g., "No known allergies")

---

## Design Notes

### Quick-reply chip list

```
'No known allergies'
'No current medications'
'No significant medical history'
'Symptoms started recently'
'Same issue as before'
'Feeling better overall'
```

Chips appear **only** when: `!store.isLoading() && !store.isComplete() && store.messages().length > 0`

After the user sends a message (or clicks a chip), the chips remain visible for the next turn — they are persistent conversation shortcuts, not one-time.

### Retry flow

1. User sends a message → `sendMessage()` fails → toast warning + set `failedAtIndex`
2. The failed user bubble shows inline: `"Message not sent"` (red text) + `"Retry"` button
3. User clicks Retry → `retryLast()` removes the failed bubble and calls `sendMessage()` again
4. On success → `failedAtIndex` is cleared

---

## Implementation Steps

### 1. Extend `IntakeChatStore`

Open `src/health-platform-ui/src/app/features/intake/intake-chat.store.ts`.

**a) Add `failedAtIndex` to state:**

```typescript
interface IntakeChatState {
  messages: ChatMessage[];
  sessionId: string | null;
  isLoading: boolean;
  isComplete: boolean;
  collected: Record<string, string | null>;
  failedAtIndex: number | null;   // new
}

const initialState: IntakeChatState = {
  messages: [],
  sessionId: null,
  isLoading: false,
  isComplete: false,
  collected: {},
  failedAtIndex: null,            // new
};
```

**b) Update `sendMessage` catch block** — replace the existing `toast.error(...)` call with:

```typescript
} catch {
  patchState(store, {
    isLoading: false,
    failedAtIndex: store.messages().length - 1, // index of the user message just added
  });
  toast.warn('Message not sent', 'Tap the Retry button to resend.');
}
```

**c) Add `retryLast()` method** alongside the other methods:

```typescript
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
```

**d) Also clear `failedAtIndex` inside `reset()`:**

`reset()` already calls `patchState(store, initialState)` which includes `failedAtIndex: null` — no change needed.

---

### 2. Update `IntakeChatComponent`

Open `src/health-platform-ui/src/app/features/intake/intake-chat/intake-chat.component.ts`.

**a) Add `QUICK_REPLIES` constant** above the `@Component` decorator:

```typescript
const QUICK_REPLIES = [
  'No known allergies',
  'No current medications',
  'No significant medical history',
  'Symptoms started recently',
  'Same issue as before',
  'Feeling better overall',
] as const;
```

**b) Expose it on the component class** (after `protected inputText = signal('')`):

```typescript
protected readonly quickReplies = QUICK_REPLIES;
```

**c) In the `@for` message loop**, add the inline retry UI immediately after the bubble `<div>`, inside the same `bubble-row` wrapper:

```html
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
```

**d) Add quick-reply chips** between the chat-messages div and the input-row div:

```html
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
```

**e) Update `submit()` to accept an optional override** parameter:

```typescript
protected async submit(override?: string): Promise<void> {
  const text = override ?? this.inputText().trim();
  if (!text) return;
  this.inputText.set('');
  await this.store.sendMessage(text);
}
```

**f) Add CSS** for the new elements — add inside the existing `styles: [...]` block:

```css
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
```

---

### 3. Update unit tests — `intake-chat.component.spec.ts`

Open `src/health-platform-ui/src/app/features/intake/intake-chat/intake-chat.component.spec.ts`.

Add 2 tests after the existing `it('should create', ...)`:

```typescript
it('should expose a non-empty quickReplies list', () => {
  expect(fixture.componentInstance['quickReplies'].length).toBeGreaterThan(0);
});

it('should have retryLast method on the injected store', () => {
  const store = TestBed.inject(IntakeChatStore);
  expect(typeof store.retryLast).toBe('function');
});
```

Add the `IntakeChatStore` import to the spec file imports section if not already present.

---

## Verification

```bash
cd src/health-platform-ui
npx ng test --no-watch
```

Expected: all existing tests pass + 2 new — **33/33** total.

Lint check:

```bash
npx ng lint
```

Expected: `All files pass linting.`

---

## Files Modified

| File | Change |
|------|--------|
| `src/health-platform-ui/src/app/features/intake/intake-chat.store.ts` | Add `failedAtIndex` state; update `sendMessage` catch; add `retryLast()` |
| `src/health-platform-ui/src/app/features/intake/intake-chat/intake-chat.component.ts` | Add quick-reply chips + retry UI + mobile CSS |
| `src/health-platform-ui/src/app/features/intake/intake-chat/intake-chat.component.spec.ts` | Add 2 smoke tests |
