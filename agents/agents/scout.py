"""
Scout agent — scores nearby venues for a given user profile using Gemini reasoning.
Returns venues ranked by contextual fit (time of day, mood, category preference).
"""
from __future__ import annotations
import logging
from typing import Any

from langchain_google_genai import ChatGoogleGenerativeAI
from langchain_core.messages import HumanMessage, SystemMessage
from langgraph.graph import StateGraph, END
from typing_extensions import TypedDict

from config import settings
from models import UserProfileInput, VenueInput

logger = logging.getLogger(__name__)

SCOUT_SYSTEM = """You are the Scout agent for Synapse — a LifeQuest app that sends pairs of people
on city missions at local cafés and venues.

Given a user profile (interests, mood) and a list of nearby venues, rank the venues by contextual fit.
Consider: user interests vs. venue category, current mood/energy level, time of day, and variety.

Return a JSON array of venue IDs in descending order of fit, with a one-sentence reason for the top pick.
Format: {"ranked_ids": [1, 5, 3, ...], "top_reason": "..."}
Only return valid JSON, no markdown fences."""


class ScoutState(TypedDict):
    user_profile: UserProfileInput
    venues: list[VenueInput]
    ranked_ids: list[int]
    top_reason: str
    error: str | None


def build_scout_prompt(state: ScoutState) -> ScoutState:
    return state


async def call_gemini(state: ScoutState) -> ScoutState:
    llm = ChatGoogleGenerativeAI(
        model=settings.reasoning_model,
        google_api_key=settings.google_api_key,
        max_output_tokens=512,
    )
    profile = state["user_profile"]
    venues_text = "\n".join(
        f"- id={v.id} name={v.name} category={v.category} dist={v.distance_metres:.0f}m"
        for v in state["venues"]
    )
    user_text = (
        f"Interests: {', '.join(profile.interest_tags)}\n"
        f"Mood: {profile.mood_snapshot or 'not set'}\n"
        f"Search radius: {profile.search_radius_metres}m"
    )
    messages = [
        SystemMessage(content=SCOUT_SYSTEM),
        HumanMessage(content=f"User profile:\n{user_text}\n\nNearby venues:\n{venues_text}"),
    ]
    try:
        import json
        response = await llm.ainvoke(messages)
        data = json.loads(response.content)
        state["ranked_ids"] = data.get("ranked_ids", [v.id for v in state["venues"]])
        state["top_reason"] = data.get("top_reason", "")
    except Exception as exc:
        logger.warning("Scout Claude call failed: %s — falling back to distance order", exc)
        state["ranked_ids"] = [v.id for v in state["venues"]]
        state["top_reason"] = "Sorted by distance (fallback)."
        state["error"] = str(exc)
    return state


def build_scout_graph() -> Any:
    g = StateGraph(ScoutState)
    g.add_node("scout", call_gemini)
    g.set_entry_point("scout")
    g.add_edge("scout", END)
    return g.compile()


scout_graph = build_scout_graph()


async def run_scout(user_profile: UserProfileInput, venues: list[VenueInput]) -> list[int]:
    """Returns venue IDs ranked by contextual fit for this user."""
    initial: ScoutState = {
        "user_profile": user_profile,
        "venues": venues,
        "ranked_ids": [],
        "top_reason": "",
        "error": None,
    }
    result = await scout_graph.ainvoke(initial)
    return result["ranked_ids"]
