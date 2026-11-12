# 155: Core: hole table holes, precision or clearance, and the group order key

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Decided behaviour", open questions 1, 6, 8 to 11)
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** 160

## Goal

Core has one model for a hole instance as the hole table sees it, built from what the add-in already reads for a hole feature, and decides for each one: precision or clearance, its table type, and the key its group sorts by. Tasks 156 to 159 build on it.

## Scope

New folder `src/InventorAddin.Core/HoleTable/` (namespace `InventorAddin.Core.HoleTable`), tests in `tests/InventorAddin.Core.Tests/HoleTable/`.

- `HoleTableType` enum, in sort order: `Plain` (Simple, Clearance and dowel holes with no seat), `Counterbore`, `Countersink`, `Spotface`, `Tapped`, `TaperTapped`.
- `HoleTableHole` record: one hole instance. At least:
  - `Id`: stable within a drawing: the feature name and the instance's index in the feature's centre list (`Plate Hole 3#2`). Task 160 may replace the index with something steadier; keep `Id` a string so that change stays local.
  - `FeatureName`, `Units` (`HoleUnits`, the hole's own: the AWB spec's, else the drawing's, passed in)
  - `Type` (`HoleTableType`), `IsPrecision`
  - the hole: `DrillDiameterCm`, `Termination`, `DepthCm`, `FlatBottom`, `DrillPointAngleDeg`
  - the seat: counterbore, countersink or spotface diameter, depth, angle (cm and degrees)
  - the thread: `ThreadDesignation`, `ThreadType`, `ThreadDepthCm` (null for full depth), `TaperedThread`
  - the tolerance: `HoleTolerance?` (existing record: name, ISO code, upper and lower in cm)
  - the position: `SheetXCm`, `SheetYCm`, the hole centre on the sheet (task 157 turns it into table coordinates)
- `HoleTableHoles.From(InventorHoleFacts facts, HoleSpec? awbSpec, ...)`: builds the instances of one hole feature, one per centre given. Rules:
  - **Precision** is `HolePrecision.IsPrecision(awbSpec, facts.HasTolerance)`; do not write a second rule (folds the task 113 follow-up in `TODO.md`).
  - **Type:** a thread decides first (question 10): tapered thread → `TaperTapped`, other thread → `Tapped`. Otherwise the seat: Counterbore, Countersink, Spotface; no seat → `Plain`. A dowel is `Plain` (or seated if it has a seat) and precision.
  - **Drill diameter** (question 11): the facts' hole diameter. A taper tapped hole has none in Inventor (spike 108); take the AWB spec's Taper Tapped row tap drill when the spec and workbook give one (pass the row or the value in; do not read the workbook here), else leave it null.
  - Lengths stay in cm. Inch rows' tap drills convert at this edge.
- `HoleTableOrder`: the comparer that orders **groups** (questions 6, 9, 11):
  - Precision table: drill diameter ascending.
  - Clearance table: `HoleTableType` order, then drill diameter ascending.
  - Ties (same type and diameter) fall back to an ordinal comparison of a tie-break string the caller passes (task 156 passes the description text). A null diameter sorts last within its type.
  - Never by quantity (Aidan: letters stay stable when instances are added or removed). Put that in the doc comment.
- `HoleTableDecimals` (Default, question 14), used by tasks 156 and 157: precision table 2 decimals in mm and 3 in inches; clearance table 1 in mm and 2 in inches.
- `HoleTableOrder.Instances`: orders instances within a group, Y ascending then X ascending, on the sheet position. Compare with a tolerance (0.0005 cm) so two holes on one row with float noise in Y still order by X.

Out of scope: descriptions (156), coordinates relative to the origin (157), letters (158), anything under `src/InventorAddin`.

## Acceptance criteria

- [ ] `From` gives one instance per centre, each with the feature's data and its own sheet position and `Id`.
- [ ] Type rules: tapped with a counterbore is `Tapped`; taper tapped with a countersink is `TaperTapped`; a seated dowel is its seat type and precision; Simple and Clearance with no seat are `Plain`.
- [ ] Precision: an AWB toleranced Clearance and Simple hole, every dowel, and an Inventor hole with a tolerance and no AWB data; an AWB untoleranced hole whose Inventor diameter happens to carry a tolerance is not (AWB data decides).
- [ ] Group order for each table as above, including the tie-break and a null diameter.
- [ ] Instance order Y then X, with the tolerance.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- `InventorHoleFacts` (`Holes/HoleReadBack.cs`) is what `HoleFeatureReader.ReadFacts` returns in the add-in; it is checked in Inventor and is the input here. `HoleStoredData.Read` gives the AWB `HoleSpec`, when a hole has stored data.
- `HolePrecision.cs` already holds the precision rule, written for this task's use.
- Follow the Core style: file-scoped namespace, records, XML doc comments that say where a rule came from (Aidan, date, or Default and the spec question).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none expected
- **Inventor API members not confirmed:** none expected
- **Manual checklist for Inventor:** none (Core only)

## Follow-ups

Things noticed but not done.
