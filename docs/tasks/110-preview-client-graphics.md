# 110: The AWB Hole preview drawn with client graphics, no face paint

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window": live 3D preview; question 13)
- **Status:** todo (completed from spike 107's results)
- **Depends on:** 107
- **Needs:** windows build
- **Parallel-safe with:** 111, 112, 113, 119

## Goal

The preview looks like Inventor's own Hole preview (Aidan, 2026-10-24): a faint, see-through red copy of the hole volume drawn over the model with client graphics, end caps included, so a through hole looks red seen straight down the bore. Task 101's face paint is removed.

## Scope

- Remove task 101's preview paint: the paint transaction in `HoleSession` (`_paint`, `ClearPaint`), `HolePreviewWriter`'s face painting, and Core's `HolePreviewAppearance` where nothing else uses it. This also closes the `TODO.md` follow-ups "Preview red (task 101 review)" (a failed clear leaving the paint transaction open; "translucent" comments), which are folded in here.
- Draw the preview as spike 107 recommends: the volume source, the node layout, the colour (RGB 220, 40, 40, the 2026-10-18 Default) and the opacity Aidan chose, redrawn after each successful change and removed on OK, Cancel, a refused change that keeps the last good hole (the graphics follow the last good hole), and when the session ends any other way.
- Any geometry the graphics need (for example the profile of the hole volume per centre) is worked out in Core from `HoleGeometry`, with tests; the add-in only turns it into Inventor objects.
- If task 105 is done first, the graphics also draw in a part edited in place, as spike 103 question 8 found; otherwise 105 picks this up.

Out of scope:

- The fit colours on OK and task 096's reset: unchanged.

## Acceptance criteria

- To be completed from spike 107. At least:
- [ ] No face of the part is painted while the window is open; nothing of the preview is left in the part after OK, Cancel, Undo or closing the part.
- [ ] A through hole looks red seen down the bore; blind holes, drill points and every seat are drawn.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Verification

Filled in by the implementer.

## Follow-ups
