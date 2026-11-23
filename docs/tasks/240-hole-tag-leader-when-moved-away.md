# 240: A hole tag moved away from its hole gets a leader

- **Milestone:** M5 (Aidan's request, 2026-11-23)
- **Feature spec:** `docs/features/04-hole-table.md` (tasks 165, 174 to 181)
- **Status:** todo
- **Depends on:** none
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

A hole tag ("A1") dragged away from its hole is hard to match to the hole. When a tag sits more than a set distance from its hole it shows a leader to the hole; a tag close to its hole stays without one.

## Scope

- Tags are sketched symbols placed with `AddWithLeader` and `LeaderVisible = false` (`HoleTableMarker`, spike 160 round 7), so the leader already exists and stays attached to the hole; only its visibility changes.
- Core: `HoleTableMarks` gets the rule: the gap is the shortest distance on paper from the tag's box to the hole's edge (centre distance minus the drawn radius); the leader shows when the gap is more than 5 mm (Default, spec 04 question 26). Tests, including a tag exactly at the distance and a tag overlapping the hole.
- Add-in: on Update (the Hole Table window's Update and the automatic out-of-date check's Update), read each tag's range box and its hole's centre and radius, and set `LeaderVisible` from the rule. Inventor gives no event while a sketched symbol is dragged, so the leader appears or goes on the next Update, not live (Default). Placement on Create and Generate is unchanged (new tags sit beside their hole).
- Moving a tag must not by itself make the drawing out of date for the automatic check.

## Acceptance criteria

- [ ] A tag dragged 20 mm away gets a leader to its hole on Update; dragged back beside the hole, the leader goes on the next Update.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
