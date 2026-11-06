# 115: Toleranced Clearance and Simple holes in Inventor and the window

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Toleranced clearance and Simple holes")
- **Status:** done, built on Windows, not yet run in Inventor
- **Depends on:** 108, 113, 114
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

A Clearance or Simple hole can be toleranced in the window (Aidan, 2026-10-24): the tolerance goes on its diameter in Inventor, it takes the precision colour, and Edit reads it back.

## Scope

- `src/InventorAddin/Holes/HoleFeatureWriter.cs`, `HoleFitWriter.cs`, `HoleFeatureReader.cs`: the tolerance on the through diameter of a Clearance or Simple hole (seated too, the seat untouched), from an ISO row, a Limits row or Custom limits, as spike 108 question 5 confirmed: `HoleFitWriter.ApplyTolerance` works unchanged on a seated clearance hole (ISO gives `kLimitsFitsStackedTolerance`, Custom gives `kDeviationTolerance`, the counterbore diameter stays `kDefaultTolerance`) and `HoleFeatureReader.ReadFacts` reads both back, so no new API work is expected; the colour from task 113's rule on every face; unticking removes both (task 096's reset).
- `src/InventorAddin/UI/AwbHoleWindow.xaml(.cs)`: the Toleranced checkbox on one row, the Tolerance row it opens (the dowel's dropdown, reused), and the Custom Upper and Lower boxes.

## Acceptance criteria

- [ ] A toleranced clearance hole (unseated and counterbored) and a toleranced Simple hole show the tolerance in Inventor's own hole dialog and on the diameter parameter, and are coloured on every face.
- [ ] Custom limits reach Inventor as deviations.
- [ ] Unticking Toleranced on an edited hole puts every face back to the part's colour and removes the tolerance; one Undo brings both back.
- [x] The solution builds on Windows; `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

- **Ran (Windows):**
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: succeeded, 0 errors. The 4 warnings (8 lines, each listed twice for the WPF temp project) were already there: CS8600 in the Create Print, New Part, Save As Copy and Save As Replace commands.
  - `dotnet test tests/InventorAddin.Core.Tests`: 4219 passed, 0 failed, 0 skipped. No Core code changed in this task.
- **What changed:**
  - `src/InventorAddin/UI/AwbHoleWindow.xaml`:
    - A Toleranced checkbox on its own row (no label, like a field's box), shown on `ShowsToleranced` (Clearance with AWB Clearance, and Simple), bound to `Toleranced`. It sits between Fit and Tolerance.
    - The Tolerance row already bound `ShowsTolerance`, which task 113 widened, so it now opens for a toleranced hole with no binding change. The list is the dowel's dropdown, reused.
    - Upper and Lower boxes under Tolerance, shown on `ShowsCustomTolerance`, bound to `ToleranceUpperText` and `ToleranceLowerText` with `UpdateSourceTrigger=PropertyChanged` (task 130). The unit follows each box, and `ToleranceUpperProblem` and `ToleranceLowerProblem` show under them. The typed-value record (task 124) is already in the view-model's setters and `Refresh`. Leaving a box or pressing Enter commits it through the existing code-behind handlers, which act on every `TextBox`.
    - The header comment lists the new rows and access keys.
  - `src/InventorAddin/Holes/HoleSession.cs`: OK now uses the precision overload of `HoleSessionRules.ResetsFaceAppearances`, with `HolePrecision.IsPrecision` of the spec the window opened on and of the resolved spec kept. A toleranced hole kept toleranced is recoloured without a reset first. Unticking Toleranced, or changing to a type that cannot be toleranced, still resets every face. `_openedKind` is replaced by `_openedPrecision`.
  - `src/InventorAddin/Holes/HoleFitWriter.cs`: doc comments only. `Finish` already applies the tolerance and colour whenever Core sets `FitColour`, which task 113 does for every precision hole. It removes the tolerance otherwise (`RemoveDowel`, not on a tapped hole).
  - `HoleFeatureWriter.cs` and `HoleFeatureReader.cs`: unchanged. Spike 108 question 5 confirmed that `ApplyTolerance` on a seated clearance hole touches only the hole diameter, and that `ReadFacts` reads the code and deviations back.
- **Not compiled in the cloud:** nothing. Every changed file under `src/InventorAddin` was built on Windows, as part of the solution above. None of it has been run in Inventor.
- **Inventor API members not confirmed:** none. No new member is used. The code relies on `HoleFeature.HoleDiameter`, `Parameter.Tolerance`, `Tolerance.SetToFits`, `SetToDeviation`, `SetToDefault` and `ToleranceType`, already used by `HoleFitWriter`/`HoleFeatureReader` and confirmed on a seated clearance hole by spike 108 question 5. A Simple hole's diameter tolerance relies on the same calls on `HoleDiameter`. The spike did not try a Simple hole, but the calls are the same as for a dowel.
- **Manual checklist for Aidan (Inventor 2026, a part of your own with a sketch of hole centres on a face):**
  1. AWB Hole, Type Clearance, Standard AWB Clearance, Seat None, Size M6. Toleranced is on its own row, unticked, and there is no Tolerance row. Tick it: the Tolerance row opens with every Precision Tol row for the units, the Default = Y row preselected, and Custom last. OK. Inventor's own Edit Hole dialog and the diameter parameter (Parameters, or Show Dimensions) show the ISO tolerance, for example H7. Every face, the bottom included, has the row's fit colour.
  2. Do the same with Seat Counterbore. The tolerance is on the hole diameter only; the counterbore diameter has none. Every face, the counterbore included, is coloured.
  3. Type Simple, Diameter 5, Toleranced, choose Custom. Upper and Lower boxes appear and OK is disabled, with a message under each box. Type Upper `0.02` and Lower `0`. The preview follows about 300 ms after typing. Type Upper `-1` and check that the box shows the Upper-above-Lower message and OK waits. Put `0.02` back. OK. Inventor's hole dialog shows a deviation of +0.02 / 0, and the faces take the normal fit colour.
  4. Right-click the hole from step 1, then Edit AWB Hole. The window opens toleranced, on the same row. OK without changes: the tolerance stays and every face is still coloured.
  5. Edit it again, untick Toleranced, OK. Every face is back to the part's colour, and Inventor's dialog shows no tolerance. One Undo brings the tolerance and the colour back.
  6. Edit the Custom Simple hole from step 3. It opens Custom, with +0.02 and 0 in the boxes.
  7. A dowel (Standard AWB Dowel) with Custom chosen now shows the Upper and Lower boxes. With limits typed, OK becomes enabled.
  8. Edit a Clearance hole made with Inventor's own Hole command that has a tolerance on its diameter (task 113's follow-up). The window opens toleranced. OK keeps the tolerance and colours every face.
  9. Switch Units with Custom limits typed. The limits show converted (4 decimals of a mm or 5 of an inch).

## Defaults

These choices were made to keep the work moving. Aidan can overturn any of them.

- **The Toleranced checkbox has no field label.** It sits on its own row, in line with the other boxes, between Fit and Tolerance. The spec says "on one row".
- **Access keys:** N for Toleranced and W for Lower. Upper has no access key, because U, p, e and r are all taken.
- **No colour swatch for a toleranced hole.** The swatch beside Fit stays dowel-only. See Follow-ups.

## Follow-ups

- **Fit colour swatch for toleranced holes.** `ShowsFitColour`, `FitSwatch` and `FitColourText` are dowel-only, so a toleranced hole shows no swatch for the colour it will take. Aidan to decide whether the Tolerance row should show one; the spec does not say.
- **A Simple hole's diameter tolerance is untried in Inventor.** Spike 108 question 5 covered a counterbored clearance hole only. Checklist step 3 covers it.
