"""Orchestrates the conversational intake flow using Ollama + session state."""

from __future__ import annotations

import time

import structlog

from app.models.intake_models import (
    ChatResponse,
    ConversationMessage,
    IntakeSessionData,
)
from app.services.ollama_client import OllamaClient
from app.services.session_manager import SessionManager

log = structlog.get_logger(__name__)

_SYSTEM_PROMPT = """\
You are a compassionate medical intake assistant for a healthcare clinic.
Your goal is to gather the following information from the patient through
friendly conversation:
  1. Chief complaint (main reason for today's visit)
  2. Symptom duration (how long they have had symptoms)
  3. Severity on a scale of 1–10
  4. Current medications
  5. Allergies
  6. Relevant medical history

Ask about one topic at a time. When all fields are collected set
'is_complete' to true. Keep replies concise (≤ 80 words) and empathetic.
Never request personally identifiable information beyond what is listed.
"""

_OPENING = (
    "Hello! I'm your intake assistant. I'll ask you a few quick questions "
    "before your appointment. Let's start — what brings you in today?"
)

_REQUIRED_FIELDS = [
    "chief_complaint",
    "symptom_duration",
    "severity",
    "medications",
    "allergies",
    "medical_history",
]


class IntakeOrchestrationService:
    """Turn-by-turn conversation driver for patient intake."""

    def __init__(
        self,
        session_manager: SessionManager,
        ollama: OllamaClient | None = None,
    ) -> None:
        self._sessions = session_manager
        self._ollama = ollama

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    async def start_session(
        self,
        patient_id: str | None,
        appointment_id: str | None,
    ) -> ChatResponse:
        """Create a new session and return the opening greeting."""
        session = await self._sessions.create(
            patient_id=patient_id,
            appointment_id=appointment_id,
        )
        session.messages.append(
            ConversationMessage(role="assistant", content=_OPENING)
        )
        await self._sessions.save(session)
        return ChatResponse(
            session_id=session.session_id,
            reply=_OPENING,
            is_complete=False,
            collected=session.collected,
        )

    async def handle_message(
        self,
        session: IntakeSessionData,
        user_message: str,
    ) -> ChatResponse:
        """Process a user turn and return the assistant reply."""
        session.messages.append(
            ConversationMessage(role="user", content=user_message)
        )
        session.last_activity = time.time()

        # Try to extract fields from the latest user message
        try:
            await self._extract_fields(session, user_message)
        except Exception:  # non-fatal — intake continues
            log.warning("extract_fields_failed", session_id=session.session_id)

        is_complete = self._all_collected(session)

        # Generate LLM reply
        reply, fallback_required = await self._generate_reply(session, is_complete)

        session.messages.append(
            ConversationMessage(role="assistant", content=reply)
        )
        await self._sessions.save(session)

        return ChatResponse(
            session_id=session.session_id,
            reply=reply,
            is_complete=is_complete,
            collected=session.collected,
            fallback_required=fallback_required,
        )

    # ------------------------------------------------------------------
    # Private helpers
    # ------------------------------------------------------------------

    async def _generate_reply(
        self,
        session: IntakeSessionData,
        is_complete: bool,
    ) -> tuple[str, bool]:
        """Return (reply_text, fallback_required)."""
        if self._ollama is None:
            return self._rule_based_reply(session, is_complete), True

        prompt = self._build_prompt(session, is_complete)
        try:
            reply = await self._ollama.generate(prompt, system=_SYSTEM_PROMPT)
            return reply.strip() or self._rule_based_reply(session, is_complete), False
        except RuntimeError:
            log.warning("ollama_fallback", session_id=session.session_id)
            return self._rule_based_reply(session, is_complete), True

    async def _extract_fields(
        self,
        session: IntakeSessionData,
        user_message: str,
    ) -> None:
        """Attempt a lightweight heuristic field extraction from user input."""
        msg_lower = user_message.lower()

        # Severity: look for a number 1–10
        for token in user_message.split():
            cleaned = token.strip(".,/")
            if cleaned.isdigit() and 1 <= int(cleaned) <= 10:
                if session.collected.get("severity") is None:
                    session.collected["severity"] = cleaned
                    break

        # Chief complaint: capture first non-empty message if not yet set
        if session.collected.get("chief_complaint") is None and user_message.strip():
            session.collected["chief_complaint"] = user_message.strip()

        # Medications: heuristic keyword
        if session.collected.get("medications") is None:
            if any(kw in msg_lower for kw in ("mg", "tablet", "pill", "medication", "prescribed")):
                session.collected["medications"] = user_message.strip()

        # Allergies: heuristic keyword
        if session.collected.get("allergies") is None:
            if any(kw in msg_lower for kw in ("allerg", "react", "intoleran")):
                session.collected["allergies"] = user_message.strip()

    def _all_collected(self, session: IntakeSessionData) -> bool:
        return all(session.collected.get(f) is not None for f in _REQUIRED_FIELDS)

    def _build_prompt(self, session: IntakeSessionData, is_complete: bool) -> str:
        history_lines = [
            f"{m.role.capitalize()}: {m.content}"
            for m in session.messages[-10:]  # last 10 turns
        ]
        collected_summary = ", ".join(
            f"{k}={'?' if v is None else 'done'}" for k, v in session.collected.items()
        )
        return (
            f"Collected fields so far: {collected_summary}\n\n"
            + "\n".join(history_lines)
            + ("\n\nAll fields collected. Provide a warm closing message." if is_complete else "")
        )

    @staticmethod
    def _rule_based_reply(session: IntakeSessionData, is_complete: bool) -> str:
        """Deterministic fallback when Ollama is unavailable."""
        if is_complete:
            return (
                "Thank you for sharing that information. "
                "Your intake is complete — a care team member will be with you shortly."
            )
        for field in _REQUIRED_FIELDS:
            if session.collected.get(field) is None:
                questions = {
                    "chief_complaint": "What brings you in today?",
                    "symptom_duration": "How long have you been experiencing these symptoms?",
                    "severity": "On a scale of 1 to 10, how severe are your symptoms?",
                    "medications": "Are you currently taking any medications?",
                    "allergies": "Do you have any known allergies?",
                    "medical_history": "Do you have any relevant medical history we should know about?",
                }
                return questions.get(field, "Could you tell me more?")
        return "Thank you. Is there anything else you'd like to add?"
