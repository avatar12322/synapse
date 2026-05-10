"""
Phase 4 one-time migration: encrypt existing plaintext embeddings via .NET backend.

Reads UserProfiles where Embedding IS NOT NULL AND EncryptedEmbedding IS NULL,
calls POST /api/internal/profile/embedding for each — .NET stores AES-256-GCM copy.

Usage:
    cd agents
    SYNAPSE_API_URL=http://localhost:5000 INTERNAL_API_SECRET=<secret> python scripts/migrate_embeddings.py
"""
import asyncio
import logging
import os

import asyncpg
import httpx

logging.basicConfig(level=logging.INFO, format="%(levelname)s %(message)s")
logger = logging.getLogger(__name__)

DATABASE_URL = os.environ.get("DATABASE_URL", "postgresql://postgres:postgres@localhost:5432/synapse")
SYNAPSE_API_URL = os.environ.get("SYNAPSE_API_URL", "http://localhost:5000")
INTERNAL_SECRET = os.environ.get("INTERNAL_API_SECRET", "")
BATCH_SIZE = 50


async def main() -> None:
    conn = await asyncpg.connect(DATABASE_URL)
    try:
        rows = await conn.fetch(
            'SELECT "UserId", "Embedding"::text FROM "UserProfiles" '
            'WHERE "Embedding" IS NOT NULL AND "EncryptedEmbedding" IS NULL'
        )
    finally:
        await conn.close()

    logger.info("Found %d profiles to encrypt", len(rows))
    if not rows:
        return

    ok = 0
    fail = 0
    async with httpx.AsyncClient(timeout=30.0) as client:
        for i, row in enumerate(rows):
            user_id = row["UserId"]
            raw = row["Embedding"].strip("[]")
            embedding = [float(x) for x in raw.split(",")]
            try:
                resp = await client.post(
                    f"{SYNAPSE_API_URL}/api/internal/profile/embedding",
                    json={"userId": user_id, "embedding": embedding},
                    headers={"X-Internal-Secret": INTERNAL_SECRET},
                )
                resp.raise_for_status()
                ok += 1
            except Exception as exc:
                logger.warning("userId=%d failed: %s", user_id, exc)
                fail += 1

            if (i + 1) % BATCH_SIZE == 0:
                logger.info("  %d/%d processed (ok=%d fail=%d)", i + 1, len(rows), ok, fail)

    logger.info("Migration complete — ok=%d fail=%d", ok, fail)


if __name__ == "__main__":
    asyncio.run(main())
