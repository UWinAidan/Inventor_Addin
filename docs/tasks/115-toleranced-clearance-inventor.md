# 115: Toleranced Clearance and Simple holes in Inventor and the window

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Toleranced clearance and Simple holes")
- **Status:** todo
- **Depends on:** 108, 113, 114
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

A Clearance or Simple hole can be toleranced in the window (Aidan, 2026-10-24): the tolerance goes on its diameter in Inventor, it takes the precision colour, and Edit reads it back.

## Scope

- `src/InventorAddin/Holes/HoleFeatureWriter.cs`, `HoleFitWriter.cs`, `HoleFeatureReader.cs`: the tolerance on the through diameter of a Clearance or Simple hole (seated too, the seat untouched), from an ISO row, a Limits row or Custom limits, as spike 108 question 5 confirmed: `HoleFitWriter.ApplyTolerance` works unchanged on a seated clearance hole (ISO gives `kLimitsFitsStackedTolerance`, Custom gives `kDeviationTolerance`, the counterbore diameter stays `kDefaultTolerance`) and `HoleFeatureReader.ReadFacts` reads both back, so no new API work is expected; the colour from task 113's rule on every face; unticking removes both (task 096's reset).
- `src/InventorAddin/UI/AwbHoleWindow.xaml(.cs)`: the Toleranced checkbox on one row, the Tolerance row it opens (the dowel's dropdown, reused), and the Custom Upper and Lower boxes.

## Acceptance criteria

- [ ] A toleranced clearance hole (unseated and counterbored) and a toleranced Simple hole show the tolerance in Inventor's own hole dialog and on the diameter parameter, and are coloured on every face.
- [ ] Custom limits reach Inventor as deviations.
- [ ] Unticking Toleranced on an edited hole puts every face back to the part's colour and removes the tolerance; one Undo brings both back.
- [ ] The solution builds on Windows; `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer.

## Follow-ups
