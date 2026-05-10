from __future__ import annotations
from pydantic import BaseModel
from typing import Optional


class UserProfileInput(BaseModel):
    user_id: int
    interest_tags: list[str]
    mood_snapshot: Optional[dict] = None
    search_radius_metres: int = 2000
    embedding: Optional[list[float]] = None  # Phase 3: 768-dim vector from pgvector


class VenueInput(BaseModel):
    id: int
    name: str
    category: str
    address: str
    latitude: float
    longitude: float
    distance_metres: Optional[float] = None


class MatchRequest(BaseModel):
    user_a_id: int
    user_a_profile: UserProfileInput
    user_b_id: int
    user_b_profile: UserProfileInput
    nearby_venues: list[VenueInput]


class MissionProposal(BaseModel):
    title: str
    description: str
    category: str
    venue_id: int
    interest_tags: list[str]
    reasoning: str


class ProfileUpdateRequest(BaseModel):
    user_id: int
    interest_tags: list[str]
    mood_snapshot: Optional[dict] = None


class EmbeddingResponse(BaseModel):
    user_id: int
    embedding: list[float]


class OrchestrationRequest(BaseModel):
    type: str  # "match" | "profile" | "scout"
    payload: dict


class OrchestrationResponse(BaseModel):
    success: bool
    result: dict
    error: Optional[str] = None
