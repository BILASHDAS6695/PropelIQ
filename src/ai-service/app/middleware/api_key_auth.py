"""Internal API key authentication middleware.

Rejects requests that do not present the correct X-Internal-Api-Key header.
Returns 401 Unauthorized without leaking the expected key value.
"""

from __future__ import annotations

from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import JSONResponse

from app.config import settings

_HEADER_NAME = "X-Internal-Api-Key"
_EXEMPT_PATHS = {"/health"}


class ApiKeyMiddleware(BaseHTTPMiddleware):
    """Validates the X-Internal-Api-Key header on every non-exempt request."""

    async def dispatch(self, request: Request, call_next):  # type: ignore[override]
        if request.url.path in _EXEMPT_PATHS:
            return await call_next(request)

        api_key = request.headers.get(_HEADER_NAME)
        if not api_key or api_key != settings.internal_api_key:
            return JSONResponse(
                status_code=401,
                content={"detail": "Unauthorized"},
            )

        return await call_next(request)
