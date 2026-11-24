# 240: A hole tag moved away from its hole gets a leader

- **Milestone:** M5 (Aidan's request, 2026-11-23)
- **Feature spec:** `docs/features/04-hole-table.md` (tasks 165, 174 to 181)
- **Status:** done (built on Windows; not run in Inventor)
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

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5475 passed, 0 failed (one flaky probe-timeout failure on the first run passed on rerun). 5 new tests in `HoleTableMarksTests` (gap, beside, exactly 5 mm, 20 mm away, overlapping).
- **Not compiled (changed under `src/InventorAddin`):** none; all built on Windows. Not run in Inventor.
- **Inventor API members not confirmed:** `SketchedSymbol.Position` as the tag's centre after a drag (TagPoint's doc says the symbol lands centred on its point, from spike 160 round 7; not measured after a drag). `SketchedSymbol.RangeBox` does not exist (checked against the interop), so the tag's box is Position plus half the text width (measured as at placement) and half the font height. `SketchedSymbol.LeaderVisible` set to true: only set false in existing code; the leader should run to the hole's centre intent.
- **Manual checklist for Inventor:**
  1. Open a drawing with a view of a part with holes and Create the hole tables (Hole Table window).
  2. Drag one tag (e.g. A1) about 20 mm away (try one below-left of its hole too) from its hole. Click Update in the window. Expected: a leader appears from the tag to that hole; the status line says "1 tag shows a leader to its hole."; other tags have none.
  3. Drag the tag back beside the hole (within 5 mm of its edge). Update. Expected: the leader is gone.
  4. Drag a tag 20 mm away and do not Update; switch to another document and back (that runs the automatic check): it must not say the tables are out of date because of the move.
  5. Drag a tag onto its hole and Update: no leader.
  6. Ctrl+Z after an Update: one Undo reverts it.

## Follow-ups

- The gap uses the hole's outermost drawn circle (a seat or thread circle counts), so the tag clearing a seat is measured from the seat's edge. Say if it should be the drill circle.
- If Position turns out not to be the tag's centre after a drag, the box in `HoleTableMarker.SetLeaders` needs a different anchor.
- A leader on a tag shows only after Update, so the first Update after a move reports it; the status text only mentions tags that show a leader, not ones that lost it.
