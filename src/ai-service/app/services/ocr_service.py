"""OCR extraction service — local processing only (ADR-004)."""

from __future__ import annotations


class OcrService:
    """Extracts raw text from document images or PDF pages using Tesseract/PyMuPDF.

    No external API calls are made. All processing is local per ADR-004.
    """

    def extract_text_from_image(self, image_bytes: bytes) -> str:
        """Extract text from a raw image byte buffer.

        Args:
            image_bytes: Raw image data (PNG, JPEG, TIFF).

        Returns:
            Extracted text string.

        Raises:
            NotImplementedError: Until OCR logic is implemented.
        """
        raise NotImplementedError("OCR extraction not yet implemented")

    def extract_text_from_pdf(self, pdf_bytes: bytes) -> list[str]:
        """Extract per-page text from a PDF byte buffer using PyMuPDF.

        Args:
            pdf_bytes: Raw PDF data.

        Returns:
            List of text strings, one entry per page.

        Raises:
            NotImplementedError: Until PDF extraction logic is implemented.
        """
        raise NotImplementedError("PDF text extraction not yet implemented")
