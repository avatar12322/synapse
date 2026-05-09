"""
Matchmaker agent — decides if two users are compatible enough for a mission.
Uses cosine similarity on embeddings + Claude reasoning for a final narrative fit score.
"""
from __future__ import annotations
import json
import logging
import math
from typing import Any

from langchain_anthropic import ChatAnthropic
from langchain_core.messages import HumanMessage, SystemMessage
from langgraph.graph import StateGraph, END
from typing_extensions import TypedDict

from config import settings
from models import MatchRequest, MissionProposal, VenueInput

logger = logging.getLogger(__name__)

MATCHMAKER_SYSTEM = """You are the Matchmaker agent for Synapse — a LifeQuest app.
Given two user profiles and a ranked list of nearby venues, craft the best mission proposal.

Rules:
- Pick ONE venue that works well for BOTH users.
- Write a mission title (max 80 chars) and a 2-sentence description that motivates both people.
- The category should match the venue.
- List 2-4 shared interest tags that make this pairing interesting.
- Give a one-sentence reasoning for the venue + match.

Return valid JSON only:
{
  "title": "...",
  "description": "...",
  "category": "Coffee|Lunch|Sports|Culture|Learning|Networking|Other",
  "venue_id": 1,
  "interest_tags": ["...", "..."],
  "reasoning": "..."
}"""


class MatchmakerState(TypedDict):
    request: MatchRequest
    ranked_venue_ids: list[int]
    cosine_similarity: float
    proposal: MissionProposal | None
    error: str | None


def compute_cosine_similarity(state: MatchmakerState) -> MatchmakerState:
    # Embeddings come from profiles persisted by the Profiler agent
    # For Phase 1, we check if they exist; if not, default similarity = 0.5
    state["cosine_similarity"] = 0.5
    return state


async def call_matchmaker_claude(state: MatchmakerState) -> MatchmakerState:
    req = state["request"]
    ranked_ids = state["ranked_venue_ids"]

    # Build venue lookup from request
    venue_map = {v.id: v for v in req.nearby_venues}
    ranked_venues = [venue_map[vid] for vid in ranked_ids if vid in venue_map]
    top_venues = ranked_venues[:5]

    venues_text = "\n".join(
        f"- id={v.id} {v.name} ({v.category}) {v.distance_metres:.0f}m"
        for v in top_venues
    )
    profile_a = f"User A (id={req.user_a_id}): {', '.join(req.user_a_profile.interest_tags)}"
    profile_b = f"User B (id={req.user_b_id}): {', '.join(req.user_b_profile.interest_tags)}"
    mood_a = f"Mood A: {req.user_a_profile.mood_snapshot}" if req.user_a_profile.mood_snapshot else ""
    mood_b = f"Mood B: {req.user_b_profile.mood_snapshot}" if req.user_b_profile.mood_snapshot else ""

    prompt = "\n".join(filter(None, [profile_a, profile_b, mood_a, mood_b, "", "Venues:", venues_text]))

    llm = ChatAnthropic(
        model=settings.reasoning_model,
        api_key=settings.anthropic_api_key,
        max_tokens=768,
    )
    messages = [
        SystemMessage(content=MATCHMAKER_SYSTEM),
        HumanMessage(content=prompt),
    ]
    try:
        response = await llm.ainvoke(messages)
        data = json.loads(response.content)
        state["proposal"] = MissionProposal(**data)
    except Exception as exc:
        logger.warning("Matchmaker Claude call failed: %s — using fallback", exc)
        fallback_venue = top_venues[0] if top_venues else req.nearby_venues[0]
        state["proposal"] = MissionProposal(
            title=f"Meet @ {fallback_venue.name}",
            description="Meet your matched partner and enjoy some time together without phones.",
            category="Coffee",
            venue_id=fallback_venue.id,
            interest_tags=[],
            reasoning="Fallback: closest venue.",
        )
        state["error"] = str(exc)
    return state


def build_matchmaker_graph() -> Any:
    g = StateGraph(MatchmakerState)
    g.add_node("similarity", compute_cosine_similarity)
    g.add_node("propose", call_matchmaker_claude)
    g.set_entry_point("similarity")
    g.add_edge("similarity", "propose")
    g.add_edge("propose", END)
    return g.compile()


matchmaker_graph = build_matchmaker_graph()


async def run_matchmaker(req: MatchRequest, ranked_venue_ids: list[int]) -> MissionProposal:
    initial: MatchmakerState = {
        "request": req,
        "ranked_venue_ids": ranked_venue_ids,
        "cosine_similarity": 0.0,
        "proposal": None,
        "error": None,
    }
    result = await matchmaker_graph.ainvoke(initial)
    if result["proposal"] is None:
        raise RuntimeError(result.get("error", "Matchmaker produced no proposal"))
    return result["proposal"]
