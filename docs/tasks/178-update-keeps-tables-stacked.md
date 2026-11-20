# 178: Fix: Update keeps the precision table stacked on the clearance table

- **Milestone:** M4 part 2 (fix from Aidan's check of 174 to 176, 2026-11-20)
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Decided behaviour": Update, Align)
- **Status:** todo
- **Depends on:** 164, 166
- **Needs:** windows (Core and add-in)
- **Parallel-safe with:** 177, 180

## Goal

After Update the tables overlapped (Aidan's screenshot `ADDIN_PICS/spike160/update-overlap.png`, local only): the clearance table grew upward from the title block corner and the precision table, which sat on top of it, stayed where it was. Aidan (2026-11-20): if the two tables were stacked before Update, keep them stacked after it.

Each table is a sketched symbol placed by its bottom-right corner (`HoleTableAlign.Positions`), and a redrawn definition grows upward. So when the clearance table changes height, a precision table that sat on it must move by the change.

## Scope

- `src/InventorAddin.Core/HoleTable/HoleTableLayout.cs` (`HoleTableAlign`): a new pure method that decides where the precision table goes after a redraw that did not align. Inputs: the anchor, the clearance table's position and height before (null when it did not exist), the precision table's position before (null when it did not exist), and the clearance table's position and height after (null when it was deleted). Output: the precision table's new position, or null to leave it where it is. Rules:
  - The precision table **was stacked** when its position is within 0.01 cm (`StackToleranceCm`) of the point straight above the clearance table: (clearance X, clearance Y + clearance height before). With no clearance table before, it was stacked when it sat on the anchor (the lone-table spot).
  - Stacked, and the clearance table exists after: move it to (clearance X after, clearance Y after + clearance height after).
  - Stacked, and the clearance table was deleted: move it to where the clearance table was (or the anchor when there was none).
  - Not stacked (the user moved it): null, leave it.
  - Return null when the new position equals the old one within the tolerance, so nothing is written when nothing changed.
- `src/InventorAddin/HoleTable/HoleTableDrawer.cs`:
  - `Draw` with `align: false`: before drawing, read each existing symbol's position (`SketchedSymbol.Position`) and its definition's height (`ReadHeight`). After drawing and placing, call the new Core method and set the precision symbol's `Position` when it returns a point.
  - A precision table that is new on this call, while a clearance table already exists: place it on top of the clearance table where it actually is (its position after, plus its height after), not at the anchor stack. A new clearance table still goes on the anchor.
  - `DeleteTable` is called by Update (task 166) before or after `Draw`; check the call order in `HoleTableSource.UpdateTables` so the deleted-clearance case reads the position before the symbol is deleted. If the order makes that awkward, handle it in `UpdateTables` (read before, move after) and say so in Verification.
  - `align: true` (Create Tables) and `Align` are unchanged.
- `src/InventorAddin/HoleTable/HoleTableSource.cs`: only if the delete order needs it (above).
- Tests in `tests/InventorAddin.Core.Tests` for the new method.
- `docs/features/04-hole-table-wizard.md`: under Update in "Decided behaviour": tables that were stacked before Update stay stacked after it; a table the user moved stays where it is (Aidan, 2026-11-20). Add the 0.01 cm tolerance and the deleted-clearance case as a numbered default under "Open questions".

Out of scope: moving the clearance table when the precision table changes (it grows upward, away from the clearance table); tables of other views.

## Acceptance criteria

- [ ] Core: stacked before, clearance grows by 2 cm: precision moves up by 2 cm. Shrinks: moves down.
- [ ] Core: the clearance table moved by the user, precision still on top of it: precision follows the clearance table's own position, not the anchor.
- [ ] Core: precision moved away by the user (more than 0.01 cm off the stack point): null.
- [ ] Core: clearance deleted, precision was stacked: precision goes to the clearance table's old spot.
- [ ] Core: no clearance table before, precision on the anchor, a clearance table is new: precision goes above it.
- [ ] Core: nothing changed in height: null.
- [ ] Add-in: only the members named here are used, each checked against existing use in `HoleTableDrawer` or by reflection on the Inventor 2026 interop.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Notes for the implementer

- `HoleTableAlign.Positions` and `SheetPoint` are in `HoleTableLayout.cs`. `ReadAnchor`, `ReadHeight`, `FindSymbol`, `FindDefinition` and `PlaceSymbol` are in the drawer. `SketchedSymbol.Position` is set in `PlaceSymbol` and `Align`; reading it returns a `Point2d` (`.X`, `.Y` in cm).
- Read heights before `DrawDefinition` replaces the definition's content; after, `drawing.HeightCm` is the new height.
- One Update of a view is one transaction (task 176), so the move is undone with the redraw.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Drawing with a clearance and a precision table stacked on the title block. Add two clearance holes in the part, save, Update: the clearance table grows and the precision table moves up with it; no overlap.
  2. Remove clearance holes so the table shrinks, Update: the precision table moves down and still sits on it.
  3. Drag the precision table elsewhere, add a clearance hole, Update: the precision table stays where you put it.
  4. Undo after Update: both tables go back.

## Follow-ups

Things noticed but not done.
