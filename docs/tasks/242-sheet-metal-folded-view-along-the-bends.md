# 242: Sheet metal: the folded view looks along the bend lines; no bend notes

- **Milestone:** M5 (from Aidan's chunk 3 check, 2026-11-26)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** todo
- **Depends on:** 229, 233
- **Needs:** windows build
- **Parallel-safe with:** none (243 touches the same placer)

## Goal

Task 229's folded front view looked straight at the base face and printed a plain rectangle (Aidan's check, steps 8 and 9). That rule was wrong. The folded view looks along the bend lines, so the bend profile shows (a Z-shaped part prints as a Z). Aidan also dropped the bend notes on the flat pattern: he adds bend information himself when a print needs it. The flat pattern view and the iso stay as they are.

## Scope

- **Core** (`src/InventorAddin.Core/Drawing/FaceViewOrientation.cs` or a new file beside it, with tests):
  - New rule for sheet metal on Front view Auto. Input: each bend's axis direction (unit vector, either sign) and length or radius, and the base face's outward normal in the folded model. Output: the same `FaceViewOrientation` (viewing direction and up).
  - Viewing direction: along the bend axis. When the bends are not all parallel, the direction shared by most bends (parallel either sign, within a small angle) wins; a tie goes to the direction with the longest total bend length (Default).
  - Up on the sheet: the base face normal with any part along the viewing direction removed, so the base flange prints horizontal (Default).
  - Which end to look from: the eye on the side of the axis toward +Z, else -Y, else +X (Default; say in the code why). The flat pattern and iso are unchanged.
  - No usable bend (a flat plate, or every bend axis parallel to the base normal): fall back to the old base-face rule (`FaceViewRule.Choose`), so a plate still prints flat.
- **Add-in** (`src/InventorAddin/Drawing/InitialViewsPlacer.cs` and whatever reads the base face today):
  - Read the bends: `SheetMetalComponentDefinition.Bends`, each `Bend.FrontFaces`, and the face's `Geometry` as a `Cylinder` (`AxisVector`, `Radius`). Confirmed by the parent on 2026-11-26 with a COM probe on Aidan's test part: `Bends.Count` 2, `FrontFaces` cylinders with `AxisVector` (0, 0, ±1). Use `ComSafe` per bend.
  - The camera for the folded view (and the scratch measurement) uses the new orientation the same way 229's camera does.
  - Remove the bend notes on the flat pattern and the `UsePartUnits` code with them. No bend notes are added.
- Spec 05: the Place Initial Views line says the sheet metal folded view looks along the bend lines (one short clause).

Out of scope: the iso (task 243), the flat pattern view, explicit Front view choices (they keep 233's behaviour).

## Acceptance criteria

- [ ] On Aidan's Z-shaped test part the folded view shows the Z profile, the base flange horizontal, not upside down.
- [ ] No bend notes are placed; nothing else about the flat pattern view changes.
- [ ] A flat plate (no bends) still prints with the face seen flat.
- [ ] Core tests for: one bend, two parallel bends of either sign, bends in two directions (most wins; tie by length), no bends, an axis parallel to the normal.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Notes for the implementer

- Read `docs/tasks/229-*.md` and `docs/tasks/233-*.md` first; follow their camera and measuring code. Do not touch `FaceViewRule`'s own behaviour beyond using it as the fallback.
- You may check Inventor behaviour with a PowerShell 5.1 late-bound COM script on a copy in `C:\AwbSpike\242\` (for example `C:\AwbSpike\242\sm.ipt`, a copy of the test part). Use `GetActiveObject('Inventor.Application')`; if Inventor is not running, `New-Object -ComObject Inventor.Application` and `Quit()` it at the end. Never open or save files under `C:\CAD_TEST` or the repo's `libraries\Templates`.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
