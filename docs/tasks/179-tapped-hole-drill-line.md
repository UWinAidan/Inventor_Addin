# 179: Fix: a tapped group keeps its drill line after Update

- **Milestone:** M4 part 2 (fix from Aidan's check of 174 to 176, 2026-11-20)
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Target output"; open questions 11 and 19)
- **Status:** done
- **Depends on:** 156, 163, 170
- **Needs:** windows (Core and add-in)
- **Parallel-safe with:** 177, 178

## Goal

After Update, the M6 tapped group (J) lost its drill line `Ø5.0 ↧ 12.0`; only the thread line `M6x1 - 6H ↧ 9.0` showed. The description writes the hole line only when `HoleTableHole.DrillDiameterCm` has a value, and for a straight tapped hole that value comes from `HoleFeatureReader.ReadFacts` reading `HoleFeature.HoleDiameter`. Task 170 found that a tapped hole may have no diameter parameter (the getter throws). So the drill line depends on a read that can fail. A tapped hole's tap drill must not depend on it.

## Scope

- `src/InventorAddin.Core/HoleTable/HoleTableHole.cs` (`HoleTableHoles.From`): for a straight tapped hole (`HoleTableType.Tapped`) the drill diameter is, in order:
  1. the AWB stored data's applied hole diameter (the tap drill AWB Hole modelled), when given;
  2. Inventor's hole diameter (`facts.HoleDiameterCm`);
  3. the AWB Tapped row's tap drill for the hole's thread designation, when given (converted to cm, as the taper row is).
  Add optional parameters for 1 and 3 next to `taperTappedRow`. Taper tapped holes are unchanged. Other types still use `facts.HoleDiameterCm` only. Document the order on the `DrillDiameterCm` parameter.
- `src/InventorAddin.Core/Holes/HoleReadBack.cs`: a `TappedRowFor(InventorHoleFacts, HoleWorkbook)` beside `TaperTappedRowFor`, if no such lookup exists, matching the thread designation (and type when given) the way the read-back already matches tapped holes. Reuse the existing match; do not write a second one.
- `src/InventorAddin/HoleTable/HoleTableSource.cs` (where `HoleTableHoles.From` is called): pass the stored data's applied hole diameter (check the real member path on `HoleStoredDataReadResult`) and the tapped row (from the cached workbook, like `FindTaperRow`). When a tapped hole still ends with no drill diameter, log one warning naming the hole: `Hole table: Hole22 is tapped but its tap drill could not be read; its hole line is left out.`
- Tests in `tests/InventorAddin.Core.Tests` for the order and for the Tapped row lookup.

Out of scope: the description layout; taper tapped holes; the AWB Hole window.

## Acceptance criteria

- [ ] Core: a tapped hole whose facts have no hole diameter but whose AWB data has one: the description's first line is the drill line with that diameter and the drill depth.
- [ ] Core: no AWB data, facts with a diameter: that diameter (behaviour as now).
- [ ] Core: neither, but a Tapped row for the designation: the row's tap drill.
- [ ] Core: none of the three: no drill line, as now (question 19).
- [ ] Core: AWB data and facts both present and different: the AWB value wins (the same order `HoleDepthReference` uses for tapped holes).
- [ ] Add-in: no new Inventor API member; any member path on the stored data is checked against existing use.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Notes for the implementer

- `HoleDepthReference` (Core, around line 210) already prefers the stored hole diameter for tapped holes. Follow it.
- `HoleFeatureReader.ReadFacts` reads `Length(() => hole.HoleDiameter)`; leave it as it is.
- Before choosing the fix, look at whether anything on the Update path differs from Create Tables in how the hole is read (for example a fresh `HoleTableSource` with no workbook loaded). If you find the real reason the drill read differed, write it under Verification.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 4965 passed, 0 failed (first run had 5 known flaky failures in RenamePlan and similar; passed on rerun). `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors, 8 warnings (existing).
- **Not compiled (changed under `src/InventorAddin`):** none; `HoleTable/HoleTableSource.cs` was built on Windows (0 errors). Not run in Inventor.
- **Inventor API members not confirmed:** none new. The stored diameter is `stored?.Data?.Applied.HoleDiameterCm` (`HoleStoredData.Applied`, `HoleAppliedState.HoleDiameterCm`, Core types; the same path `HoleReapply` uses).
- **Why Update lost the drill line:** not proven without Inventor. Update and Create Tables both build a fresh `HoleTableSource` and read through the same `ReadHole`, so nothing on the Update path reads the hole differently. The cause is most likely `HoleFeature.HoleDiameter` not reading for the tapped hole (task 170), which the old code left as no drill line. The new log warning will name any hole where all three sources fail.
- **Changes:** `HoleTableHoles.From` takes `storedHoleDiameterCm` and `tappedRow` (order: stored, Inventor, row; taper unchanged). `HoleReadBack.TappedRowFor` added; `TapDrillCm` now reuses it instead of its own copy of the match. Tests added in `HoleTableTests` (order, description line) and `HoleTableViewRulesTests` (row lookup).
- **Manual checklist for Inventor:**
  1. Drawing whose tables have an M6 tapped group made with AWB Hole. Move a hole in the part, save, Update: the tapped group still shows `Ø5.0 ↧ 12.0` above `M6x1 - 6H ↧ 9.0`.
  2. The log has no `is tapped but its tap drill could not be read` line for that hole. If it does, send me the line.

## Follow-ups

Things noticed but not done.
