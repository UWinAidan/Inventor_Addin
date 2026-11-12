# 157: Core: XDIM and YDIM from the view's ordinate origin

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Decided behaviour": Origin; open questions 7, 14, 16, 17)
- **Status:** todo
- **Depends on:** 155
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** 156, 160

## Goal

Core turns a hole's position on the sheet into the XDIM and YDIM the table shows, measured from the view's ordinate-dimension origin, and owns the rules for when the wizard must stop instead (no origin, several origins).

## Scope

- `src/InventorAddin.Core/HoleTable/HoleTableOrigin.cs`:
  - `HoleTableOriginResult.From(IReadOnlyList<SheetPoint> origins)` (or similar): the add-in passes every ordinate origin it found on the view, in sheet cm.
    - None: a stop with the message, in plain words, that the view has no ordinate dimension origin and one must be added before the wizard can measure the holes (Aidan, question 7: it never invents one).
    - Several that are not the same point (within 0.001 cm): a stop saying the view has more than one ordinate origin (Default, question 17).
    - One (or several at one point): the origin.
- `src/InventorAddin.Core/HoleTable/HoleTableCoordinates.cs`:
  - `For(HoleTableHole hole, SheetPoint origin, double viewScale, HoleUnits drawingUnits, HoleTableDecimals decimals, bool precisionTable)`: the X and Y in model length, `(sheet - origin) / scale`, along the sheet's own axes (ordinate dimensions measure along the sheet, so a rotated view needs nothing extra), converted to mm or inches and rounded to the table's decimals (task 155's `HoleTableDecimals`). Holes left of or below the origin are negative.
  - The text the cell shows: invariant culture, decimals kept (`0.0`, `12.50`), and `-0.0` shown as `0.0`.
  - Rounding is away from zero at the half (`MidpointRounding.AwayFromZero`), as a drawing reads.
- `HoleTableMessages` (one place for the wizard's user-facing texts; tasks 158 and 159 add to it): the two stop messages above, and "N holes were left out because they are not seen end on in this view" for question 16 (the add-in counts them; Core words it, singular and plural).

Out of scope: reading the origin and positions from Inventor (spike 160, then an add-in task).

## Acceptance criteria

- [ ] No origin and two different origins each give a stop with its message; two origins at the same point give that origin.
- [ ] A view at 1:2 with a hole 5 cm right and 2.5 cm above the origin on the sheet reads XDIM 100.0 and YDIM 50.0 (mm, clearance table); 100.00 and 50.00 in the precision table; inches convert and use question 14's decimals.
- [ ] Negative positions, `-0.0` to `0.0`, and the half rounding are tested.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- A small `SheetPoint` record (X, Y in cm) is enough; check `Models/` for an existing 2D point first (`Point3D` exists; do not overload it).
- View scale is the model-to-sheet factor Inventor reports (`DrawingViewData.Scale`, 0.5 for 1:2).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none expected
- **Inventor API members not confirmed:** none expected
- **Manual checklist for Inventor:** none (Core only)

## Follow-ups

Things noticed but not done.
