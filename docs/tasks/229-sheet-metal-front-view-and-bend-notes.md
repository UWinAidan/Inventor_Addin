# 229: Sheet metal: front view on the base face, and bend notes

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** done (built on Windows; not run in Inventor)
- **Depends on:** 225, 228
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

On sheet metal the folded front view shows the part edge-on. The front view looks straight at the flat pattern's base face, longest edge horizontal; the other views project from it. The flat pattern view gets bend notes (direction, angle, radius) if it has none.

## Scope

- Core: the rule (base face normal, longest edge horizontal) as data the adapter applies.
- `InitialViewsPlacer`: base view placed with spike 225's orientation call; projected views from it.
- Bend notes on the flat pattern view as spike 225 found.

## Acceptance criteria

- [ ] Aidan's sheet metal test part prints with the base face seen flat in the front view, longest edge horizontal, and bend notes on the flat pattern.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 5372 passed, 0 failed (8 new, `FaceViewRuleTests`).
- **Not compiled (changed under `src/InventorAddin`):** nothing; `Drawing/InitialViewsPlacer.cs` built on Windows. It has not been run in Inventor.
- **Inventor API members not confirmed:** all compile. Not run: `BendNote.UsePartUnits = false` (the effect on an imperial print is a guess; it compiled, and is set inside `ComSafe.Run`), `BendNotes.Add(curve)` with the optional argument left out (spike used Missing), `AddBaseView` with `""` for SourceFileName and the camera (spike used Missing). `FlatPattern.BaseFace`, `IsParamReversed`, `GeometryType`, `CreateCamera` and `EdgeType` are as spike 225 ran them.
- **Behaviour:** Core `FaceViewRule.Choose` takes the outward normal and the face's straight edges, takes the longest, flattens it into the face plane, and sets up = normal x edge, flipped so it points toward +Z, else +Y, else +X (the spike's sign follow-up). The placer builds a camera from it (Target = range box centre, Eye = Target + normal x 100) for the folded front view; if anything fails it logs a warning and uses the standard front view. The scratch measuring uses the same camera, so the layout uses the real size. The flat pattern view gets a bend note on each `kBendUpEdge`/`kBendDownEdge` curve, only when the sheet has no bend notes.
- **Manual checklist for Inventor:**
  1. Open Aidan's sheet metal test part, choose Place Initial Views (or Create Print) on a new drawing.
  2. The folded view shows the base face flat (not edge-on), the longest edge of that face horizontal, and is not upside down.
  3. The flat pattern view shows notes like "UP 90.00 deg R3.18" on every bend centreline.
  4. Do it again on an imperial print: the bend radius should be in inches, not mm. If it is still mm, see the follow-up.
  5. Check a part with no flat pattern or an odd base face still places a normal front view (the log says why).
  6. Ctrl+Z reverts the whole placement.

## Follow-ups

Things noticed but not done.

- Whether `UsePartUnits = false` fixes the imperial bend radius is unverified; if not, set the units through the bend note style.
- The folded view's plan estimate still uses the range box (width x height); only the measured size is exact. It is overridden by the measurement, so only a failed measurement is affected.
- Bend notes are placed where Inventor puts them by default; overlap with other flat pattern geometry is not handled.
