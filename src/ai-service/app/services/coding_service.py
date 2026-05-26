"""Medical coding service — ICD/CPT suggestion from clinical text (ADR-004)."""

from __future__ import annotations


class CodingService:
    """Suggests ICD-10-CM and CPT codes from clinical text or NER output.

    No external API calls are made. All processing is local per ADR-004.
    """

    def suggest_codes(self, clinical_text: str) -> list[dict[str, str]]:
        """Suggest ICD/CPT codes for the given clinical text.

        Args:
            clinical_text: Free-text clinical narrative.

        Returns:
            List of code suggestion dicts with keys: ``code``, ``description``, ``confidence``.

        Raises:
            NotImplementedError: Until coding model is implemented.
        """
        raise NotImplementedError("Code suggestion not yet implemented")

    def validate_codes(self, codes: list[str]) -> dict[str, bool]:
        """Validate a list of codes against the known code sets.

        Args:
            codes: List of ICD/CPT code strings.

        Returns:
            Dict mapping each code to its validity (True/False).

        Raises:
            NotImplementedError: Until code validation is implemented.
        """
        raise NotImplementedError("Code validation not yet implemented")
