"""
Orchestrator agent — routes incoming requests to the right sub-agent.
Called by the .NET backend via HTTP POST /orchestrate.
"""
from __future__ import annotations
import logging

from models import (
    MatchRequest,
    MissionProposal,
    OrchestrationRequest,
    OrchestrationResponse,
    ProfileUpdateRequest,
)
from agents.profiler import run_profiler
from agents.scout import run_scout
from agents.matchmaker import run_matchmaker

logger = logging.getLogger(__name__)


async def orchestrate(req: OrchestrationRequest) -> OrchestrationResponse:
    try:
        match req.type:
            case "profile":
                profile_req = ProfileUpdateRequest(**req.payload)
                embedding_resp = await run_profiler(profile_req)
                return OrchestrationResponse(
                    success=True,
                    result={"user_id": embedding_resp.user_id, "embedding_dims": len(embedding_resp.embedding)},
                )

            case "scout":
                from models import UserProfileInput, VenueInput
                user_profile = UserProfileInput(**req.payload["user_profile"])
                venues = [VenueInput(**v) for v in req.payload["venues"]]
                ranked = await run_scout(user_profile, venues)
                return OrchestrationResponse(success=True, result={"ranked_venue_ids": ranked})

            case "match":
                match_req = MatchRequest(**req.payload)

                # Scout on behalf of both users combined
                from models import UserProfileInput
                combined_tags = list(set(
                    match_req.user_a_profile.interest_tags + match_req.user_b_profile.interest_tags
                ))
                combined_profile = UserProfileInput(
                    user_id=0,
                    interest_tags=combined_tags,
                )
                ranked = await run_scout(combined_profile, match_req.nearby_venues)
                proposal: MissionProposal = await run_matchmaker(match_req, ranked)
                return OrchestrationResponse(success=True, result=proposal.model_dump())

            case _:
                return OrchestrationResponse(
                    success=False, result={}, error=f"Unknown orchestration type: {req.type}"
                )
    except Exception as exc:
        logger.error("Orchestration error [%s]: %s", req.type, exc, exc_info=True)
        return OrchestrationResponse(success=False, result={}, error=str(exc))
