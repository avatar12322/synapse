"""
Anti-Sybil agent — scores a user's likelihood of being a fake/bot account.

Uses SQL-derived features (device collisions, IP collisions, mission patterns).
When a trained model exists at agents/models/antisybil_rf.pkl, it uses RandomForest.
Otherwise falls back to heuristic rules.

Optional: if NEO4J_URI is set in env, enriches features with graph-based signals
(shared-device cliques, connected component size).
"""
from __future__ import annotations
import logging
import os
import pickle
from pathlib import Path
from typing import Any

import asyncpg
from langgraph.graph import StateGraph, END
from pydantic import BaseModel
from typing_extensions import TypedDict

from config import settings

logger = logging.getLogger(__name__)

MODEL_PATH = Path(__file__).parent.parent / "models" / "antisybil_rf.pkl"


class AntisybilRequest(BaseModel):
    user_id: int


class SybilScore(BaseModel):
    user_id: int
    score: float          # 0.0 = clean, 1.0 = definitely sybil
    is_suspicious: bool
    reason: str


class AntisybilState(TypedDict):
    user_id: int
    features: dict[str, float]
    score: float
    is_suspicious: bool
    reason: str


async def build_features(state: AntisybilState) -> AntisybilState:
    uid = state["user_id"]
    features: dict[str, float] = {}

    try:
        conn = await asyncpg.connect(settings.database_url)
        try:
            # How many users share the same DeviceFingerprint
            fp_row = await conn.fetchrow(
                """SELECT COUNT(*) as cnt FROM "Users" u2
                   JOIN "Users" u1 ON u1."DeviceFingerprint" = u2."DeviceFingerprint"
                   WHERE u1."Id" = $1 AND u1."DeviceFingerprint" IS NOT NULL AND u2."Id" != $1""",
                uid
            )
            features["device_collision"] = float(fp_row["cnt"] or 0)

            # How many users share the same IP in the last 7 days
            ip_row = await conn.fetchrow(
                """SELECT COUNT(*) as cnt FROM "Users" u2
                   JOIN "Users" u1 ON u1."LastKnownIp" = u2."LastKnownIp"
                   WHERE u1."Id" = $1 AND u1."LastKnownIp" IS NOT NULL
                     AND u2."Id" != $1 AND u2."LastLoginAt" > NOW() - INTERVAL '7 days'""",
                uid
            )
            features["ip_collision"] = float(ip_row["cnt"] or 0)

            # How many users share the same BSSID
            bssid_row = await conn.fetchrow(
                """SELECT COUNT(*) as cnt FROM "Users" u2
                   JOIN "Users" u1 ON u1."LastKnownBssid" = u2."LastKnownBssid"
                   WHERE u1."Id" = $1 AND u1."LastKnownBssid" IS NOT NULL AND u2."Id" != $1""",
                uid
            )
            features["bssid_collision"] = float(bssid_row["cnt"] or 0)

            # Mission count in last 30 days
            mission_row = await conn.fetchrow(
                """SELECT COUNT(*) as cnt FROM "Missions"
                   WHERE ("UserAId" = $1 OR "UserBId" = $1)
                     AND "CreatedAt" > NOW() - INTERVAL '30 days'""",
                uid
            )
            features["mission_count_30d"] = float(mission_row["cnt"] or 0)

            # Unique partners in last 30 days
            partner_row = await conn.fetchrow(
                """SELECT COUNT(DISTINCT CASE WHEN "UserAId" = $1 THEN "UserBId" ELSE "UserAId" END) as cnt
                   FROM "Missions"
                   WHERE ("UserAId" = $1 OR "UserBId" = $1)
                     AND "Status" = 3  -- Completed
                     AND "CreatedAt" > NOW() - INTERVAL '30 days'""",
                uid
            )
            features["unique_partners_30d"] = float(partner_row["cnt"] or 0)

        finally:
            await conn.close()
    except Exception as exc:
        logger.warning("Feature build failed for user %d: %s", uid, exc)

    # Optional Neo4j graph features
    neo4j_uri = os.environ.get("NEO4J_URI")
    if neo4j_uri and features:
        try:
            from neo4j import AsyncGraphDatabase
            async with AsyncGraphDatabase.driver(
                neo4j_uri,
                auth=(os.environ.get("NEO4J_USER", "neo4j"), os.environ.get("NEO4J_PASSWORD", "synapse"))
            ) as driver:
                async with driver.session() as session:
                    # Upsert user node + device/location relationships
                    await session.run(
                        "MERGE (u:User {userId: $uid})",
                        uid=uid
                    )
                    # Count connected component size (users reachable via shared devices)
                    result = await session.run(
                        """MATCH path = (u:User {userId: $uid})-[:SHARES_DEVICE*1..3]-(other:User)
                           RETURN COUNT(DISTINCT other) as component_size""",
                        uid=uid
                    )
                    rec = await result.single()
                    features["neo4j_component_size"] = float((rec["component_size"] if rec else 0) or 0)
        except Exception as exc:
            logger.warning("Neo4j features failed for user %d: %s", uid, exc)

    state["features"] = features
    return state


def classify(state: AntisybilState) -> AntisybilState:
    features = state["features"]
    if not features:
        state["score"] = 0.0
        state["is_suspicious"] = False
        state["reason"] = "no_data"
        return state

    # Try trained RandomForest model
    if MODEL_PATH.exists():
        try:
            with open(MODEL_PATH, "rb") as f:
                model = pickle.load(f)
            feature_vector = [[
                features.get("device_collision", 0),
                features.get("ip_collision", 0),
                features.get("bssid_collision", 0),
                features.get("mission_count_30d", 0),
                features.get("unique_partners_30d", 0),
                features.get("neo4j_component_size", 0),
            ]]
            proba = model.predict_proba(feature_vector)[0][1]  # P(sybil)
            state["score"] = float(proba)
            state["is_suspicious"] = proba > 0.7
            state["reason"] = f"rf_model:score={proba:.2f}"
            return state
        except Exception as exc:
            logger.warning("RF model inference failed: %s — falling back to heuristics", exc)

    # Heuristic fallback
    reasons = []
    score = 0.0
    if features.get("device_collision", 0) > 2:
        reasons.append(f"device_shared_by_{int(features['device_collision'])+1}_users")
        score += 0.4
    if features.get("ip_collision", 0) > 5:
        reasons.append(f"ip_shared_by_{int(features['ip_collision'])+1}_users")
        score += 0.3
    if features.get("bssid_collision", 0) > 3:
        reasons.append(f"bssid_shared_by_{int(features['bssid_collision'])+1}_users")
        score += 0.2
    if features.get("neo4j_component_size", 0) > 10:
        reasons.append(f"graph_component_size_{int(features['neo4j_component_size'])}")
        score += 0.3

    score = min(score, 1.0)
    state["score"] = score
    state["is_suspicious"] = score >= 0.5
    state["reason"] = ",".join(reasons) if reasons else "clean"
    return state


def build_antisybil_graph() -> Any:
    g = StateGraph(AntisybilState)
    g.add_node("build_features", build_features)
    g.add_node("classify", classify)
    g.set_entry_point("build_features")
    g.add_edge("build_features", "classify")
    g.add_edge("classify", END)
    return g.compile()


antisybil_graph = build_antisybil_graph()


async def run_antisybil(user_id: int) -> SybilScore:
    initial: AntisybilState = {
        "user_id": user_id,
        "features": {},
        "score": 0.0,
        "is_suspicious": False,
        "reason": "",
    }
    result = await antisybil_graph.ainvoke(initial)
    return SybilScore(
        user_id=user_id,
        score=result["score"],
        is_suspicious=result["is_suspicious"],
        reason=result["reason"],
    )
