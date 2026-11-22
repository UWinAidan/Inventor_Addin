# 216: Print template builder fixes and rebuild

- **Milestone:** M5 (from Aidan's task 187 check, 2026-11-22)
- **Feature spec:** `docs/features/05-drawing-tools.md`; task 187
- **Status:** todo
- **Depends on:** 187, 215
- **Needs:** **windows** (drives Inventor; Aidan runs the command)
- **Parallel-safe with:** none

## Goal

The builder draws task 215's corrected layout. The metric standard prints a decimal point in every style that shows numbers. The Status cell reads Inventor's own Status field. The build measures each text against its cell. Aidan then rebuilds both templates and checks them again.

## Scope

- `src/InventorAddin/Drawing/PrintTemplateBuilder.cs`:
  - **Decimal marker.** On the AWB Metric standard and every style that shows numbers (dimension styles, text styles where applicable, parts list, revision table, hole table styles), set the decimal marker to a period. Aidan's check: "260,00" still printed with a comma after task 187 set only the standard's `DecimalMarkerType`. Find where Inventor keeps the marker per style, for example `DimensionStyle` members.
    - Check each member against the interop by reflection (`C:\Program Files\Autodesk\Inventor 2026\Bin\Public Assemblies\Autodesk.Inventor.Interop.dll` and its `.xml`).
    - List each member under Verification.
  - **Status field.** Read Design Tracking "User Status" (`kUserStatusDesignTrackingProperties`, PropertyID 17, the Status tab's Status text) instead of the custom Status property (Aidan, 2026-11-22, task 219).
    - Change `PrintField.Status`'s documentation in Core to match.
    - The field uses the confirmed `<Property Document='model' PropertySet='Design Tracking Properties' ... PropertyID='17'>` form.
  - **Measure each text.** After drawing each text, read its `RangeBox` and warn in the log (and in the command's report) when it extends past its cell. Use the layout's cell bounds.
- **Rebuild.** Aidan deploys and runs Build Print Template again. The parent commits the two `.idw` files when he passes them.

## Acceptance criteria

- [ ] A metric drawing from the rebuilt template dimensions "260.00", and parts list and hole table numbers use a period.
- [ ] STATUS shows the part's Status tab value.
- [ ] The build log reports no text past its cell.
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors and the Core tests pass; every Inventor member is confirmed or listed.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
