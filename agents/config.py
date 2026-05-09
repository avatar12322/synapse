from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8")

    google_api_key: str = ""
    database_url: str = "postgresql://postgres:postgres@localhost:5432/synapse"
    redis_url: str = "redis://localhost:6379"
    embedding_model: str = "sentence-transformers/paraphrase-multilingual-mpnet-base-v2"
    synapse_api_url: str = "http://localhost:5000"

    reasoning_model: str = "gemini-2.5-flash"
    fast_model: str = "gemini-2.5-flash"


settings = Settings()
