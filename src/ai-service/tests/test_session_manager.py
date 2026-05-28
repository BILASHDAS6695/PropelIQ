"""Unit tests for SessionManager (Task 001 — US-040)."""

from __future__ import annotations

import time
from unittest.mock import AsyncMock, MagicMock

import pytest

from app.models.intake_models import IntakeSessionData
from app.services.session_manager import (
    SESSION_TIMEOUT_SECONDS,
    SessionManager,
)


def _make_redis() -> MagicMock:
    redis = MagicMock()
    redis.get = AsyncMock(return_value=None)
    redis.setex = AsyncMock()
    redis.delete = AsyncMock()
    return redis


@pytest.mark.asyncio
async def test_create_returns_session_with_id() -> None:
    redis = _make_redis()
    manager = SessionManager(redis)

    session = await manager.create(patient_id="p-1", appointment_id="a-1")

    assert session.session_id != ""
    assert session.patient_id == "p-1"
    assert session.appointment_id == "a-1"
    redis.setex.assert_awaited_once()


@pytest.mark.asyncio
async def test_get_returns_none_when_not_found() -> None:
    redis = _make_redis()
    manager = SessionManager(redis)

    result = await manager.get("non-existent-id")

    assert result is None


@pytest.mark.asyncio
async def test_save_and_get_round_trip() -> None:
    session = IntakeSessionData(session_id="sess-1", patient_id="p-2")
    stored: dict[str, str] = {}

    async def fake_setex(key: str, ttl: int, value: str) -> None:
        stored[key] = value

    async def fake_get(key: str) -> str | None:
        return stored.get(key)

    redis = _make_redis()
    redis.setex = AsyncMock(side_effect=fake_setex)
    redis.get = AsyncMock(side_effect=fake_get)

    manager = SessionManager(redis)
    await manager.save(session)
    retrieved = await manager.get("sess-1")

    assert retrieved is not None
    assert retrieved.session_id == "sess-1"
    assert retrieved.patient_id == "p-2"


def test_is_timed_out_returns_true_for_old_session() -> None:
    manager = SessionManager(MagicMock())
    old_session = IntakeSessionData(
        session_id="old",
        last_activity=time.time() - SESSION_TIMEOUT_SECONDS - 1,
    )
    assert manager.is_timed_out(old_session) is True


def test_is_timed_out_returns_false_for_fresh_session() -> None:
    manager = SessionManager(MagicMock())
    fresh_session = IntakeSessionData(
        session_id="fresh",
        last_activity=time.time(),
    )
    assert manager.is_timed_out(fresh_session) is False
