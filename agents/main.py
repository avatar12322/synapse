"""
Synapse Agents — FastAPI microservice
Provides AI-powered profiling, venue scouting, and mission generation.
Called by the .NET backend via HTTP.
"""
from __future__ import annotations
import logging

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware

from config import settings
from models import (
    OrchestrationRequest,
    OrchestrationResponse,
    ProfileUpdateRequest,
    EmbeddingResponse,
    MatchRequest,
    MissionProposal,
)
from agents.profiler import run_profiler
from agents.orchestrator import orchestrate
from agents.antisybil import AntisybilRequest, SybilScore, run_antisybil

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(
    title="Synapse Agents",
    description="LangGraph-powered AI agents for Synapse LifeQuest",
    version="1.0.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)


@app.get("/health")
async def health():
    return {"status": "healthy", "service": "synapse-agents"}


@app.post("/profile", response_model=EmbeddingResponse)
async def profile_user(req: ProfileUpdateRequest) -> EmbeddingResponse:
    """Build/update user embedding from interest tags + mood snapshot."""
    try:
        return await run_profiler(req)
    except Exception as exc:
        logger.error("Profile error: %s", exc)
        raise HTTPException(status_code=500, detail=str(exc))


@app.post("/orchestrate", response_model=OrchestrationResponse)
async def orchestrate_endpoint(req: OrchestrationRequest) -> OrchestrationResponse:
    """
    Main entry point for .NET backend.
    Supported types: "match", "scout", "profile"
    """
    return await orchestrate(req)


@app.post("/antisybil/score", response_model=SybilScore)
async def antisybil_score(req: AntisybilRequest) -> SybilScore:
    try:
        return await run_antisybil(req.user_id)
    except Exception as exc:
        logger.error("Anti-sybil error for user %d: %s", req.user_id, exc)
        raise HTTPException(status_code=500, detail=str(exc))
