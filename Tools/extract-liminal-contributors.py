#!/usr/bin/env python3
"""Build the public LIMINAL credit roster from one Codex thread's descendants."""

from __future__ import annotations

import argparse
import hashlib
import json
import sqlite3
from pathlib import Path


ROOT_THREAD_ID = "019e12e6-4faa-7380-b8c1-83756b2de918"
EXPECTED_DESCENDANT_COUNT = 75
EXPECTED_CONTRIBUTOR_COUNT = 75
STOPPED_THREAD_IDS = {
    "01a0e194-4733-7210-b4d6-74c082580235",
    "01a0f37c-f494-7fc0-8c97-a446e29e6aeb",
}

# Roles are short editorial labels grounded in each selected task and its report.
ROLE_BY_THREAD_ID = {
    "01a0db86-44e8-7243-a662-5432f68900bd": "Free-Flight Navigation",
    "01a0e13e-d297-7730-9be5-ea703bccb639": "Process Audio Capture",
    "01a0e1d2-970e-75b3-abde-3e5097a283ce": "Locomotion and Combat",
    "01a0e1d3-00d2-7872-9209-7aecd767ab33": "Cavern Environment Art",
    "01a0e1d3-703d-7982-b1bf-1609f9e3ccf8": "Marine Fauna and Whale",
    "01a0e295-0e4d-7dd2-a7d1-05f201f4654a": "Persistent GPU Matter",
    "01a0e295-0ff9-7d93-b5f4-4116ef0ca6f2": "Serpent Matter Transitions",
    "01a0e398-2134-7141-a606-59c8fd38a8d0": "Serpent Combat Systems",
    "01a0e399-1d50-7b31-9c49-6464d1f1a6aa": "Whale Particle Geometry",
    "01a0e399-1df8-7133-9f2a-2fcd719878ab": "Horizon Water Rendering",
    "01a0e914-99f7-7bc2-89db-bbec2199ffd1": "Marine Environment Art",
    "01a0e914-9abe-7541-b1c0-53e1284ba503": "Whale Motion and Marine Life",
    "01a0e914-9b9b-7513-b411-9fd2f87e9154": "Horizon Water and Spray",
    "01a0f2c7-4a59-7d50-9008-c9a4695d0be2": "Visual Comparison Audit",
    "01a0f2f7-a0a4-7d22-89a5-3a0c7305e8df": "Cavern Background Art",
    "01a0f2f7-a153-7651-9203-3dead08c2079": "Combat Visual Effects",
    "01a0f336-3e4e-7df3-93bc-d7e50dee08a0": "Combat and Kinematics",
    "01a0f336-3f0e-76e1-a5a7-e5e84f1b2dcb": "Hit Feedback Effects",
    "01a0f33d-ab38-76e2-a5ff-4854fb5e1536": "Whale Deformation",
    "01a0f33f-6231-7be2-b13e-ee1fc2223409": "Cavern Environment Refinement",
    "01a0f33f-62dc-73b0-8b24-dbbf6981cf11": "Water Surface Effects",
    "01a0f34e-58da-7733-a7f5-646fdbbb263b": "Cavern Verification",
    "01a0f396-a4d5-7893-af4a-c31526866f22": "Whale and Dolphin Lifecycle",
    "01a0f396-a583-7f30-84fe-1ec3b6b8f730": "Dolphin Locomotion",
    "01a0f48e-4524-7851-9190-6bef0f14b061": "Dolphin Combat and Music",
    "01a0f492-dd76-7aa1-94dc-ec68f47a5a91": "Gameplay Capture Tooling",
    "01a0f64d-da38-78a2-a209-19615f2cfe5a": "Hermit Crab Encounter",
    "01a0f64d-dae8-76c2-b54f-ea2209e2a14f": "Submarine Encounter",
    "01a0f64d-dbe7-72d2-b29f-120a6e5b7f70": "Interactive Tutorial",
    "01a0f698-d9fc-7362-bf49-511439a08fd4": "Portal Effects",
    "01a0f6aa-4d98-7901-844e-edbbc085f1bc": "Display Brightness Controls",
    "01a0f75c-15c9-7730-9b8d-716c08b3d96c": "Hermit Character Redesign",
    "01a0f75c-1684-75a0-849b-1e5e440c6713": "Submarine Character Redesign",
    "01a0f75c-178c-7760-b16a-c4f8c577c0d6": "Cave Passage Visuals",
    "01a0f784-f474-7881-bd7b-4d3c5fedb7e0": "Visual Architecture Review",
    "01a0f7c9-3546-7960-8b73-647a5cfc3cad": "Hermit Boss Encounter",
    "01a0f7c9-3602-7131-9638-4a82a571908e": "Cave Layout and Shoals",
    "01a0f7c9-3a97-7e03-968c-db7f88ac99af": "Stage Music Transport",
    "01a0f7cb-2a71-7fd2-a2ef-4e25ffc9c52f": "Stage Inheritance",
    "01a0f7cb-2b28-7a73-a6fd-5f97c062f6c0": "Atlantis Finale",
    "01a0f7cc-5ba8-7fc0-b776-9f869f3980d8": "PCVR OpenXR Integration",
    "01a0f805-436a-7b91-81d6-4ca74ad28fd7": "Gameplay Systems Review",
    "01a0f88b-947c-79c3-8ff4-140762d17ff0": "Audio Sample Research",
    "01a0f8a5-592b-7f90-ad4d-7796235f7ecf": "PCVR Flight and Menus",
    "01a0f8a5-59dd-7293-8476-f8ab3dcb4a3d": "VR Targeting Policy",
    "01a0fadc-60e3-7bd0-8a33-fda9672eb129": "Capture Footage Review",
    "01a0fb1a-5073-7531-9a93-e7b691a3aa7a": "Desktop Credits HUD",
    "01a0fdbf-47dc-7421-a395-00f25a98aa87": "Submarine Encounter",
    "01a0fdbf-4895-7490-8e70-3700c20265a3": "VR HUD and Feedback",
    "01a0fdbf-49a5-7e80-8b66-734b9256a07a": "Whale Arrival Lighting",
    "01a0fe00-7eb1-7530-a90b-03d0956820cd": "Dolphin Encounter",
    "01a10152-1f0f-75b1-842a-25004b538a8e": "Final Review Proof",
    "01a10152-1fda-78b0-9dfd-405c956a2611": "Boss Release Music",
    "01a1018b-8d4e-7e13-b3ba-083f2ee8f0a4": "Whale Splash Proof",
    "01a101c4-4ea3-7491-a26a-e3af0c2df24d": "Combat Flow and Feedback",
    "01a1026c-9209-7880-b21e-70c9e2606074": "Atlantis Particle Life",
    "01a102bc-9514-74a0-9b78-206a2a20ecf1": "Room Retry Checkpoints",
    "01a102fb-1759-7c30-8db1-f7f1ac2267e0": "Particle Sharpness Proof",
    "01a1050c-046f-7ef1-8a37-816de929f713": "Sharp Particle Shaders",
    "01a1050c-0529-7753-a4da-c631c68d2d32": "Sharp Grain Rendering",
    "01a10579-86ca-7d23-8dd2-377296defc8e": "Particle Rendering Controls",
    "01a10579-8790-7c13-b393-3133d99677b2": "Atlantis Architecture",
    "01a105c6-5350-7333-884c-a2575790f38a": "VR Pause and Tutorial",
    "01a106bd-7006-7ac0-add7-e5dbb09feb94": "Ending Audio Preparation",
    "01a10799-7ccb-7832-8d03-4fa2d2369133": "Ending Audio Assets",
    "01a10799-7d8c-7e82-a0f2-52a693dbae98": "Staff Credits and Provenance",
    "01a10799-7e6d-77b3-8a52-3430cfbe8746": "Finale Celebration Fauna",
    "01a0db86-45a3-7ea3-beec-64e97e51f1fd": "Targeting and Spatial HUD",
    "01a0e194-4733-7210-b4d6-74c082580235": "PV Research and Recording",
    "01a0e295-0f12-75f3-ba19-973a69b23988": "Marine Interactions and Persistent Forms",
    "01a0f2a3-b6f4-7f70-953c-16427e6c5c11": "Visual Architecture Audit",
    "01a0f37c-f494-7fc0-8c97-a446e29e6aeb": "PV Finishing Script",
    "01a0f48f-0ce2-7292-873e-be04de944fdf": "Development Log and Media",
    "01a0fe00-7dfd-7710-b934-d5ce108afd83": "Pufferfish Encounter",
    "01a101e8-38ad-7bd2-8846-118dca8177f2": "Atlantis Visual Overhaul",
}

INTRO = [
    {"text": "LIMINAL", "heading": True, "featured": True},
    {"text": "ABYSSAL CHOIR", "heading": True, "featured": False},
    {"text": "CREATED BY", "heading": True, "featured": False},
    {"text": "tete", "heading": False, "featured": True},
]
TECHNOLOGIES = [
    "Unity 6",
    "Universal Render Pipeline",
    "Compute Shaders",
    "GraphicsBuffer",
    "DSP Scheduled Audio",
    "OpenXR",
    "ACE-Step XL-SFT",
    "Unity Recorder / FFmpeg",
]


def connect_read_only(path: Path) -> sqlite3.Connection:
    uri = path.resolve().as_uri() + "?mode=ro"
    return sqlite3.connect(uri, uri=True)


def get_descendants(connection: sqlite3.Connection, root_id: str) -> list[sqlite3.Row]:
    connection.row_factory = sqlite3.Row
    query = """
        WITH RECURSIVE descendants(id) AS (
            SELECT ?
            UNION
            SELECT edge.child_thread_id
            FROM thread_spawn_edges AS edge
            JOIN descendants AS parent ON edge.parent_thread_id = parent.id
        )
        SELECT thread.id, thread.rollout_path, thread.agent_nickname, thread.model,
               thread.reasoning_effort, edge.status
        FROM descendants
        JOIN threads AS thread ON thread.id = descendants.id
        LEFT JOIN thread_spawn_edges AS edge ON edge.child_thread_id = thread.id
        WHERE thread.id <> ?
        ORDER BY thread.created_at
    """
    return list(connection.execute(query, (root_id, root_id)))


def message_text(payload: dict) -> str:
    return "\n".join(
        item.get("text", "")
        for item in payload.get("content", [])
        if isinstance(item, dict) and isinstance(item.get("text"), str)
    ).strip()


def is_task_assignment(payload: dict) -> bool:
    if payload.get("role") != "user":
        return False
    text = message_text(payload)
    return bool(text) and not text.startswith((
        "<recommended_plugins>",
        "<turn_aborted>",
        "# AGENTS.md instructions",
    ))


def event_evidence(path: Path, thread_id: str) -> tuple[dict | None, dict | None, dict | None]:
    assignment = None
    final_responses = []
    stop_request = None
    with path.open("rb") as session:
        for line_number, raw in enumerate(session, 1):
            if b'"response_item"' not in raw:
                continue
            if b'"role":"user"' not in raw and b'"phase":"final_answer"' not in raw:
                continue
            try:
                record = json.loads(raw)
            except (json.JSONDecodeError, UnicodeDecodeError):
                continue
            if record.get("type") != "response_item":
                continue
            payload = record.get("payload", {})
            if payload.get("type") != "message":
                continue
            reference = {
                "file": path.name,
                "line": line_number,
                "sha256": hashlib.sha256(raw).hexdigest(),
            }
            if is_task_assignment(payload) and assignment is None:
                assignment = {**reference, "eventName": "task assignment"}
            elif payload.get("role") == "assistant" and payload.get("phase") == "final_answer":
                final_responses.append({**reference, "eventName": "final response"})
            elif thread_id in STOPPED_THREAD_IDS and payload.get("role") == "user":
                text = message_text(payload).lower()
                if "stop" in text or "cancel" in text:
                    stop_request = {**reference, "eventName": "stop request"}
    final_response = (final_responses[-1] if thread_id in STOPPED_THREAD_IDS else
                      final_responses[0] if final_responses else None)
    if thread_id in STOPPED_THREAD_IDS and stop_request is None:
        raise RuntimeError("No explicit stop request was found for " + thread_id)
    if thread_id in STOPPED_THREAD_IDS and final_response is not None:
        final_response = {**final_response, "eventName": "stopped acknowledgment"}
    return assignment, final_response, stop_request


def build_manifest(state_db: Path, root_id: str) -> dict:
    with connect_read_only(state_db) as connection:
        descendants = get_descendants(connection, root_id)
        if len(descendants) != EXPECTED_DESCENDANT_COUNT or len(ROLE_BY_THREAD_ID) != EXPECTED_CONTRIBUTOR_COUNT:
            raise RuntimeError("The descendant set changed; re-audit worker evidence before regenerating credits.")
        by_id = {row["id"]: row for row in descendants}
        missing_ids = sorted(set(ROLE_BY_THREAD_ID) - set(by_id))
        if missing_ids:
            raise RuntimeError("Curated contributor IDs are not descendants of the selected root: " + ", ".join(missing_ids))
        if len(by_id) - len(ROLE_BY_THREAD_ID) != EXPECTED_DESCENDANT_COUNT - EXPECTED_CONTRIBUTOR_COUNT:
            raise RuntimeError("The number of unverified descendants changed; review them before regeneration.")

        contributors = []
        status_counts = {"completed": 0, "stopped": 0, "unreported": 0}
        for thread_id, role in ROLE_BY_THREAD_ID.items():
            row = by_id[thread_id]
            if not row["agent_nickname"]:
                raise RuntimeError("A selected contributor has no recorded nickname: " + thread_id)
            session_path = Path(row["rollout_path"])
            assignment, final_response, stop_request = event_evidence(session_path, thread_id)
            if assignment is None:
                raise RuntimeError("No task assignment event was found for " + thread_id)

            evidence = [assignment]
            completion_status = (
                "stopped" if thread_id in STOPPED_THREAD_IDS else
                "completed" if final_response is not None else
                "unreported"
            )
            if stop_request is not None:
                evidence.append(stop_request)
            if final_response is not None:
                evidence.append(final_response)
            status_counts[completion_status] += 1

            contributor = {
                "id": thread_id,
                "nickname": row["agent_nickname"],
                "role": role,
                "model": row["model"] or "Model not recorded",
                "completionStatus": completion_status,
                "evidence": evidence,
            }
            contributors.append(contributor)

    return {
        "schemaVersion": 1,
        "creator": {
            "nickname": "tete",
            "role": "Creator and Producer",
            "evidence": [{"source": "User-provided creator credit"}],
        },
        "opening": INTRO,
        "contributorHeading": "PRODUCTION CONTRIBUTORS",
        "contributors": contributors,
        "technologyHeading": "TOOLS AND TECHNOLOGY",
        "technologies": TECHNOLOGIES,
        "closing": "THANK YOU FOR PLAYING",
        "coverage": {
            "descendantThreadCount": len(descendants),
            "verifiedAgentContributorCount": len(contributors),
            "excludedDescendantCount": len(descendants) - len(contributors),
            "completionStatusCounts": status_counts,
            "note": (
                "Every listed identity has an assignment event in the indexed descendant tree. "
                "Completion status describes reported task state, not integration or shipment. "
                "Same UUIDs resumed once; different UUIDs with matching nicknames remain separate. "
                "Compacted, unindexed, or pruned history may be missing."
            ),
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--state-db", required=True, type=Path, help="Codex state_*.sqlite file")
    parser.add_argument("--root-thread-id", default=ROOT_THREAD_ID)
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("Assets/Liminal/Resources/ProductionCredits.json"),
    )
    args = parser.parse_args()

    manifest = build_manifest(args.state_db, args.root_thread_id)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(
        "Wrote {} contributors from {} descendant sessions to {}".format(
            manifest["coverage"]["verifiedAgentContributorCount"],
            manifest["coverage"]["descendantThreadCount"],
            args.output,
        )
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
