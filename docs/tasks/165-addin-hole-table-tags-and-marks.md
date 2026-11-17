# 165: Add-in: hole tags, precision marks and the AWB Centre Mark on the view

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Target output": on the view; "Design decision, settled by spike 160")
- **Status:** todo
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
- Attach an attribute set (`AWB_HoleTable`, the hole `Id`) to every tag, mark and centre mark, so tasks 166 and Update can find and replace them. The geometry rules (tag offset, 45 degrees) go in Core with tests.
- Same transaction as the tables (task 164).

## Acceptance criteria

- [ ] Builds on Windows. The Core rules have tests.
- [ ] The manual checklist below passes.

## Notes for the implementer

- Spike 160 "Recommendations" and rounds 3 to 7. The proved script is `spikes/160/spike160-round7.ps1` on branch `spike/160-hole-table`, section "4. tags and precision marks".
- `kThreadEdge` is 82689, and `kCircleCurve` is 5124.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Every hole on VIEW1 has its tag up-right, clear of its circles.
  2. Every centre mark runs through the hole and ends 2 mm past the outermost circle (compare round 7's pictures), precision holes included.
  3. Precision holes show the filled quadrant.
  4. Move the view: everything follows.
  5. Move a hole in the part and update: everything follows.

## Follow-ups

Things noticed but not done.
