from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    internal_api_key: str
    port: int = 8000
    log_level: str = "INFO"

    # Ollama (local LLM — ADR-004)
    ollama_base_url: str = "http://localhost:11434"
    ollama_model: str = "llama3"

    # Redis (session store)
    redis_url: str = "redis://localhost:6379/0"

    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")


settings = Settings()  # type: ignore[call-arg]
