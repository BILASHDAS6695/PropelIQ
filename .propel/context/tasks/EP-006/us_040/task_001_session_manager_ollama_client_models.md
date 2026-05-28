# Task 001: Session Manager, Ollama Client & Chat Models

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-040 |
| **Epic** | EP-006 |
| **Layer** | Python AI Service — models, config, services |
| **Priority** | High |
| **Estimated Effort** | 30 minutes |
| **Dependencies** | ai-service scaffold in place (`src/ai-service/`) |

## Objective

Extend the existing `ai-service` with:
1. **Pydantic models** for the `/intake/chat` request/response contract and session data
2. **`Settings` extension** — add `ollama_base_url`, `redis_url`, `ollama_model` with safe defaults
3. **`OllamaClient`** — async `httpx` wrapper around Ollama `/api/chat`
4. **`SessionManager`** — Redis-backed conversation session store (30-min TTL)
5. **`requirements.txt` additions** — `redis[hiredis]` and `httpx`
6. **3 unit tests** for `SessionManager` (create, retrieve, expiry guard)

---

## Acceptance Criteria Covered

- AC: Session maintains context across multiple messages (conversation history in Redis)
- AC: Conversation timeout: 30 minutes of inactivity → session closed, partial data saved
- AC: Local Ollama LLM used for NLP (no external AI APIs per ADR-004)

---

## Implementation Steps

### 1. Add dependencies to `requirements.txt`

Append to `src/ai-service/requirements.txt`:

```
redis[hiredis]==5.0.7
httpx==0.27.0
```

### 2. Extend `app/config.py`

```python
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    internal_api_key: str
    port: int = 8000
    log_level: str = "INFO"

    # Ollama (local LLM — ADR-004)
    ollama_base_url: str = "http://localhost:11434"
    ollama_model: str = "llama3"

    # Redis (session store)
    redis_url: str = "redis://localhost:6379/0"

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")


settings = Settings()  # type: ignore[call-arg]
```

Update `.env.example`:
```
INTERNAL_API_KEY=changeme
PORT=8000
LOG_LEVEL=INFO
OLLAMA_BASE_URL=http://localhost:11434
OLLAMA_MODEL=llama3
REDIS_URL=redis://localhost:6379/0
```

### 3. Add chat models to `app/models/intake_models.py`

Append to the existing file (do NOT remove existing models):

```python
# --- Conversational Chat ---

class ConversationMessage(BaseModel):
    """A single turn in the conversation history."""

    role: str = Field(..., description="'user' or 'assistant'")
    content: str


class IntakeSessionData(BaseModel):
    """Full session state stored in Redis."""

    session_id: str
    patient_id: str | None = None
    appointment_id: str | None = None
    messages: list[ConversationMessage] = Field(default_factory=list)
    collected: dict[str, str | None] = Field(
        default_factory=lambda: {
            "chief_complaint": None,
            "symptom_duration": None,
            "severity": None,
            "medications": None,
            "allergies": None,
            "medical_history": None,
        }
    )
    last_activity: float = Field(default_factory=lambda: __import__("time").time())


class ChatRequest(BaseModel):
    """Request body for POST /intake/chat."""

    session_id: str | None = Field(
        default=None,
        description="Omit on first message; include on subsequent turns.",
    )
    message: str = Field(..., min_length=1, max_length=4000)
    patient_id: str | None = None
    appointment_id: str | None = None


class ChatResponse(BaseModel):
    """Response body for POST /intake/chat."""

    session_id: str
    reply: str
    is_complete: bool = False
    collected: dict[str, str | None] = Field(default_factory=dict)
    fallback_required: bool = False
```

Update `app/models/__init__.py` to export the new models:
```python
from app.models.intake_models import (
    ChatRequest,
    ChatResponse,
    ConversationMessage,
    IntakeSessionData,
    IntakeCategory,
    IntakeClassifyRequest,
    IntakeClassifyResponse,
    IntakeParseRequest,
    IntakeParseResponse,
)

__all__ = [
    "ChatRequest",
    "ChatResponse",
    "ConversationMessage",
    "IntakeSessionData",
    "IntakeCategory",
    "IntakeClassifyRequest",
    "IntakeClassifyResponse",
    "IntakeParseRequest",
    "IntakeParseResponse",
]
```

### 4. Create `app/services/ollama_client.py`

```python
"""Ollama async HTTP client — local LLM inference (ADR-004)."""

from __future__ import annotations

import httpx
import structlog

from app.config import settings

logger = structlog.get_logger(__name__)

_TIMEOUT_SECONDS = 30.0


class OllamaClient:
    """Sends chat completion requests to a local Ollama instance."""

    def __init__(self) -> None:
        self._base_url = settings.ollama_base_url.rstrip("/")
        self._model = settings.ollama_model

    async def chat(
        self,
        messages: list[dict[str, str]],
        *,
        temperature: float = 0.3,
    ) -> str:
        """Send a chat request and return the assistant reply text.

        Args:
            messages: List of {"role": "user"|"assistant"|"system", "content": str}.
            temperature: Sampling temperature (lower = more deterministic).

        Returns:
            The assistant's reply as a plain string.

        Raises:
            RuntimeError: If Ollama is unreachable or returns a non-200 response.
        """
        payload = {
            "model": self._model,
            "messages": messages,
            "stream": False,
            "options": {"temperature": temperature},
        }
        try:
            async with httpx.AsyncClient(timeout=_TIMEOUT_SECONDS) as client:
                response = await client.post(
                    f"{self._base_url}/api/chat",
                    json=payload,
                )
                response.raise_for_status()
                data = response.json()
                return str(data["message"]["content"])
        except httpx.HTTPError as exc:
            logger.error("ollama_http_error", error=str(exc))
            raise RuntimeError("Ollama unavailable") from exc
```

### 5. Create `app/services/session_manager.py`

```python
"""Redis-backed conversation session manager."""

from __future__ import annotations

import json
import time
import uuid

import redis.asyncio as aioredis
import structlog

from app.config import settings
from app.models.intake_models import IntakeSessionData

logger = structlog.get_logger(__name__)

SESSION_TTL_SECONDS = 1800  # 30 minutes


class SessionManager:
    """Stores and retrieves IntakeSessionData in Redis with a 30-minute TTL."""

    def __init__(self, redis_client: aioredis.Redis | None = None) -> None:
        self._redis: aioredis.Redis = redis_client or aioredis.from_url(
            settings.redis_url, decode_responses=True
        )

    def _key(self, session_id: str) -> str:
        return f"intake:session:{session_id}"

    async def create(
        self,
        patient_id: str | None = None,
        appointment_id: str | None = None,
    ) -> IntakeSessionData:
        """Create a new session and persist it. Returns the new session."""
        session = IntakeSessionData(
            session_id=str(uuid.uuid4()),
            patient_id=patient_id,
            appointment_id=appointment_id,
        )
        await self._save(session)
        logger.info("session_created", session_id=session.session_id)
        return session

    async def get(self, session_id: str) -> IntakeSessionData | None:
        """Retrieve a session. Returns None if not found or expired."""
        raw = await self._redis.get(self._key(session_id))
        if raw is None:
            return None
        return IntakeSessionData.model_validate(json.loads(raw))

    async def save(self, session: IntakeSessionData) -> None:
        """Persist session state and reset the TTL."""
        session.last_activity = time.time()
        await self._save(session)

    async def _save(self, session: IntakeSessionData) -> None:
        await self._redis.setex(
            self._key(session.session_id),
            SESSION_TTL_SECONDS,
            session.model_dump_json(),
        )

    async def delete(self, session_id: str) -> None:
        """Explicitly close a session (e.g., on completion or timeout)."""
        await self._redis.delete(self._key(session_id))
        logger.info("session_deleted", session_id=session_id)

    def is_timed_out(self, session: IntakeSessionData) -> bool:
        """Return True if the session has exceeded the inactivity timeout."""
        return (time.time() - session.last_activity) > SESSION_TTL_SECONDS
```

### 6. Unit tests — `tests/test_session_manager.py`

```python
"""Unit tests for SessionManager."""

from __future__ import annotations

import time
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

from app.models.intake_models import IntakeSessionData
from app.services.session_manager import SESSION_TTL_SECONDS, SessionManager


def _make_mock_redis() -> MagicMock:
    mock = MagicMock()
    mock.setex = AsyncMock()
    mock.get = AsyncMock(return_value=None)
    mock.delete = AsyncMock()
    return mock


@pytest.mark.asyncio
async def test_create_returns_session_with_id():
    mock_redis = _make_mock_redis()
    mgr = SessionManager(redis_client=mock_redis)
    session = await mgr.create(patient_id="p-001")
    assert session.session_id
    assert session.patient_id == "p-001"
    mock_redis.setex.assert_awaited_once()


@pytest.mark.asyncio
async def test_get_returns_none_when_key_missing():
    mock_redis = _make_mock_redis()
    mock_redis.get = AsyncMock(return_value=None)
    mgr = SessionManager(redis_client=mock_redis)
    result = await mgr.get("nonexistent-id")
    assert result is None


@pytest.mark.asyncio
async def test_is_timed_out_returns_true_for_stale_session():
    mgr = SessionManager(redis_client=_make_mock_redis())
    stale = IntakeSessionData(
        session_id="s-001",
        last_activity=time.time() - SESSION_TTL_SECONDS - 1,
    )
    assert mgr.is_timed_out(stale) is True


@pytest.mark.asyncio
async def test_is_timed_out_returns_false_for_fresh_session():
    mgr = SessionManager(redis_client=_make_mock_redis())
    fresh = IntakeSessionData(session_id="s-002")
    assert mgr.is_timed_out(fresh) is False
```

---

## Verification

```bash
cd src/ai-service
pip install -r requirements.txt
pytest tests/test_session_manager.py -v
```

Expected: **4 tests pass**.
