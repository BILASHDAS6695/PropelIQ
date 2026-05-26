"""Named Entity Recognition service — local spaCy processing (ADR-004)."""

from __future__ import annotations


class NerService:
    """Identifies clinical named entities (conditions, medications, procedures)
    from plain text using a local spaCy model.

    No external API calls are made. All processing is local per ADR-004.
    """

    def extract_entities(self, text: str) -> list[dict[str, str]]:
        """Run NER over the provided text and return entity spans.

        Args:
            text: Plain text input from OCR or direct submission.

        Returns:
            List of entity dicts with keys: ``label``, ``text``, ``start``, ``end``.

        Raises:
            NotImplementedError: Until spaCy model is loaded and wired.
        """
        raise NotImplementedError("NER entity extraction not yet implemented")
