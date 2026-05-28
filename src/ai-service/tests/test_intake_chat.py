"""Tests for the /intake/chat endpoint and IntakeOrchestrationService (Task 002 — US-040)."""

from __future__ import annotations

from unittest.mock import AsyncMock, MagicMock, patch

import pytest
from fastapi.testclient import TestClient

from app.main import app
from app.models.intake_models import IntakeSessionData
from app.services.intake_orchestration_service import IntakeOrchestrationService
from app.services.session_manager import SessionManager

API_KEY_HEADER = {"X-Internal-Api-Key": "test-key"}


# ---------------------------------------------------------------------------
# IntakeOrchestrationService unit tests
# ---------------------------------------------------------------------------


def _make_manager(session: IntakeSessionData | None = None) -> SessionManager:
    redis = MagicMock()
    redis.get = AsyncMock(
        return_value=session.model_dump_json() if session else None
    )
    redis.setex = AsyncMock()
    redis.delete = AsyncMock()
    return SessionManager(redis)


@pytest.mark.asyncio
async def test_start_session_returns_opening_message() -> None:
    manager = _make_manager()
    service = IntakeOrchestrationService(session_manager=manager, ollama=None)

    response = await service.start_session(patient_id="p-1", appointment_id=None)

    assert response.session_id != ""
    assert "today" in response.reply.lower()
    assert response.fallback_required is False


@pytest.mark.asyncio
async def test_handle_message_rule_based_fallback_when_no_ollama() -> None:
    session = IntakeSessionData(session_id="s-1")
    manager = _make_manager(session)
    service = IntakeOrchestrationService(session_manager=manager, ollama=None)

    response = await service.handle_message(session, "I have a headache")

    assert response.session_id == "s-1"
    assert response.reply != ""
    assert response.fallback_required is True


@pytest.mark.asyncio
async def test_handle_message_uses_ollama_reply() -> None:
    session = IntakeSessionData(session_id="s-2")
    manager = _make_manager(session)

    mock_ollama = MagicMock()
    mock_ollama.generate = AsyncMock(return_value="How long have you had the headache?")

    service = IntakeOrchestrationService(session_manager=manager, ollama=mock_ollama)

    response = await service.handle_message(session, "I have a headache")

    assert "headache" in response.reply.lower() or "long" in response.reply.lower()
    assert response.fallback_required is False


@pytest.mark.asyncio
async def test_handle_message_falls_back_when_ollama_raises() -> None:
    session = IntakeSessionData(session_id="s-3")
    manager = _make_manager(session)

    mock_ollama = MagicMock()
    mock_ollama.generate = AsyncMock(side_effect=RuntimeError("Ollama unreachable"))

    service = IntakeOrchestrationService(session_manager=manager, ollama=mock_ollama)

    response = await service.handle_message(session, "chest pain")

    assert response.reply != ""
    assert response.fallback_required is True


# ---------------------------------------------------------------------------
# /intake/chat endpoint integration test
# ---------------------------------------------------------------------------


def test_chat_endpoint_new_session_returns_opening() -> None:
    """POST /intake/chat with no session_id must return HTTP 200 + opening reply."""
    opening_session = IntakeSessionData(session_id="new-sess")

    with (
        patch(
            "app.routers.intake._session_manager.create",
            new=AsyncMock(return_value=opening_session),
        ),
        patch(
            "app.routers.intake._session_manager.save",
            new=AsyncMock(),
        ),
        patch.dict("os.environ", {"INTERNAL_API_KEY": "test-key"}),
    ):
        client = TestClient(app)
        resp = client.post(
            "/intake/chat",
            json={"message": ""},
            headers=API_KEY_HEADER,
        )

    assert resp.status_code == 200
    body = resp.json()
    assert body["session_id"] != ""
    assert isinstance(body["reply"], str)
