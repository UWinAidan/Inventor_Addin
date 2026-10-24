# 117: Default hole centres per point, like Inventor's Hole

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window": hole centres; question 11)
- **Status:** todo
- **Depends on:** 094
- **Needs:** windows build (Core rule first)
- **Parallel-safe with:** 110, 111, 112, 113, 119

## Goal

Aidan, 2026-10-24 (question 11): with nothing selected, AWB Hole picks centres per point, like Inventor's Hole: the newest visible sketch that has hole-centre points no feature uses yet, and only those points. This replaces task 094's per-sketch rule, which skipped a sketch whole once any feature consumed it.

## Scope

- `src/InventorAddin.Core/Holes/HoleSessionRules.cs`: the choice of sketch and of the points picked, from a list of sketches with their visibility, age and, per hole-centre point, whether a feature uses it. A selected sketch or selected points still win, consumed or not (task 094). Tests.
- `src/InventorAddin/Holes/HoleSession.cs`: read which points a feature already uses (from the sketch's dependent features, or each point's dependents, as is cheapest and reliable; check the members against existing usage or the API reference) and pass them to the rule; the start log line says how the sketch and points were chosen.

## Acceptance criteria

- [ ] A sketch with four hole centres, two used by an earlier hole, opens AWB Hole with the other two picked.
- [ ] A sketch whose centres are all used is skipped for the next newest with free centres.
- [ ] Selecting a sketch first still picks all its points.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Verification

Filled in by the implementer.

## Follow-ups
