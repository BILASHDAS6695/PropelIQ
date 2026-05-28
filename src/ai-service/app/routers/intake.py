import redis.asyncio as aioredis
from fastapi import APIRouter, HTTPException

from app.config import settings
from app.models import (
    ChatRequest,
    ChatResponse,
    IntakeClassifyRequest,
    IntakeClassifyResponse,
    IntakeParseRequest,
    IntakeParseResponse,
)
from app.services.intake_orchestration_service import IntakeOrchestrationService
from app.services.ollama_client import OllamaClient
from app.services.session_manager import SessionManager

router = APIRouter()

# Module-level singletons (swapped out in tests via monkeypatching)
_redis_client = aioredis.from_url(settings.redis_url, decode_responses=True)
_session_manager = SessionManager(_redis_client)
_ollama_client = OllamaClient(settings.ollama_base_url, settings.ollama_model)
_orchestration = IntakeOrchestrationService(_session_manager, _ollama_client)


@router.post("/parse", response_model=IntakeParseResponse)
async def parse_intake(request: IntakeParseRequest) -> IntakeParseResponse:
    """NLP parsing of intake form text — placeholder."""
    raise NotImplementedError


@router.post("/classify", response_model=IntakeClassifyResponse)
async def classify_intake(request: IntakeClassifyRequest) -> IntakeClassifyResponse:
    """Classification of intake data — placeholder."""
    raise NotImplementedError


@router.post("/chat", response_model=ChatResponse)
async def chat(request: ChatRequest) -> ChatResponse:
    """Drive a conversational intake turn.

    - **No session_id**: starts a new session, returns opening greeting.
    - **With session_id**: processes the patient's message and returns
      the assistant reply.  Returns HTTP 410 if the session has expired.
    """
    # --- New session ---
    if request.session_id is None:
        return await _orchestration.start_session(
            patient_id=request.patient_id,
            appointment_id=request.appointment_id,
        )

    # --- Existing session ---
    session = await _session_manager.get(request.session_id)
    if session is None:
        raise HTTPException(status_code=410, detail="Session expired or not found.")

    if _session_manager.is_timed_out(session):
        await _session_manager.delete(session.session_id)
        raise HTTPException(status_code=410, detail="Session timed out.")

    return await _orchestration.handle_message(session, request.message)

