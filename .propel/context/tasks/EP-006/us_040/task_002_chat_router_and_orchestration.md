# Task 002: Chat Router & Intake Orchestration Service

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-040 |
| **Epic** | EP-006 |
| **Layer** | Python AI Service — orchestration + router |
| **Priority** | High |
| **Estimated Effort** | 35 minutes |
| **Dependencies** | Task 001 complete — models, SessionManager, OllamaClient in place |

## Objective

1. **`IntakeOrchestrationService`** — drives the conversational intake flow:
   - System prompt construction
   - Question state machine (6 fields: chief complaint → duration → severity → medications → allergies → history)
   - Branching follow-up logic
   - Structured field extraction from LLM reply
   - Skip handling (patient declines/skips)
   - Multi-field extraction (patient covers several questions in one message)
2. **`POST /intake/chat`** endpoint registered in the existing `intake.py` router
3. **LLM-unavailable fallback** — returns HTTP 200 with `fallback_required: true`
4. **Session timeout** — returns 410 Gone when session has timed out
5. **4 unit tests** covering the orchestration logic

---

## Acceptance Criteria Covered

- AC: `POST /intake/chat` accepts message + session context
- AC: Local Ollama LLM used (no external AI APIs)
- AC: Conversational flow gathers 6 structured fields
- AC: System asks follow-up questions based on responses (branching logic)
- AC: Structured data extracted from natural language responses
- AC: Session maintains context across multiple messages
- AC: Conversation timeout → session closed, partial data saved
- AC: API response time < 3 seconds (Ollama inference timeout enforced)
- AC: Nonsensical response → clarification follow-up
- AC: Skipped question → marked "not provided", continue
- AC: LLM unavailable → fallback_required: true
- AC: Multi-field message → extract all, skip covered questions

---

## Design Notes

### Question flow state machine

The orchestrator determines the **next unanswered field** in order:

```
FIELD_ORDER = [
    "chief_complaint",
    "symptom_duration",
    "severity",
    "medications",
    "allergies",
    "medical_history",
]
```

When all 6 fields are non-null (value or `"not provided"`), `is_complete = True`.

### System prompt

```
You are a medical intake assistant for a healthcare clinic.
Your role is to gather pre-visit information from patients conversationally.
Ask ONE question at a time. Be empathetic and concise.
When the patient's answer is unclear, ask a gentle clarifying follow-up.
If the patient says they want to skip or don't know, acknowledge and move on.
Extract structured information from the patient's responses.
Never provide medical advice. Never diagnose. Refer urgent symptoms to emergency services.
Current fields still needed: {pending_fields}
```

### Extraction prompt (separate call after each user message)

After the main reply is generated, a second Ollama call extracts structured JSON:

```
Given this patient message: "{user_message}"
And the ongoing conversation context, extract any of these fields if mentioned:
chief_complaint, symptom_duration, severity (1-10 or descriptive), medications (list),
allergies (list), medical_history (list).
Respond ONLY with a JSON object. Use null for fields not mentioned.
Example: {"chief_complaint": "headache", "severity": "7", "medications": null}
```

Parse the JSON; ignore fields that are already collected.

---

## Implementation Steps

### 1. Create `app/services/intake_orchestration_service.py`

```python
"""Intake conversational orchestration — drives the AI chat flow."""

from __future__ import annotations

import json
import re

import structlog

from app.models.intake_models import ConversationMessage, IntakeSessionData
from app.services.ollama_client import OllamaClient

logger = structlog.get_logger(__name__)

FIELD_ORDER = [
    "chief_complaint",
    "symptom_duration",
    "severity",
    "medications",
    "allergies",
    "medical_history",
]

FIELD_QUESTIONS: dict[str, str] = {
    "chief_complaint": "What brings you in today? Please describe your main concern.",
    "symptom_duration": "How long have you been experiencing this?",
    "severity": "On a scale of 1 to 10, how would you rate the severity?",
    "medications": "Are you currently taking any medications? If none, just say none.",
    "allergies": "Do you have any known allergies (medications, foods, or environmental)?",
    "medical_history": "Do you have any significant past medical history or chronic conditions?",
}

_SKIP_PHRASES = {
    "skip", "pass", "don't know", "not sure", "no idea",
    "prefer not", "n/a", "na", "none", "nothing",
}

_SYSTEM_PROMPT_TEMPLATE = """\
You are a medical intake assistant for a healthcare clinic.
Your role is to gather pre-visit information from patients conversationally.
Ask ONE question at a time. Be empathetic and concise (2-3 sentences max).
When the patient's answer is unclear, ask a gentle clarifying follow-up.
If the patient says they want to skip or don't know, acknowledge and move on.
Never provide medical advice. Never diagnose.
If the patient describes a life-threatening emergency, tell them to call 911 immediately.
Pending fields to collect: {pending_fields}
"""

_EXTRACT_PROMPT_TEMPLATE = """\
Given this patient message: "{user_message}"
Extract any of the following fields if clearly mentioned:
chief_complaint, symptom_duration, severity (number 1-10 or descriptive word), \
medications (comma-separated list), allergies (comma-separated list), \
medical_history (comma-separated list).
Respond ONLY with a compact JSON object. Use null for fields not mentioned.
Example: {{"chief_complaint": "headache", "severity": "7", "medications": null}}
"""


class IntakeOrchestrationService:
    """Drives the conversational intake flow using Ollama and session state."""

    def __init__(self, ollama: OllamaClient | None = None) -> None:
        self._ollama = ollama or OllamaClient()

    def _pending_fields(self, session: IntakeSessionData) -> list[str]:
        return [f for f in FIELD_ORDER if session.collected.get(f) is None]

    def _is_skip(self, text: str) -> bool:
        lower = text.lower().strip()
        return any(phrase in lower for phrase in _SKIP_PHRASES) and len(lower.split()) <= 6

    def get_opening_message(self) -> str:
        return (
            "Hello! I'm here to help gather some information before your visit. "
            + FIELD_QUESTIONS["chief_complaint"]
        )

    async def process_turn(
        self,
        session: IntakeSessionData,
        user_message: str,
    ) -> tuple[str, bool]:
        """Process one user turn. Returns (assistant_reply, is_complete).

        Side-effect: mutates session.collected and session.messages.
        """
        # Append user message to history
        session.messages.append(
            ConversationMessage(role="user", content=user_message)
        )

        # --- Try to extract structured fields from the user message ---
        await self._extract_fields(session, user_message)

        # Check if complete after extraction
        pending = self._pending_fields(session)
        if not pending:
            reply = (
                "Thank you! I've gathered all the information I need for your visit. "
                "Your care team will review this before your appointment."
            )
            session.messages.append(ConversationMessage(role="assistant", content=reply))
            return reply, True

        # --- Handle skip ---
        current_field = pending[0]
        if self._is_skip(user_message) and len(session.messages) > 1:
            session.collected[current_field] = "not provided"
            pending = self._pending_fields(session)
            if not pending:
                reply = (
                    "No problem at all. I have everything I need. "
                    "Your care team will be ready for your visit."
                )
                session.messages.append(
                    ConversationMessage(role="assistant", content=reply)
                )
                return reply, True
            reply = f"Understood, no problem. {FIELD_QUESTIONS[pending[0]]}"
            session.messages.append(ConversationMessage(role="assistant", content=reply))
            return reply, False

        # --- Generate conversational reply from Ollama ---
        pending_after = self._pending_fields(session)
        system_prompt = _SYSTEM_PROMPT_TEMPLATE.format(
            pending_fields=", ".join(pending_after) if pending_after else "none"
        )
        messages_payload = [{"role": "system", "content": system_prompt}]
        for msg in session.messages[-12:]:  # cap history at 12 turns to keep context short
            messages_payload.append({"role": msg.role, "content": msg.content})

        reply = await self._ollama.chat(messages_payload)

        session.messages.append(ConversationMessage(role="assistant", content=reply))
        return reply, False

    async def _extract_fields(
        self, session: IntakeSessionData, user_message: str
    ) -> None:
        """Call Ollama to extract structured fields; update session.collected."""
        extract_prompt = _EXTRACT_PROMPT_TEMPLATE.format(user_message=user_message)
        try:
            raw = await self._ollama.chat(
                [{"role": "user", "content": extract_prompt}],
                temperature=0.0,
            )
            # Extract JSON substring (LLM may wrap in backticks)
            json_match = re.search(r"\{.*?\}", raw, re.DOTALL)
            if not json_match:
                return
            extracted: dict[str, str | None] = json.loads(json_match.group())
            for field, value in extracted.items():
                if field in session.collected and session.collected[field] is None:
                    if value and str(value).strip():
                        session.collected[field] = str(value).strip()
        except Exception:  # noqa: BLE001
            # Extraction failure is non-fatal; conversation continues
            logger.warning("field_extraction_failed", message_preview=user_message[:80])
```

### 2. Update `app/routers/intake.py`

Replace the entire file:

```python
"""Intake router — conversational chat + NLP parse/classify endpoints."""

from __future__ import annotations

import structlog
from fastapi import APIRouter, HTTPException

from app.models import (
    ChatRequest,
    ChatResponse,
    IntakeClassifyRequest,
    IntakeClassifyResponse,
    IntakeParseRequest,
    IntakeParseResponse,
)
from app.services.intake_orchestration_service import IntakeOrchestrationService
from app.services.ollama_client import OllamaClient
from app.services.session_manager import SessionManager

router = APIRouter()
logger = structlog.get_logger(__name__)

# Module-level singletons (FastAPI DI not needed at this scale)
_session_manager = SessionManager()
_ollama_client = OllamaClient()
_orchestrator = IntakeOrchestrationService(ollama=_ollama_client)


@router.post("/chat", response_model=ChatResponse)
async def intake_chat(request: ChatRequest) -> ChatResponse:
    """Conversational AI intake — one turn at a time.

    - Omit session_id on first message; include on subsequent turns.
    - Returns fallback_required=true if Ollama is unavailable.
    - Returns HTTP 410 if the session has timed out.
    """
    # --- Resolve or create session ---
    if request.session_id:
        session = await _session_manager.get(request.session_id)
        if session is None:
            raise HTTPException(status_code=410, detail="Session expired or not found.")
        if _session_manager.is_timed_out(session):
            await _session_manager.delete(request.session_id)
            raise HTTPException(status_code=410, detail="Session timed out.")
    else:
        session = await _session_manager.create(
            patient_id=request.patient_id,
            appointment_id=request.appointment_id,
        )
        # First turn: return opening greeting, don't process message yet
        opening = _orchestrator.get_opening_message()
        await _session_manager.save(session)
        return ChatResponse(
            session_id=session.session_id,
            reply=opening,
            is_complete=False,
            collected=session.collected,
        )

    # --- Process the user's message ---
    try:
        reply, is_complete = await _orchestrator.process_turn(session, request.message)
    except RuntimeError:
        # Ollama unavailable — return fallback signal
        logger.warning("ollama_unavailable_fallback", session_id=session.session_id)
        return ChatResponse(
            session_id=session.session_id,
            reply="I'm having trouble processing your response right now.",
            is_complete=False,
            collected=session.collected,
            fallback_required=True,
        )

    await _session_manager.save(session)

    if is_complete:
        await _session_manager.delete(session.session_id)

    return ChatResponse(
        session_id=session.session_id,
        reply=reply,
        is_complete=is_complete,
        collected=session.collected,
    )


@router.post("/parse", response_model=IntakeParseResponse)
async def parse_intake(request: IntakeParseRequest) -> IntakeParseResponse:
    """NLP parsing of intake form text — placeholder."""
    raise NotImplementedError


@router.post("/classify", response_model=IntakeClassifyResponse)
async def classify_intake(request: IntakeClassifyRequest) -> IntakeClassifyResponse:
    """Classification of intake data — placeholder."""
    raise NotImplementedError
```

### 3. Unit tests — `tests/test_intake_chat.py`

```python
"""Unit tests for IntakeOrchestrationService and /intake/chat endpoint."""

from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from fastapi.testclient import TestClient

from app.main import app
from app.models.intake_models import ConversationMessage, IntakeSessionData
from app.services.intake_orchestration_service import IntakeOrchestrationService

API_KEY = "test-key"
HEADERS = {"X-Internal-Api-Key": API_KEY}


# --- Orchestration unit tests ---

def _make_orchestrator(reply: str = "Mock reply") -> tuple[IntakeOrchestrationService, MagicMock]:
    mock_ollama = MagicMock()
    mock_ollama.chat = AsyncMock(
        side_effect=[
            '{"chief_complaint": "headache"}',  # extraction call
            reply,                               # conversational reply call
        ]
    )
    return IntakeOrchestrationService(ollama=mock_ollama), mock_ollama


@pytest.mark.asyncio
async def test_process_turn_extracts_chief_complaint():
    orchestrator, _ = _make_orchestrator('{"chief_complaint": "headache"}')
    session = IntakeSessionData(session_id="s-001")
    await orchestrator.process_turn(session, "I have a bad headache")
    assert session.collected["chief_complaint"] == "headache"


@pytest.mark.asyncio
async def test_process_turn_skip_marks_not_provided():
    mock_ollama = MagicMock()
    mock_ollama.chat = AsyncMock(return_value="{}")  # extraction returns nothing
    orchestrator = IntakeOrchestrationService(ollama=mock_ollama)
    session = IntakeSessionData(session_id="s-002")
    # Simulate already having had a turn (so skip is recognised)
    session.messages.append(ConversationMessage(role="assistant", content="What brings you in?"))
    reply, is_complete = await orchestrator.process_turn(session, "skip")
    assert session.collected["chief_complaint"] == "not provided"
    assert not is_complete


@pytest.mark.asyncio
async def test_process_turn_completes_when_all_collected():
    mock_ollama = MagicMock()
    mock_ollama.chat = AsyncMock(return_value="{}")
    orchestrator = IntakeOrchestrationService(ollama=mock_ollama)
    session = IntakeSessionData(session_id="s-003")
    # Pre-fill 5 of 6 fields
    session.collected = {
        "chief_complaint": "headache",
        "symptom_duration": "2 days",
        "severity": "7",
        "medications": "none",
        "allergies": "none",
        "medical_history": None,
    }
    mock_ollama.chat = AsyncMock(return_value='{"medical_history": "diabetes"}')
    reply, is_complete = await orchestrator.process_turn(session, "I have diabetes")
    assert is_complete is True
    assert session.collected["medical_history"] == "diabetes"


# --- Endpoint integration test ---

@pytest.fixture()
def client_with_env(monkeypatch: pytest.MonkeyPatch) -> TestClient:
    monkeypatch.setenv("INTERNAL_API_KEY", API_KEY)
    monkeypatch.setenv("REDIS_URL", "redis://localhost:6379/0")
    monkeypatch.setenv("OLLAMA_BASE_URL", "http://localhost:11434")
    return TestClient(app)


def test_chat_endpoint_new_session_returns_opening(client_with_env: TestClient):
    from app.models.intake_models import IntakeSessionData
    mock_session = IntakeSessionData(session_id="sess-new")

    with (
        patch("app.routers.intake._session_manager.create", new=AsyncMock(return_value=mock_session)),
        patch("app.routers.intake._session_manager.save", new=AsyncMock()),
    ):
        resp = client_with_env.post(
            "/intake/chat",
            json={"message": "Hello"},
            headers=HEADERS,
        )
    assert resp.status_code == 200
    body = resp.json()
    assert body["session_id"] == "sess-new"
    assert "chief_complaint" in body["reply"].lower() or len(body["reply"]) > 10
    assert body["fallback_required"] is False
```

---

## Verification

```bash
cd src/ai-service
pytest tests/test_intake_chat.py -v
```

Expected: **4 tests pass** (3 orchestration + 1 endpoint).
