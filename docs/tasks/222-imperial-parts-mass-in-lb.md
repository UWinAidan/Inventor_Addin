# 222: Mass property in kg or lb, printed by the templates

- **Milestone:** M5 (Aidan, 2026-11-22 and 2026-11-23; spec 05 questions 28 and 29)
- **Feature spec:** `docs/features/05-drawing-tools.md` (open questions 28 and 29)
- **Status:** todo
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

- [ ] Saving a part writes "Mass" as "4.33 kg" (metric) or "9.55 lb" (imperial), and editing the part then saving updates it.
- [ ] Create Print on an old part with no Mass prints its weight; the WEIGHT field prints the property on both templates.
- [ ] A save never fails because of the handler.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
