from fastapi import APIRouter

from app.models import IntakeParseRequest, IntakeParseResponse, IntakeClassifyRequest, IntakeClassifyResponse

router = APIRouter()


@router.post("/parse", response_model=IntakeParseResponse)
async def parse_intake(request: IntakeParseRequest) -> IntakeParseResponse:
    """NLP parsing of intake form text — placeholder."""
    raise NotImplementedError


@router.post("/classify", response_model=IntakeClassifyResponse)
async def classify_intake(request: IntakeClassifyRequest) -> IntakeClassifyResponse:
    """Classification of intake data — placeholder."""
    raise NotImplementedError
