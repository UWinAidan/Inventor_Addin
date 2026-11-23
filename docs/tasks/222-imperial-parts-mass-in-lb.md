# 222: Imperial parts weigh in lb

- **Milestone:** M5 (Aidan, 2026-11-22; spec 05 question 28, option (a))
- **Feature spec:** `docs/features/05-drawing-tools.md` (open questions 28 and 29)
- **Status:** todo
- **Depends on:** 219, 225
- **Needs:** windows build
- **Parallel-safe with:** none (shares `FinishSpecBackfiller` and the three backfill call sites with 188)

## Goal

The imperial print's WEIGHT field prints lb. The field follows the part's own mass unit (spike 183 round 4), so the add-in sets an imperial part's mass unit to lb wherever it already fills in missing print properties, and when New Part makes an imperial part.

## Scope

- Core: one decision, for example `PrintMassUnit.For(PrintUnits units, MassUnit current)`, returning lb when the part is imperial and its mass unit is not already lb, else nothing. Metric parts are left as they are (Default). Tests.
- `src/InventorAddin/Extraction/FinishSpecBackfiller.cs` (or a sibling): reads `UnitsOfMeasure.MassUnits` with `ComSafe` and applies the change. Imperial is decided by `MaterialFinishTargets.UnitsOf`, as for Finish Spec.
- The three backfill call sites, inside their existing transactions: Material and Finish on opening (`MaterialFinishCommand`), Create Print before the drawing is made (`CreatePrintCommand`), Mass Update (`FileScanner` and the command that writes its plan). Mass Update's plan lists the change like its other backfill lines.
- New Part (`Templates.cs` / the New Part command): an imperial part gets lb before it is first saved.
- After the change set `Document.Dirty = true`: the unit change alone leaves the part unmodified, and `Save()` would skip it (spike 183 round 4).
- The log line names the part and "mass unit set to lb".

- **The unit text (Aidan, 2026-11-23, question 29):** "lbmass" is not acceptable. Use spike 225's finding: if the mass field can print "lb", do that. If not, use option (b): the add-in writes a "Mass" text property ("9.55 lb" / "4.33 kg", 2 decimals, in the part's mass unit), refreshed automatically when the part is saved (a document save event), and the templates' WEIGHT field prints that property; the template builder changes accordingly.

Out of scope: assemblies; metric parts with lb.

## Acceptance criteria

- [ ] An imperial part with kg mass: opening Material and Finish, Create Print or Mass Update sets it to lb, and Create Print's drawing shows "... lb".
- [ ] New Part with the imperial template makes a part whose mass unit is lb.
- [ ] A metric part's mass unit is never changed.
- [ ] A part already in lb is not touched, and is not made dirty.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member used is confirmed in spike 183 or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** include: one Undo after Create Print or Material and Finish puts the mass unit back (not tried in the spike).

## Follow-ups

Things noticed but not done.
