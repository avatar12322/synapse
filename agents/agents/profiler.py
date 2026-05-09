"""
Profiler agent — builds and updates user embeddings from interest tags + mood snapshots.
Uses sentence-transformers locally so raw profile text never leaves the device (CLAUDE.md §4).
"""
from __future__ import annotations
import json
import logging
from typing import Any

import asyncpg
from langgraph.graph import StateGraph, END
from sentence_transformers import SentenceTransformer
from typing_extensions import TypedDict

from config import settings
from models import ProfileUpdateRequest, EmbeddingResponse

logger = logging.getLogger(__name__)

# Singleton — loaded once per process; ~400 MB on first run
_encoder: SentenceTransformer | None = None


def _get_encoder() -> SentenceTransformer:
    global _encoder
    if _encoder is None:
        _encoder = SentenceTransformer(settings.embedding_model)
    return _encoder


# ── LangGraph state ────────────────────────────────────────────────────────────

class ProfilerState(TypedDict):
    request: ProfileUpdateRequest
    profile_text: str
    embedding: list[float]
    persisted: bool
    error: str | None


# ── Nodes ──────────────────────────────────────────────────────────────────────

def build_profile_text(state: ProfilerState) -> ProfilerState:
    req = state["request"]
    parts = req.interest_tags[:]
    if req.mood_snapshot:
        mood = req.mood_snapshot.get("mood", "")
        energy = req.mood_snapshot.get("energy", "")
        if mood:
            parts.append(f"mood:{mood}")
        if energy:
            parts.append(f"energy_level:{energy}")
    state["profile_text"] = " ".join(parts)
    return state


def compute_embedding(state: ProfilerState) -> ProfilerState:
    try:
        encoder = _get_encoder()
        vec = encoder.encode(state["profile_text"]).tolist()
        state["embedding"] = vec
    except Exception as exc:
        state["error"] = str(exc)
    return state


async def persist_embedding(state: ProfilerState) -> ProfilerState:
    if state.get("error") or not state.get("embedding"):
        return state
    try:
        conn = await asyncpg.connect(settings.database_url)
        try:
            embedding_str = "[" + ",".join(map(str, state["embedding"])) + "]"
            await conn.execute(
                """
                INSERT INTO "UserProfiles" ("UserId", "Embedding", "InterestTags", "MoodSnapshot", "LastProfiledAt", "UpdatedAt")
                VALUES ($1, $2::vector, $3, $4, NOW(), NOW())
                ON CONFLICT ("UserId") DO UPDATE
                  SET "Embedding" = EXCLUDED."Embedding",
                      "InterestTags" = EXCLUDED."InterestTags",
                      "MoodSnapshot" = EXCLUDED."MoodSnapshot",
                      "UpdatedAt" = NOW()
                """,
                state["request"].user_id,
                embedding_str,
                json.dumps(state["request"].interest_tags),
                json.dumps(state["request"].mood_snapshot) if state["request"].mood_snapshot else None,
            )
            state["persisted"] = True
        finally:
            await conn.close()
    except Exception as exc:
        logger.error("Persist embedding error: %s", exc)
        state["error"] = str(exc)
    return state


# ── Graph ──────────────────────────────────────────────────────────────────────

def build_profiler_graph() -> Any:
    g = StateGraph(ProfilerState)
    g.add_node("build_text", build_profile_text)
    g.add_node("embed", compute_embedding)
    g.add_node("persist", persist_embedding)
    g.set_entry_point("build_text")
    g.add_edge("build_text", "embed")
    g.add_edge("embed", "persist")
    g.add_edge("persist", END)
    return g.compile()


profiler_graph = build_profiler_graph()


async def run_profiler(req: ProfileUpdateRequest) -> EmbeddingResponse:
    initial: ProfilerState = {
        "request": req,
        "profile_text": "",
        "embedding": [],
        "persisted": False,
        "error": None,
    }
    result = await profiler_graph.ainvoke(initial)
    if result.get("error"):
        raise RuntimeError(result["error"])
    return EmbeddingResponse(user_id=req.user_id, embedding=result["embedding"])
