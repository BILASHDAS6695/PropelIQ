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
