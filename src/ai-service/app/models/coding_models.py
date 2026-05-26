"""Pydantic v2 models for the /coding domain."""

from __future__ import annotations

from enum import StrEnum

from pydantic import BaseModel, Field


class CodeSystem(StrEnum):
    ICD10CM = "ICD-10-CM"
    CPT = "CPT"


# --- Suggest ---

class CodeSuggestRequest(BaseModel):
    """Request body for POST /coding/suggest."""

    clinical_text: str = Field(
        ...,
        description="Free-text clinical narrative to generate code suggestions for.",
        min_length=1,
    )
    code_systems: list[CodeSystem] = Field(
        default=[CodeSystem.ICD10CM],
        description="Which code systems to query.",
    )
    max_suggestions: int = Field(
        default=10,
        ge=1,
        le=50,
        description="Maximum number of suggestions to return per code system.",
    )


class CodeSuggestion(BaseModel):
    code: str = Field(description="Suggested code (e.g., Z00.00).")
    description: str = Field(description="Human-readable description of the code.")
    code_system: CodeSystem
    confidence: float = Field(ge=0.0, le=1.0, description="Model confidence score.")


class CodeSuggestResponse(BaseModel):
    """Response body for POST /coding/suggest."""

    suggestions: list[CodeSuggestion]


# --- Validate ---

class CodeValidateRequest(BaseModel):
    """Request body for POST /coding/validate."""

    codes: list[str] = Field(
        ...,
        description="List of codes to validate.",
        min_length=1,
    )
    code_system: CodeSystem = Field(
        default=CodeSystem.ICD10CM,
        description="Code system context for validation.",
    )


class CodeValidateResponse(BaseModel):
    """Response body for POST /coding/validate."""

    results: dict[str, bool] = Field(
        description="Map of code \u2192 validity (True = valid, False = invalid)."
    )
