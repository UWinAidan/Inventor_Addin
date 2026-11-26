# 198: Part marking: options, note text and window model (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Added for M5: New tools", item 2; open question 16)
- **Status:** done
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 196

## Goal

Core holds the marking options, builds the marking note text, and the window's view-model.

## Scope

- `src/InventorAddin.Core/Drawing/PartMarking*.cs`: method (laser engrave, stamp, etch, ink, none), extra lines, target (leader or zone), engrave feature on/off, character height and depth per units, highlight colour from Settings (`PartMarkingColour`, default `#0070C0`).
- `src/InventorAddin.Core/ViewModels/PartMarkingViewModel.cs`.
- Tests.

## Acceptance criteria

- [x] Note text, for example "LASER ENGRAVE PART NUMBER, 3 mm CHARACTERS" then each extra line; method None gives only the extra lines (for example "DO NOT STAMP").
- [x] The zone text "MARKING MUST BE PLACED INSIDE THIS ZONE"; zone size 40 by 15 mm (imperial 1.5 by 0.6 in).
- [x] The engrave option is off for an assembly.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Files

- `src/InventorAddin.Core/Drawing/PartMarking.cs` (enums `PartMarkingMethod`, `PartMarkingTarget`, record `PartMarkingOptions`, static `PartMarking`: sizes per units and in cm, note text, colour normalising)
- `src/InventorAddin.Core/ViewModels/PartMarkingViewModel.cs`
- `src/InventorAddin.Core/Settings/AddinSettings.cs` (`PartMarkingColour`)
- `tests/InventorAddin.Core.Tests/Drawing/PartMarkingTests.cs`, and one line in `Settings/SettingsStoreTests.cs`

## Defaults

Beyond the spec's question 16 defaults (open question 16 stands):

- Note wording for the other methods: "STAMP PART NUMBER, ...", "ETCH PART NUMBER, ...", "INK MARK PART NUMBER, ...". Imperial reads "0.12 in CHARACTERS".
- The engrave feature is offered for a part whose method is not None (off for an assembly, as the spec says). Spec 05 question 16 should ask: engrave feature only for laser engrave and etch?
- An invalid highlight colour (not 6-digit hex) is stored as #0070C0.
- The window cannot apply when the method is None and there are no extra lines (nothing to place).

## Verification

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests`: 5688 passed, 0 failed (first run had 3 failures in the known flaky probe-timeout `NewPartCreatorTests`; the rerun was clean). `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors.
- **Not compiled (changed under `src/InventorAddin`):** none; nothing under it changed. The new Core code has no UI yet.
- **Inventor API members not confirmed:** none used.
- **Manual checklist for Inventor:** nothing to check here; Core only. The window and command come in a later task.

## Follow-ups

- The add-in task that uses this should read `AddinSettings.PartMarkingColour` and pass it to `PartMarkingViewModel`; Settings window has no field for it yet (edit settings.json until then).
- Add `PartMarkingColour` to the Settings window when the command task lands.
