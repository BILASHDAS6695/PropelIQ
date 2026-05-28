"""Pydantic v2 models for the /intake domain."""

from __future__ import annotations

from enum import StrEnum

from pydantic import BaseModel, Field


class IntakeCategory(StrEnum):
    ROUTINE = "routine"
    URGENT = "urgent"
    EMERGENCY = "emergency"
    UNKNOWN = "unknown"


# --- Parse ---

class IntakeParseRequest(BaseModel):
    """Request body for POST /intake/parse."""

    form_text: str = Field(
        ...,
        description="Raw text captured from the patient intake form.",
        min_length=1,
    )


class IntakeParseResponse(BaseModel):
    """Response body for POST /intake/parse."""

    chief_complaint: str | None = Field(default=None)
    symptoms: list[str] = Field(default_factory=list)
    medications: list[str] = Field(default_factory=list)
    allergies: list[str] = Field(default_factory=list)
    medical_history: list[str] = Field(default_factory=list)
    raw_fields: dict[str, str] = Field(
        default_factory=dict,
        description="Any additional key-value pairs extracted from the form.",
    )


# --- Classify ---

class IntakeClassifyRequest(BaseModel):
    """Request body for POST /intake/classify."""

    form_text: str = Field(
        ...,
        description="Raw text captured from the patient intake form.",
        min_length=1,
    )


class IntakeClassifyResponse(BaseModel):
    """Response body for POST /intake/classify."""

    category: IntakeCategory
    confidence: float = Field(ge=0.0, le=1.0)


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
    message: str = Field(..., min_length=0, max_length=4000)
    patient_id: str | None = None
    appointment_id: str | None = None


class ChatResponse(BaseModel):
    """Response body for POST /intake/chat."""

    session_id: str
    reply: str
    is_complete: bool = False
    collected: dict[str, str | None] = Field(default_factory=dict)
    fallback_required: bool = False
