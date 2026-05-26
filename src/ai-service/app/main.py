"""HealthPlatform AI Service — FastAPI entry point."""

from __future__ import annotations

from fastapi import FastAPI
from fastapi.responses import JSONResponse

from app.config import settings
from app.logging_config import configure_logging
from app.middleware.api_key_auth import ApiKeyMiddleware
from app.routers import extraction, coding, intake

# Configure structured logging as early as possible
configure_logging(settings.log_level)

import structlog  # noqa: E402 — must import after configure_logging()
logger = structlog.get_logger(__name__)

app = FastAPI(
    title="HealthPlatform AI Service",
    version="1.0.0",
    docs_url=None,    # Disable Swagger UI — internal service
    redoc_url=None,
)

# Middleware (registered in reverse order — auth runs first)
app.add_middleware(ApiKeyMiddleware)

# Routers
app.include_router(extraction.router, prefix="/extraction", tags=["Extraction"])
app.include_router(coding.router,     prefix="/coding",     tags=["Coding"])
app.include_router(intake.router,     prefix="/intake",     tags=["Intake"])


@app.get("/health", status_code=200)
async def health_check() -> JSONResponse:
    logger.info("health_check_called")
    return JSONResponse(
        status_code=200,
        content={
            "status": "healthy",
            "service": "ai-service",
            "version": "1.0.0",
        },
    )


@app.on_event("startup")
async def on_startup() -> None:
    logger.info("ai_service_starting", port=settings.port, log_level=settings.log_level)


@app.on_event("shutdown")
async def on_shutdown() -> None:
    logger.info("ai_service_stopping")
