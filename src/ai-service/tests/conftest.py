"""Shared pytest fixtures and env setup for all ai-service tests."""

import os

# Must be set BEFORE any app imports so pydantic-settings can build Settings.
os.environ.setdefault("INTERNAL_API_KEY", "test-key")
os.environ.setdefault("OLLAMA_BASE_URL", "http://localhost:11434")
os.environ.setdefault("OLLAMA_MODEL", "llama3")
os.environ.setdefault("REDIS_URL", "redis://localhost:6379/0")
