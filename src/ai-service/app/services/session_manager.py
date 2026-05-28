"""Redis-backed intake session manager."""

from __future__ import annotations

import time
import uuid

import structlog

from app.models.intake_models import IntakeSessionData

log = structlog.get_logger(__name__)

SESSION_TTL_SECONDS = 30 * 60  # 30 minutes
SESSION_TIMEOUT_SECONDS = 30 * 60


def _key(session_id: str) -> str:
    return f"intake:session:{session_id}"


class SessionManager:
    """Create, persist, and expire conversational intake sessions in Redis."""

    def __init__(self, redis_client) -> None:  # type: ignore[type-arg]
        self._redis = redis_client

    # ------------------------------------------------------------------
    # CRUD
    # ------------------------------------------------------------------

    async def create(
        self,
        patient_id: str | None = None,
        appointment_id: str | None = None,
    ) -> IntakeSessionData:
        """Allocate a new session and persist it."""
        session = IntakeSessionData(
            session_id=str(uuid.uuid4()),
            patient_id=patient_id,
            appointment_id=appointment_id,
            last_activity=time.time(),
        )
        await self.save(session)
        log.info("session_created", session_id=session.session_id)
        return session

    async def get(self, session_id: str) -> IntakeSessionData | None:
        """Return the session or *None* if not found."""
        raw = await self._redis.get(_key(session_id))
        if raw is None:
            return None
        return IntakeSessionData.model_validate_json(raw)

    async def save(self, session: IntakeSessionData) -> None:
        """Persist session with sliding TTL."""
        await self._redis.setex(
            _key(session.session_id),
            SESSION_TTL_SECONDS,
            session.model_dump_json(),
        )

    async def delete(self, session_id: str) -> None:
        """Remove session from Redis."""
        await self._redis.delete(_key(session_id))
        log.info("session_deleted", session_id=session_id)

    # ------------------------------------------------------------------
    # Helpers
    # ------------------------------------------------------------------

    def is_timed_out(self, session: IntakeSessionData) -> bool:
        """Return True if the session has been idle beyond the timeout."""
        return (time.time() - session.last_activity) > SESSION_TIMEOUT_SECONDS
