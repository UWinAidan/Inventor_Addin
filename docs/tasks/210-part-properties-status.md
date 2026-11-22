# 210: Part Properties: Status

- **Milestone:** M5
- **Feature spec:** `docs/features/07-part-properties.md` (open question 7); `docs/features/05-drawing-tools.md` ("Decided for M5", title block Status)
- **Status:** todo
- **Depends on:** none
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** any Core-only task

## Goal

Part Properties sets a part's status, writing Inventor's built-in Design Status (the real value, for a future PDM or Vault) and a readable custom **Status** text property that the print's title block shows (Aidan, 2026-11-22). Spike 183 found that Design Status prints as "WorkInProgress" in a title block, so the template reads Status instead.

## Scope

- `src/InventorAddin.Core/PartProperties/` (snapshot, storage map, write plan) and their tests
- `src/InventorAddin.Core/ViewModels/PartPropertiesViewModel.cs` and its tests
- `src/InventorAddin/Extraction/` (reading Design Status and Status), `src/InventorAddin/UI/PartPropertiesWindow.xaml`
- `docs/features/07-part-properties.md`

## Acceptance criteria

- [ ] Core has a status type with three values and their texts: Work In Progress / `WORK IN PROGRESS`, Pending / `PENDING`, Released / `RELEASED`, and the mapping to and from Inventor's Design Status number (1 is Work In Progress, seen in spike 183; confirm 2 and 3 in Inventor or the API reference and record it).
- [ ] The snapshot reads Design Status and the custom Status. The write plan writes both together whenever the status changes: Design Status in Design Tracking Properties, Status as a custom text property (created when missing).
- [ ] Part Properties shows a Status dropdown (Work In Progress, Pending, Released) in the Identity section, on the shared look (spec 08).
- [ ] Default (`DECISIONS.md` 2026-11-22, spec 07 question 7): on open, a custom Status that is missing or does not match Design Status counts as a pre-filled pending change (spec 07 question 5), so Apply or OK writes it. An unknown Design Status number shows as blank and writes nothing.
- [ ] Assemblies get the same field. A file that cannot be edited shows it disabled.
- [ ] One transaction per Apply, as today; one Undo reverts both properties.
- [ ] Core tests cover the mapping, the pre-fill rule and the write plan. `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds.

## Notes for the implementer

- Property names are in `PropertySetNames.cs` and `PartPropertyStorage.cs`; add "Status" there, not as a string literal in the add-in.
- The Design Status property reads back as an `Int32` (spike 183, round 1a). Check how a write takes the value before relying on it, and list it under "Inventor API members not confirmed" if it was not tried.

## Verification

Filled in by the implementer.

## Follow-ups

Filled in by the implementer.
