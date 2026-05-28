"""Pydantic v2 models for the /extraction domain."""

from __future__ import annotations

from enum import StrEnum

from pydantic import BaseModel, Field


class DocumentFormat(StrEnum):
    PDF = "pdf"
    PNG = "png"
    JPEG = "jpeg"
    TIFF = "tiff"


# --- OCR ---

class OcrRequest(BaseModel):
    """Request body for POST /extraction/ocr."""

    document_base64: str = Field(
        ...,
        description="Base64-encoded document bytes (image or PDF).",
        min_length=1,
    )
    format: DocumentFormat = Field(
        default=DocumentFormat.PDF,
        description="File format of the encoded document.",
    )
    page_numbers: list[int] | None = Field(
        default=None,
        description="Optional list of 1-based page numbers to extract (PDF only). "
                    "None means all pages.",
    )


class OcrResponse(BaseModel):
    """Response body for POST /extraction/ocr."""

    pages: list[str] = Field(description="Extracted text per page.")
    total_pages: int = Field(description="Total number of pages processed.")


# --- NER ---

class NerRequest(BaseModel):
    """Request body for POST /extraction/ner."""

    pages: list[str] = Field(
        ...,
        description="List of plain-text page strings from OCR output.",
        min_length=1,
    )
    confidence_threshold: float = Field(
        default=0.7,
        ge=0.0,
        le=1.0,
        description="Entities with score below this value are flagged low_confidence.",
    )


class EntitySpan(BaseModel):
    """A single recognised clinical entity."""

    text: str = Field(description="Surface text as it appears in the source.")
    type: str = Field(
        description=(
            "Normalised entity type: DIAGNOSIS | MEDICATION | PROCEDURE | "
            "LAB_TEST | LAB_VALUE | ANATOMY | SYMPTOM."
        )
    )
    start_offset: int = Field(description="Zero-based char start in the page text.")
    end_offset: int = Field(description="Zero-based char end (exclusive) in the page text.")
    confidence_score: float = Field(description="Model confidence 0.0–1.0.")
    low_confidence: bool = Field(
        description="True when confidence_score < the request threshold."
    )
    page_number: int = Field(description="1-based page index within the document.")


class NerResponse(BaseModel):
    """Response body for POST /extraction/ner."""

    entities: list[EntitySpan] = Field(description="All detected entity spans.")
