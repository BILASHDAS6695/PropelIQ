"""Named Entity Recognition service — local spaCy / scispaCy processing (ADR-004)."""

from __future__ import annotations

import logging
from typing import TYPE_CHECKING

import spacy
from spacy.language import Language

if TYPE_CHECKING:
    from spacy.tokens import Doc

logger = logging.getLogger(__name__)

# ── Label normalisation maps ────────────────────────────────────────────────
_BC5CDR_MAP: dict[str, str] = {
    "CHEMICAL": "MEDICATION",
    "DISEASE":  "DIAGNOSIS",
}

_BIONLP_MAP: dict[str, str] = {
    "Anatomical_system":                 "ANATOMY",
    "Organ":                             "ANATOMY",
    "Organism_subdivision":              "ANATOMY",
    "Cancer":                            "DIAGNOSIS",
    "Simple_chemical":                   "MEDICATION",
    "Amino_acid":                        "MEDICATION",
    "Developing_anatomical_structure":   "ANATOMY",
}

# Labels produced by the EntityRuler layer (already normalised)
_RULER_LABELS: frozenset[str] = frozenset(
    {"PROCEDURE", "LAB_TEST", "LAB_VALUE", "SYMPTOM"}
)


class NerService:
    """Identifies clinical named entities using two scispaCy models plus an
    EntityRuler pattern layer.

    Models are loaded **once at construction** to avoid per-request overhead.
    All processing is fully local — no external API calls (ADR-004).

    Raises:
        RuntimeError: If either model cannot be loaded (triggers Hangfire retry).
    """

    def __init__(self) -> None:
        self._bc5cdr: Language = self._load("en_ner_bc5cdr_md")
        self._bionlp: Language = self._load("en_ner_bionlp13cg_md")
        self._add_ruler_patterns()

    # ── Public API ────────────────────────────────────────────────────────

    def extract_entities(
        self,
        pages: list[str],
        confidence_threshold: float = 0.7,
        chunk_size: int = 10_000,
    ) -> list[dict]:
        """Run NER over a list of page texts and return entity dicts.

        Args:
            pages: One entry per document page (from OCR output).
            confidence_threshold: Entities below this score are flagged.
            chunk_size: Maximum characters per processing chunk.

        Returns:
            List of entity dicts compatible with ``EntitySpan``.
        """
        results: list[dict] = []

        for page_index, page_text in enumerate(pages):
            if not page_text or not page_text.strip():
                continue

            char_offset = 0
            for chunk in self._chunk_text(page_text, chunk_size):
                entities = self._process_chunk(chunk, confidence_threshold)
                # Adjust start/end offsets for chunk position within page
                for ent in entities:
                    ent["start_offset"] += char_offset
                    ent["end_offset"]   += char_offset
                    ent["page_number"]  = page_index + 1   # 1-based, matches OcrPageResult.pageNumber
                results.extend(entities)
                char_offset += len(chunk)

        return results

    # ── Private helpers ───────────────────────────────────────────────────

    def _process_chunk(self, text: str, threshold: float) -> list[dict]:
        """Run all NER passes on a single text chunk."""
        seen: set[tuple[int, int]] = set()   # de-duplicate by span position
        entities: list[dict] = []

        for doc, label_map in (
            (self._bc5cdr(text), _BC5CDR_MAP),
            (self._bionlp(text), _BIONLP_MAP),
        ):
            for ent in doc.ents:
                norm_label = label_map.get(ent.label_)
                if norm_label is None:
                    continue
                span_key = (ent.start_char, ent.end_char)
                if span_key in seen:
                    continue
                seen.add(span_key)
                # scispaCy models do not expose per-entity confidence natively;
                # use 0.8 as a conservative estimate for model-detected entities.
                score = 0.8
                entities.append(
                    self._make_entity(ent.text, norm_label, ent.start_char, ent.end_char, score, threshold)
                )

        # EntityRuler pass (patterns for PROCEDURE, LAB_TEST, LAB_VALUE, SYMPTOM)
        ruler = self._bc5cdr.get_pipe("entity_ruler")
        ruler_doc: Doc = self._bc5cdr.make_doc(text)
        ruler(ruler_doc)   # populates ruler_doc.ents in-place
        for ent in ruler_doc.ents:
            if ent.label_ not in _RULER_LABELS:
                continue
            span_key = (ent.start_char, ent.end_char)
            if span_key in seen:
                continue
            seen.add(span_key)
            # EntityRuler patterns are curated, so use a slightly higher default.
            score = 0.85
            entities.append(
                self._make_entity(ent.text, ent.label_, ent.start_char, ent.end_char, score, threshold)
            )

        return entities

    @staticmethod
    def _make_entity(
        text: str, entity_type: str, start: int, end: int, score: float, threshold: float
    ) -> dict:
        return {
            "text":             text,
            "type":             entity_type,
            "start_offset":     start,
            "end_offset":       end,
            "confidence_score": round(score, 4),
            "low_confidence":   score < threshold,
        }

    @staticmethod
    def _chunk_text(text: str, chunk_size: int) -> list[str]:
        """Split text into chunks at whitespace boundaries."""
        if len(text) <= chunk_size:
            return [text]
        chunks: list[str] = []
        while text:
            if len(text) <= chunk_size:
                chunks.append(text)
                break
            split_at = text.rfind(" ", 0, chunk_size)
            if split_at == -1:
                split_at = chunk_size
            chunks.append(text[:split_at])
            text = text[split_at:].lstrip()
        return chunks

    @staticmethod
    def _load(model_name: str) -> Language:
        try:
            return spacy.load(model_name)
        except OSError as exc:
            raise RuntimeError(
                f"scispaCy model '{model_name}' could not be loaded. "
                "Ensure it is installed in the container (see Dockerfile). "
                f"Original error: {exc}"
            ) from exc

    def _add_ruler_patterns(self) -> None:
        """Add an EntityRuler pipe to the bc5cdr model for rule-based entity types."""
        ruler = self._bc5cdr.add_pipe("entity_ruler", last=True)

        procedures = [
            "biopsy", "appendectomy", "cholecystectomy", "colonoscopy",
            "endoscopy", "MRI", "CT scan", "X-ray", "ultrasound",
            "echocardiogram", "angioplasty", "dialysis", "chemotherapy",
            "radiation therapy", "intubation", "catheterisation",
        ]
        symptoms = [
            "fatigue", "fever", "nausea", "vomiting", "headache", "dyspnea",
            "chest pain", "shortness of breath", "dizziness", "cough",
            "haemoptysis", "haematuria", "oedema", "palpitations",
        ]
        lab_tests = [
            "CBC", "BMP", "CMP", "HbA1c", "TSH", "LFTs", "creatinine",
            "eGFR", "INR", "PT", "aPTT", "troponin", "BNP", "CRP",
            "ESR", "haemoglobin", "WBC", "platelets", "albumin",
        ]

        patterns: list[dict] = []
        for term in procedures:
            patterns.append({"label": "PROCEDURE", "pattern": term})
        for term in symptoms:
            patterns.append({"label": "SYMPTOM", "pattern": term})
        for term in lab_tests:
            patterns.append({"label": "LAB_TEST", "pattern": term})

        # LAB_VALUE — numeric value followed by a unit (e.g. "12.5 g/dL")
        patterns.append({
            "label": "LAB_VALUE",
            "pattern": [
                {"TEXT": {"REGEX": r"^\d+(\.\d+)?$"}},
                {"TEXT": {"REGEX": r"^(g/dL|mg/dL|mmol/L|mEq/L|IU/L|U/L|ng/mL|pg/mL|μmol/L)$"}},
            ],
        })

        ruler.add_patterns(patterns)

