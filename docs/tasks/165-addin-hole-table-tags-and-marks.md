# 165: Add-in: hole tags, precision marks and the AWB Centre Mark on the view

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Target output": on the view; "Design decision, settled by spike 160")
- **Status:** done
- **Depends on:** 163
- **Needs:** windows (add-in; Aidan checks in Inventor)
- **Parallel-safe with:** 164

## Goal

When the wizard creates tables, every hole in them gets three things: its tag beside it, a precision mark if it is a precision hole, and the same centre mark as every other hole (Aidan, round 7). All of them follow the view and the model.

## Scope

- **`src/InventorAddin/HoleTable/HoleTableMarker.cs`.**
- **`AWB Centre Mark` style.**
  - Find it in `StylesManager.CentermarkStyles`. If it is missing, copy the drawing's standard centre mark style to that name, as a local style kept in the drawing.
  - Set `GapDistance` 0.0001 cm (0 is refused), `MarkSize` 0.01, `OvershootDistance` 0.2 and `ExtensionLength` 0.2. Set them every time, so a changed style is put right.
  - Put the values in Core as named constants with Aidan's rule in a doc comment: the lines run continuously through the hole and end 2 mm past the outermost visible circle.
- **Outermost circle.** The largest-radius `kCircleCurve` drawing curve of the view centred on the hole (within 0.001 cm), thread edges included. That is the seat, else the thread circle, else the hole.
- **Centre mark.** `Centermarks.Add(sheet.CreateGeometryIntent(outermost), true, false, style)` on every hole, precision holes included.
- **Precision mark.**
  - A sketched symbol definition `AWB_PrecisionMark`, made once: a circle of radius 1 and the top-right quadrant filled (two radii and an arc as a profile, `SketchFillRegions.Add`). No centre lines.
  - Placed with `AddWithLeader(def, [centre intent], 0, holeRadius)` and `LeaderVisible = false`. Use two points (the centre point, then the intent) if one point is refused.
- **Tag.**
  - A sketched symbol definition `AWB_HoleTag`, made once: one text box `<Prompt>TAG</Prompt>` in the hole tag text style, justified left and bottom, at the origin.
  - Placed with `AddWithLeader(def, [point, centre intent], 0, 1, [tag])` and `LeaderVisible = false`.
  - The point puts the tag's lower-left corner 0.5 x the text height outside the outermost circle (or the precision mark, if larger) at 45 degrees, up and right. The symbol lands centred on its point.
- Attach an attribute set (`AWB_HoleMark`, the hole `Id`) to every tag, mark and centre mark, so tasks 166 and Update can find and replace them. The geometry rules (tag offset, 45 degrees) go in Core with tests.
- Same transaction as the tables (task 164).

## Acceptance criteria

- [ ] Builds on Windows. The Core rules have tests.
- [ ] The manual checklist below passes.

## Notes for the implementer

- Spike 160 "Recommendations" and rounds 3 to 7. The proved script is `spikes/160/spike160-round7.ps1` on branch `spike/160-hole-table`, section "4. tags and precision marks".
- `kThreadEdge` is 82689, and `kCircleCurve` is 5124.

## Verification

Filled in by the implementer (Windows, 2026-11-17).

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: succeeded, 0 errors (the 9 warnings are old ones in other files). `dotnet test tests/InventorAddin.Core.Tests`: 4815 passed, 0 failed (4805 before, plus 10 new in `HoleTableMarksTests`). Nothing was run in Inventor.
- **Not compiled (changed under `src/InventorAddin`):** none. Both files built on Windows: new `HoleTable/HoleTableMarker.cs`; changed `HoleTable/HoleTableSource.cs` (`DrawTables` now calls `MarkHoles` after the drawer, inside the same transaction). Built is not the same as working: none of it has run in Inventor.
- **Inventor API members not confirmed by existing use or the spike:** every member was checked against the interop assembly by reflection (signatures, enum names), and the usage follows spike 160 round 7. What reflection cannot show is behaviour. Unproven at run time: `DrawingCurveSegment.Geometry` cast to `Circle2d` or `Arc2d` (the spike read `.Radius` late-bound); `DrawingStandardStyle.ActiveObjectDefaults.CentermarkStyle.Copy(name)` as the source of the new style (the spike copied the style named `Center Mark (ANSI)` instead); `Centermark.AttributeSets` accepting an attribute set (wrapped in `ComSafe.Run`, so a refusal is silent and that centre mark is then not found again); `AttributeSets.Add(name, false)` on a `SketchedSymbol`; `HoleTableRows[1].HoleTag.DimensionStyle.TextStyle` on a temporary hole table (the spike used `Item(1)`); passing `PointIntentEnum` as the `Intent` argument of `Sheet.CreateGeometryIntent`.
- **Manual checklist for Inventor** (open the drawing with a part view that has an ordinate origin and a mix of holes: a precision hole, a tapped hole, a counterbore, a countersink, a plain hole; Inventor must have this add-in loaded from a Windows build with deploy):
  1. Open the Hole Table window (Hole Table button), pick the sheet and VIEW1, click Create Tables. The status line says the tables were drawn and "Marked N holes on the view."
  2. Every hole on VIEW1 has its tag (A1, B1, ...) up and right of it at 45 degrees, clear of its circles, with no leader line showing.
  3. Every hole, precision ones included, has a centre mark: lines run continuously through the hole and end 2 mm past the outermost visible circle (the seat of a counterbore, spotface or countersink; the thread circle of a tapped hole). Check one of each type, and compare with spike 160 round 7's pictures.
  4. Precision holes show a circle with the top right quadrant filled solid, the same size as the hole, and the tag stands clear of it. Clearance holes have no filled quadrant.
  5. The drawing's Styles list has one `AWB Centre Mark` centre mark style (gap 0.0001 cm, size 0.01, extension and overshoot 0.2 cm) in the drawing, and the sketched symbol list has `AWB_HoleTag` and `AWB_PrecisionMark` once each.
  6. Click Create Tables again: the tags, precision marks and centre marks are replaced, centre marks not doubled. Check awbaddin.log for "could not be marked".
  7. Press Undo once: the tables, tags, precision marks and centre marks all go.
  8. Move VIEW1 by dragging it: the tags, precision marks and centre marks follow.
  9. Change a hole position in the part, return to the drawing and update: everything follows the hole.
  10. Generate one table only (for example Precision): only that table's holes are tagged and marked.
  11. A tapped or counterbored hole on a precision table: the tag stands clear of the larger of the seat and the mark.

## Follow-ups

Things noticed but not done.

- **Task 166, finding the marks again:** every tag, precision mark (both `SketchedSymbol`s on the sheet) and centre mark carries the attribute set `AWB_HoleMark` (`HoleTableMarks.AttributeSetName`) with string attributes `HoleId` (the hole `Id`), `ViewKey` (the task 163 view key) and `Role` (`Tag`, `PrecisionMark`, `CentreMark`). `HoleTableMarker.DeleteExisting` shows the lookup. The marker places marks only; Update must also re-run it for new holes and delete the marks of holes that are gone (it only deletes marks for the ids it is given). `HoleTableMarker.Place` is the entry point.
- **Default: a precision mark follows the table, not the classification.** A hole gets it when it is in the Precision table (`HoleTableMarks.HasPrecisionMark(kind)`), so a swapped hole takes the mark with it. If Aidan wants it tied to the hole's own `IsPrecision`, it is one line in Core. Log it in DECISIONS.md as a Default.
- **Tag text style** is read from a temporary hole table (as the drawer does), so the add-in makes two temporary tables per Create. `HoleTableStyleSet` could carry the tag style and the marker could take it; left alone because the drawer is task 164's file.
- **Existing tag symbol or precision mark definition is reused as it is.** If the drawing's hole tag text style changes later, `AWB_HoleTag` keeps the old text style until it is deleted.
- **Arcs are not used.** A hole whose outermost circle is drawn as an arc (kCircularArcCurve, for example a partly hidden or sectioned circle) has no circle in `HoleTableHoleRef.Curves`, so it is counted as unmarked in the status line. Task 163's reader would need to include arcs.
- **A circle that already has a centre mark** (added by hand or by Inventor) is not looked for; the wizard adds its own beside it. Marks it placed itself are replaced.
- **Per-hole failures are logged and counted, not aborting.** Only a failure in the shared parts (style, symbol definitions) aborts the transaction. Say so if Aidan wants every failure to abort.
- Tags and marks are not put on the table layer; they use the drawing's default layers.
- **Attribute set name** is `AWB_HoleMark`, not `AWB_HoleTable`, which task 166 uses for the table state on the table symbols.
- **Swap leaves a stale mark.** After a swap, regenerating only Precision leaves the old precision mark on the swapped-out hole, because `DeleteExisting` only covers the holes being marked now.
- **A hole that fails halfway keeps its partial marks.**
- **If stamping the attributes fails, the marks double on the next redraw** (they cannot be found again).
- **Tapped hole with an arc thread edge:** a tapped hole whose thread edge is drawn as an arc is marked on the drill circle, or counted as unmarked.
- **Tag gap** uses the tag text height, not the data text height the spike used.
- The "could not be marked" count includes holes missing from the fresh analysis and holes that threw, not only holes with no circle.
