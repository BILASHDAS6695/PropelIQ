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

    text: str = Field(..., description="Plain text to run NER over.", min_length=1)


class EntitySpan(BaseModel):
    label: str = Field(description="Entity label (e.g., CONDITION, MEDICATION).")
    text: str = Field(description="Surface text of the entity.")
    start: int = Field(description="Character start offset.")
    end: int = Field(description="Character end offset.")


class NerResponse(BaseModel):
    """Response body for POST /extraction/ner."""

    entities: list[EntitySpan] = Field(description="Detected entity spans.")
