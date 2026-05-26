"""Intake NLP service — form text parsing and classification (ADR-004)."""

from __future__ import annotations


class IntakeService:
    """Parses and classifies patient intake form text using local NLP models.

    No external API calls are made. All processing is local per ADR-004.
    """

    def parse_intake_form(self, form_text: str) -> dict[str, str | list[str]]:
        """Parse structured fields from free-text intake form content.

        Args:
            form_text: Raw text from a patient intake form.

        Returns:
            Dict of parsed fields (e.g., chief_complaint, medications, allergies).

        Raises:
            NotImplementedError: Until form parsing logic is implemented.
        """
        raise NotImplementedError("Intake form parsing not yet implemented")

    def classify_intake(self, form_text: str) -> str:
        """Classify intake submission into a processing category.

        Args:
            form_text: Raw text from a patient intake form.

        Returns:
            Classification label string (e.g., "routine", "urgent", "emergency").

        Raises:
            NotImplementedError: Until classification model is implemented.
        """
        raise NotImplementedError("Intake classification not yet implemented")
