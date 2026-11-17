# 163: Add-in: read a drawing view's holes and origin into Core (`IHoleTableSource`)

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Add-in side", open questions 7, 16 and 17)
- **Status:** todo
- **Depends on:** 155, 157, 159
- **Needs:** windows (add-in; Aidan checks in Inventor)
- **Parallel-safe with:** 161, 162

## Goal

The add-in implements task 159's `IHoleTableSource`: it lists sheets and views, and analysing a view returns its holes as `HoleTableHole`s, its ordinate origin, its scale and units, and how many holes were left out. It does this the way spike 160 proved.

## Scope

- **`src/InventorAddin/HoleTable/HoleTableSource.cs`.** Names follow the folder's style; this is a new folder.
- **Sheets and views.** The active drawing's `Sheets`, then each sheet's `DrawingViews` (names).
- **Analysing a view.** Use the view's referenced part (`ReferencedDocumentDescriptor.ReferencedDocument`). For an assembly view, stop with a message: part views only for now (spec question 7 in task 160).
  - For each unsuppressed `HoleFeature`:
    - **End on.** Take the axis of the first side face that is a cylinder or a cone (`SurfaceType` `kCylinderSurface` or `kConeSurface`, then `Geometry.AxisVector`). The view direction is `Camera.Eye.VectorTo(Camera.Target)`, normalised. If |dot| ≤ 0.999, count the hole's centres as left out (question 16).
    - **Facts.** `HoleFeatureReader.ReadFacts` for the facts, and `HoleStoredData` for the AWB spec, as AWB Hole does. Then task 155's `HoleTableHoles.From`.
    - **Centres.** For each centre in `HoleCenterPoints` (a `SketchPoint`): the sheet position is `DrawingView.ModelToSheetSpace(point.Geometry3d)`. The `Id` key comes from `point.GetReferenceKey(ref byte[] key, 0)`, then `ReferenceKeyManager.KeyToString(key)` (spike 160, C11). The 1-based index is kept for display.
  - **Origin.** If `HasOriginIndicator`, use `OriginIndicator.Intent.PointOnSheet`. Otherwise give no origin, and task 157 stops with its message.
  - **Scale.** The view's `Scale`.
  - **Units.** The drawing's length units (`UnitsOfMeasure.LengthUnits`), mapped to `HoleUnits`.
- Every COM read goes through `ComSafe`. A hole that throws is skipped with a warning in the log; it does not stop the analysis.
- Keep a map from each hole `Id` to its `HoleFeature`, its centre point, and the view's drawing curves at that centre. Tasks 164 and 165 need them. Late-bound callers must pass `DrawingCurves(Missing)`; C# can omit the argument.

Out of scope: the window (task 167), drawing anything (tasks 164 and 165).

## Acceptance criteria

- [ ] Builds on Windows with `dotnet build InventorAddin.slnx`.
- [ ] The manual checklist below passes in Inventor 2026 on Aidan's test part.

## Notes for the implementer

- Spike 160, "Recommendations" and rounds 1 and 5 (`docs/tasks/160-spike-hole-table-in-inventor.md`): every member here was used in the spike. The spike's reading code is `Read-Holes` in `spikes/160/spike160-round7.ps1` on branch `spike/160-hole-table`.
- `kConeSurface` is 5893. A taper tapped hole has no cylinder side face.
- Check member names against `Extraction/` and the interop. List any you could not confirm under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** with tasks 167's window (or the developer log until then): on the test drawing, VIEW1 lists all 18 holes with tags in the expected groups; VIEW2 (side view) lists none and says 18 left out; a view without ordinates stops with the no-origin message.

## Follow-ups

Things noticed but not done.
