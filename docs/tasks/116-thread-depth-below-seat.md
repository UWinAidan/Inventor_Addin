# 116: Thread and drill depths measured from the bottom of the seat

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Tapped holes"; question 12)
- **Status:** todo
- **Depends on:** 114
- **Needs:** windows build (Core first, then the writer and reader)
- **Parallel-safe with:** none

## Goal

Aidan, 2026-10-24 (question 12): a seated tapped or taper tapped hole's thread and drill depths are measured from the bottom of the seat, so an M6 counterbored hole keeps its full thread below the counterbore.

## Scope

- Core (`HoleGeometry.cs`, `HoleRules.cs`, `HoleDiagram.cs`, `AwbHoleViewModel.cs`): the window's thread and drill depths are below the seat; the seat's depth is the counterbore or spotface depth, or for a countersink the depth where the cone meets the hole diameter (Default); the depth written to Inventor is the window's depth plus the seat's depth; read-back subtracts it. The diagram dimensions the depths from the seat bottom. Depth expressions (task 100) keep working: the written expression adds the seat depth to the user's (for example `(d0) + 6 mm` or the seat's parameter), so the hole stays parametric and the window shows the user's own expression on Edit.
- Stored data: record that depths are below the seat (a version bump), so a hole made before this task opens with its depths converted, not shifted.
- `src/InventorAddin/Holes/HoleFeatureWriter.cs`, `HoleFeatureReader.cs`: as spike 108 question 3 found Inventor measures them.
- A blind clearance, dowel or Simple hole's Depth is unchanged (Default, question 16).

## Acceptance criteria

- [ ] An M6 blind tapped hole with a 6 mm counterbore and thread depth 9 has 9 mm of thread below the counterbore in Inventor.
- [ ] Changing the seat depth keeps the thread length below it.
- [ ] Edit opens the hole with the depths below the seat; an older hole opens with its depths converted.
- [ ] Core tests for each seat; `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Verification

Filled in by the implementer.

## Follow-ups
