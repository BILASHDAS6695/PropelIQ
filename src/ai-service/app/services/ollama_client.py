"""Async HTTP wrapper around the Ollama /api/generate endpoint."""

from __future__ import annotations

import httpx
import structlog

log = structlog.get_logger(__name__)

_TIMEOUT = 30.0  # seconds


class OllamaClient:
    """Thin async client for Ollama local inference (ADR-004)."""

    def __init__(self, base_url: str, model: str) -> None:
        self._base_url = base_url.rstrip("/")
        self._model = model

    async def generate(self, prompt: str, *, system: str = "") -> str:
        """Call Ollama /api/generate and return the response text.

        Raises:
            RuntimeError: when Ollama is unavailable or returns a non-200.
        """
        payload: dict = {
            "model": self._model,
            "prompt": prompt,
            "stream": False,
        }
        if system:
            payload["system"] = system

        try:
            async with httpx.AsyncClient(timeout=_TIMEOUT) as client:
                resp = await client.post(
                    f"{self._base_url}/api/generate",
                    json=payload,
                )
        except (httpx.ConnectError, httpx.TimeoutException) as exc:
            log.warning("ollama_unavailable", error=str(exc))
            raise RuntimeError(f"Ollama unreachable: {exc}") from exc

        if resp.status_code != 200:
            log.warning("ollama_error", status=resp.status_code, body=resp.text[:200])
            raise RuntimeError(
                f"Ollama returned HTTP {resp.status_code}: {resp.text[:200]}"
            )

        data = resp.json()
        return data.get("response", "")
