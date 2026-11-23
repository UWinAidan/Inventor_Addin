# 222: Mass property in kg or lb, printed by the templates

- **Milestone:** M5 (Aidan, 2026-11-22 and 2026-11-23; spec 05 questions 28 and 29)
- **Feature spec:** `docs/features/05-drawing-tools.md` (open questions 28 and 29)
- **Status:** done (passed review; not run in Inventor; Aidan rebuilds the templates)
- **Depends on:** 219, 225
- **Needs:** windows build; Aidan rebuilds both templates afterwards
- **Parallel-safe with:** none

## Goal

"lbmass" is not acceptable on the imperial print (Aidan, question 29), and spike 225 showed the WEIGHT field cannot print "lb". So question 29's fallback is built: the add-in writes a custom text property "Mass" ("9.55 lb" on imperial parts, "4.33 kg" on metric, 2 decimals), refreshed automatically whenever the part is saved, and both templates' WEIGHT field prints that property instead of Inventor's live mass.

## Scope

- Core: the text rule (mass in kg from Inventor, converted to lb for imperial parts, 2 decimals, the invariant period, the unit after a space), the property location (custom "Mass"), and when a write is needed (only when the text differs); tests.
- Add-in: a document save event handler (find the existing application or document event wiring; `ApplicationEvents.OnSaveDocument` with `kBefore` is the usual one; confirm it against the interop and list it if unconfirmed) that, for a part, reads `MassProperties.Mass`, decides the units with `MaterialFinishTargets.UnitsOf`, and writes the property before the save so it is saved with the part. It must never fail or slow a save badly: wrap in `ComSafe`, log once per failure. Assemblies: Default, the same rule (an assembly's mass is its total).
- The same write in Create Print and Mass Update with the other backfills, so an old part prints a weight before its next save.
- `PrintTemplateLayout` / `PrintTemplateBuilder`: the WEIGHT field prints the model's custom "Mass" property (a `<Property>` field on the model's user-defined set, as the builder does for other custom properties) instead of `<PhysicalProperty>`.
- The part's own mass unit is not changed (spike 225: a unit change alone did not survive save and reopen).

## Acceptance criteria

- [x] Saving a part writes "Mass" as "4.33 kg" (metric) or "9.55 lb" (imperial), and editing the part then saving updates it.
- [x] Create Print on an old part with no Mass prints its weight; the WEIGHT field prints the property on both templates.
- [x] A save never fails because of the handler.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: 0 errors (11 existing warnings). `dotnet test tests/InventorAddin.Core.Tests`: 5340 to 5341 passed, 1 to 2 failed per run, all in the known timing-flaky Numbering group (`RenamePlanTests.ListFilesToSearch_ASlowListing...`, `SaveAsCopyCreatorTests.FailureAfterAssigning...`); the new `MassTextTests` pass. Nothing was run in Inventor.
- **Not compiled (changed under `src/InventorAddin`):** none; all built on Windows. Not run in Inventor: `Extraction/MassOnSave.cs`, `Extraction/MassPropertyReader.cs`, `Extraction/FinishSpecBackfiller.cs`, `StandardAddInServer.cs`, `Drawing/PrintTemplateBuilder.cs`.
- **Inventor API members not confirmed:** none unconfirmed by reflection. By reflection on the 2026 interop: `ApplicationEvents.OnSaveDocument` (handler `_Document, EventTimingEnum, NameValueMap, out HandlingCodeEnum`). `MassProperties.Mass` (Double) on part and assembly component definitions is in existing use (`InitialViewsPlacer.ReadMass`). Not confirmed in Inventor: that a property written inside the `kBefore` save event is saved with that file, and that `OnSaveDocument` also fires for Save As and for `Save()` called by Mass Update (the manual checklist covers both).
- **Defaults chosen:** the Mass text is not written when the mass cannot be read or is negative; a zero mass writes "0.00 kg". Assemblies use the same rule (total mass), and weldments count as assemblies. The save-event write is made outside a transaction (no extra undo step). The Mass write is part of `FinishSpecBackfiller.WritesFor`, so Create Print, Mass Update and the Material and Finish window all refresh it. A Mass the add-in already wrote but that differs is replaced (any Mass text is treated as the add-in's own).
- **Manual checklist for Inventor:**
  1. Open a metric part, Save. Open iProperties, Custom tab: "Mass" is "N.NN kg" and matches Inventor's mass (rounded to 2 decimals).
  2. Open an imperial part (inch length units). Save. "Mass" is "N.NN lb" (kg x 2.2046). Close and reopen: still there.
  3. Change the part (extrude longer), Save: "Mass" changes. Save an unchanged part: no "Property write" log line for Mass in `awbaddin.log`.
  4. Save an assembly: Mass is the total mass. Save a drawing: no Mass property added.
  5. Make a part read-only (or open one from a read-only folder) and Save As elsewhere: the save still succeeds; the log shows at most one warning.
  6. Take an old part with no Mass, run Create Print: the model gets Mass (log "Filled in Mass") before the drawing is made.
  7. Rebuild both templates (Build Print Template, metric and imperial). Create Print on the imperial part: WEIGHT prints "9.55 lb" (no "lbmass"); on a metric part "4.33 kg". Open the template's WEIGHT text: it is a custom property field "Mass" on the model.
  8. Create Print on a part opened with no saved Mass and never saved after the add-in update: WEIGHT is filled (the backfill wrote it).

## Follow-ups

Things noticed but not done.

- `PrintTemplateBuilder` still sets the imperial template's drawing mass unit to lb ("Setting the mass unit to lb"). The WEIGHT field no longer uses it, so it can be removed once the rebuilt template is checked.
- `PrintTemplateLayout.SampleValue(Mass)` is "123.45 kg" for both units; an imperial sample "123.45 lb" would make the fit check exact.
- Parts saved before this task keep no Mass until they are saved, backfilled by Create Print or Mass Update, or opened in Material and Finish.
