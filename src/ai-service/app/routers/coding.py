from fastapi import APIRouter

from app.models import CodeSuggestRequest, CodeSuggestResponse, CodeValidateRequest, CodeValidateResponse

router = APIRouter()


@router.post("/suggest", response_model=CodeSuggestResponse)
async def suggest_codes(request: CodeSuggestRequest) -> CodeSuggestResponse:
    """ICD/CPT code suggestions from clinical text — placeholder."""
    raise NotImplementedError


@router.post("/validate", response_model=CodeValidateResponse)
async def validate_codes(request: CodeValidateRequest) -> CodeValidateResponse:
    """Code validation against known code sets — placeholder."""
    raise NotImplementedError
