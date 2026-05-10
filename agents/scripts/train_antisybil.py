"""
Offline training script for the Anti-Sybil RandomForest classifier.

Usage:
  cd agents
  python scripts/train_antisybil.py --db postgresql://... [--output models/antisybil_rf.pkl]

Requires labelled data in a CSV file with columns:
  user_id, device_collision, ip_collision, bssid_collision,
  mission_count_30d, unique_partners_30d, neo4j_component_size, label (0=clean, 1=sybil)

In production, generate labels by:
  - Manual review of flagged users (precision labelling)
  - Confirmed fraud chargebacks from Stripe
  - Heuristic bootstrapping on known-bad patterns
"""
from __future__ import annotations
import argparse
import pickle
from pathlib import Path

import numpy as np
import pandas as pd
from sklearn.ensemble import RandomForestClassifier
from sklearn.model_selection import cross_val_score, train_test_split
from sklearn.metrics import classification_report

FEATURES = [
    "device_collision",
    "ip_collision",
    "bssid_collision",
    "mission_count_30d",
    "unique_partners_30d",
    "neo4j_component_size",
]
DEFAULT_OUTPUT = Path(__file__).parent.parent / "models" / "antisybil_rf.pkl"


def train(csv_path: str, output: Path) -> None:
    df = pd.read_csv(csv_path)
    X = df[FEATURES].fillna(0).values
    y = df["label"].values

    X_train, X_test, y_train, y_test = train_test_split(X, y, test_size=0.2, random_state=42, stratify=y)

    model = RandomForestClassifier(
        n_estimators=200,
        max_depth=8,
        class_weight="balanced",  # handles imbalanced sybil/clean ratio
        random_state=42,
        n_jobs=-1,
    )
    model.fit(X_train, y_train)

    cv_scores = cross_val_score(model, X, y, cv=5, scoring="roc_auc")
    print(f"Cross-val ROC-AUC: {cv_scores.mean():.3f} ± {cv_scores.std():.3f}")
    print(classification_report(y_test, model.predict(X_test)))

    output.parent.mkdir(parents=True, exist_ok=True)
    with open(output, "wb") as f:
        pickle.dump(model, f)
    print(f"Model saved to {output}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Train Anti-Sybil RandomForest")
    parser.add_argument("--csv", required=True, help="Path to labelled CSV")
    parser.add_argument("--output", default=str(DEFAULT_OUTPUT), help="Output .pkl path")
    args = parser.parse_args()
    train(args.csv, Path(args.output))
