# Task 003: .NET Proxy Controller & Angular Chat UI

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-040 |
| **Epic** | EP-006 |
| **Layer** | .NET API + Angular Frontend |
| **Priority** | High |
| **Estimated Effort** | 35 minutes |
| **Dependencies** | Task 002 complete — `POST /intake/chat` live in ai-service |

## Objective

1. **.NET Infrastructure** — `AiServiceClient` (HttpClient wrapper) + registered `HttpClient`
2. **.NET API** — `IntakeController` with `POST /api/intake/chat` that proxies to ai-service
3. **Angular** — `IntakeService` + `IntakeChatStore` (ngrx/signals) + `IntakeChatComponent` (chat bubble UI)
4. Replace the stub `IntakeLandingComponent` with the real chat component; update `intake.routes.ts`
5. **1 Angular unit test** — component smoke test

---

## Acceptance Criteria Covered

- AC: Conversational flow rendered in-browser
- AC: Session maintained across message turns
- AC: `fallback_required: true` → redirect to structured form (US-041 stub)
- AC: No PHI sent to external services (all local, proxied through .NET API → ai-service)

---

## Design Notes

### .NET proxy pattern

The Angular app calls `POST /api/intake/chat` on the .NET API. The .NET API:
1. Validates the JWT (existing auth middleware)
2. Forwards the request body to `http://ai-service:8000/intake/chat` with the `X-Internal-Api-Key` header
3. Returns the ai-service response verbatim

This keeps the Angular app's same-origin rule intact and prevents direct browser access to the ai-service.

### Angular chat UI

- Messages displayed as chat bubbles: user messages right-aligned, assistant left-aligned
- PrimeNG components: `InputTextModule`, `ButtonModule`, `SkeletonModule`
- "Sending…" skeleton shown while `isLoading` is true
- On `is_complete: true` → show a success card with collected fields summary
- On `fallback_required: true` → show toast warning + navigate to `/intake/form`
- `Enter` key submits message

---

## Implementation Steps

### 1. Register AiService HttpClient in .NET DI

In `src/HealthPlatform.Infrastructure/DependencyInjection.cs`, add inside the method:

```csharp
services.AddHttpClient("AiService", client =>
{
    client.BaseAddress = new Uri(configuration["AiService:BaseUrl"]
        ?? "http://localhost:8000");
    client.DefaultRequestHeaders.Add(
        "X-Internal-Api-Key",
        configuration["AiService:InternalApiKey"] ?? string.Empty);
    client.Timeout = TimeSpan.FromSeconds(35);
});
```

Add to `appsettings.Development.json` (under existing keys):
```json
"AiService": {
  "BaseUrl": "http://localhost:8000",
  "InternalApiKey": "changeme"
}
```

### 2. Create `IntakeChatProxyRequest` and `IntakeChatProxyResponse` DTOs

Create `src/HealthPlatform.Application/Features/Intake/IntakeChatDtos.cs`:

```csharp
namespace HealthPlatform.Application.Features.Intake;

public record IntakeChatProxyRequest(
    string? SessionId,
    string Message,
    string? PatientId,
    string? AppointmentId);

public record IntakeChatProxyResponse(
    string SessionId,
    string Reply,
    bool IsComplete,
    Dictionary<string, string?> Collected,
    bool FallbackRequired);
```

### 3. Create `IntakeController`

Create `src/HealthPlatform.Api/Controllers/IntakeController.cs`:

```csharp
using System.Net.Http.Json;
using HealthPlatform.Application.Features.Intake;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthPlatform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class IntakeController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IntakeController> _logger;

    public IntakeController(
        IHttpClientFactory httpClientFactory,
        ILogger<IntakeController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpPost("chat")]
    public async Task<ActionResult<IntakeChatProxyResponse>> Chat(
        [FromBody] IntakeChatProxyRequest request,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("AiService");
        try
        {
            var aiResponse = await client.PostAsJsonAsync(
                "/intake/chat", request, cancellationToken);

            if (!aiResponse.IsSuccessStatusCode)
            {
                var status = (int)aiResponse.StatusCode;
                _logger.LogWarning(
                    "AI service returned {StatusCode} for intake/chat", status);
                return StatusCode(status, new { detail = "Upstream AI service error." });
            }

            var result = await aiResponse.Content
                .ReadFromJsonAsync<IntakeChatProxyResponse>(cancellationToken: cancellationToken);
            return Ok(result);
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("AI service timeout on intake/chat");
            return StatusCode(504, new { detail = "AI service timed out." });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "AI service unreachable on intake/chat");
            return StatusCode(503, new { detail = "AI service unavailable." });
        }
    }
}
```

### 4. Create Angular `intake.models.ts`

Create `src/health-platform-ui/src/app/core/models/intake.models.ts`:

```typescript
export interface IntakeChatRequest {
  sessionId?: string | null;
  message: string;
  patientId?: string | null;
  appointmentId?: string | null;
}

export interface IntakeChatResponse {
  sessionId: string;
  reply: string;
  isComplete: boolean;
  collected: Record<string, string | null>;
  fallbackRequired: boolean;
}
```

### 5. Create `IntakeService`

Create `src/health-platform-ui/src/app/core/services/intake.service.ts`:

```typescript
import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { IntakeChatRequest, IntakeChatResponse } from '../models/intake.models';

@Injectable({ providedIn: 'root' })
export class IntakeService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  chat(request: IntakeChatRequest): Observable<IntakeChatResponse> {
    return this.http.post<IntakeChatResponse>(`${this.base}/intake/chat`, request);
  }
}
```

### 6. Create `IntakeChatStore`

Create `src/health-platform-ui/src/app/features/intake/intake-chat.store.ts`:

```typescript
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
  isLoading: boolean;
  isComplete: boolean;
  collected: Record<string, string | null>;
}

const initialState: IntakeChatState = {
  messages: [],
  sessionId: null,
  isLoading: false,
  isComplete: false,
  collected: {},
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
        patchState(store, { isLoading: true });
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
          });

          if (response.fallbackRequired) {
            toast.warn('Notice', 'Switching to form-based intake.');
            void router.navigate(['/intake/form']);
          }
        } catch {
          patchState(store, { isLoading: false });
          toast.error('Error', 'Failed to send message. Please try again.');
        }
      },

      reset(): void {
        patchState(store, initialState);
      },
    }),
  ),
);
```

### 7. Create `IntakeChatComponent`

Create `src/health-platform-ui/src/app/features/intake/intake-chat/intake-chat.component.ts`:

```typescript
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
          <div class="bubble-row" [class.user]="msg.role === 'user'" [class.assistant]="msg.role === 'assistant'">
            <div class="bubble" [class.user]="msg.role === 'user'" [class.assistant]="msg.role === 'assistant'">
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

  protected collectedEntries() {
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
```

### 8. Update `intake.routes.ts`

Replace with:

```typescript
import { Routes } from '@angular/router';

export const INTAKE_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./intake-chat/intake-chat.component').then((m) => m.IntakeChatComponent),
  },
  {
    path: 'form',
    loadComponent: () =>
      import('./intake-landing/intake-landing.component').then((m) => m.IntakeLandingComponent),
  },
];
```

### 9. Angular unit test — `intake-chat.component.spec.ts`

Create `src/health-platform-ui/src/app/features/intake/intake-chat/intake-chat.component.spec.ts`:

```typescript
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { MessageService } from 'primeng/api';
import { IntakeChatComponent } from './intake-chat.component';
import { IntakeChatStore } from '../intake-chat.store';

describe('IntakeChatComponent', () => {
  let fixture: ComponentFixture<IntakeChatComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IntakeChatComponent],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        provideNoopAnimations(),
        MessageService,
        IntakeChatStore,
      ],
    }).compileComponents();
    fixture = TestBed.createComponent(IntakeChatComponent);
  });

  it('should create', () => {
    expect(fixture.componentInstance).toBeTruthy();
  });
});
```

---

## Verification

```bash
# Angular
cd src/health-platform-ui
npx ng build
npx ng lint
npx ng test --no-watch

# .NET
cd src
dotnet build
```

Expected:
- Angular: build clean, lint clean, all tests pass (18 total)
- .NET: build succeeds, no new errors
