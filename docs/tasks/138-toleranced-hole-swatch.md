# 138: Colour swatch on a toleranced hole's Tolerance row

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Toleranced clearance and Simple holes", question 28)
- **Status:** done (built on Windows; not yet run in Inventor)
- **Depends on:** 115 (toleranced Clearance and Simple holes), 118 (the window's final rows)
- **Needs:** windows build
- **Parallel-safe with:** none (same window and view-model as 118)

## Goal

Aidan, 2026-11-07, answering spec 09 question 28: a toleranced Clearance or Simple hole shows the precision colour swatch beside its Tolerance row, matching the colour the hole gets. This overturns task 115's Default of no swatch.

Today a dowel shows a swatch and its fit's name beside Fit (`FitSwatch`, `FitColourText`, `ShowsFitColour` in `AwbHoleViewModel`; the `SideColourSwatch` style in `AwbHoleWindow.xaml`). A toleranced hole is coloured on OK (task 115) but the window does not show the colour.

## Scope

- Core (`AwbHoleViewModel`):
  - A swatch value and its text for the Tolerance row, set only while Toleranced is ticked on a Clearance or Simple hole, else null.
  - The colour is the one the hole's faces get on OK: take it from the same place the writer's colour comes from (the chosen Precision Tol row's fit level, and Custom's colour as task 115 colours it), not a second copy of the rule. If that rule lives outside the view-model, call it.
  - The text names the colour as the dowel's does (for example `Normal fit`).
  - Raise property changes when Toleranced, the Tolerance row, Type, Standard or Units change.
- `src/InventorAddin/UI/AwbHoleWindow.xaml`: the swatch and its text beside the Tolerance dropdown, as Fit's row shows them for a dowel. Keep task 118's heights: the swatch sits on the existing row and adds no row.
- A dowel's Fit row is unchanged.
- Tests for every Core behaviour above.

## Acceptance criteria

- [x] Ticking Toleranced on a Clearance or Simple hole shows a swatch beside Tolerance, in the colour the hole's faces get on OK. (Core tests compare the swatch with `Geometry.FitColour`, the colour the writer applies. Not yet seen in Inventor.)
- [x] Changing the Tolerance row (including Custom) changes the swatch to match the faces' colour.
- [x] Unticked, or on a type that cannot be toleranced, there is no swatch.
- [x] The window is no taller than after task 118. (The swatch sits on the Tolerance dropdown's line and adds no row. Not measured with a render harness; see Verification.)
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes, apart from the known flaky Numbering tests, and the solution builds on Windows.

## Notes for the implementer

- Read task 115's Verification and Defaults for how a toleranced hole is coloured, and `HoleFitOption.SwatchOf`.
- Watch the row's width: Tolerance entries are long (`N7  -0.004 / -0.016 Medium press`). If the swatch and text push the window wider, show the swatch alone and log the choice as a Default.

## Verification

- **Ran (Windows, Inventor 2026 installed, Inventor not running):**
  - `dotnet build src/InventorAddin.Core`: succeeded, 0 errors.
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: succeeded, 0 errors. The 4 warnings were there before (CS8600 in the Create Print, New Part, Save As Copy and Save As Replace commands, each reported twice).
  - `dotnet test tests/InventorAddin.Core.Tests`: 4375 tests, 26 of them new. Two full runs had 6 and 3 failures. All were in the known flaky file-system and timing tests under `Numbering` (`RegisterStoreTests.RealEnvironment_...`, `SaveAsReplacerTests.Replace_NameThatCannotBeSet_...`, `RenamePlanTests.ListFilesToSearch_ASlowListing_...`, `NewPartCreatorTests`, `SaveAsCopyCreatorTests`). Run on their own, all 862 Numbering tests pass. The hole tests (`HoleTolerancedTests`, `AwbHoleTolerancedViewModelTests` and the new `AwbHoleToleranceSwatchViewModelTests`) pass, 104 of 104. The XAML rules test passes.
- **One colour rule.** `HolePrecision` gains `TolerancedRow(spec, workbook)` (the Precision Tol row a toleranced hole takes), `ColourFitIn(spec, workbook)` and `ColourIn(spec, workbook)`. `HoleRules.Build` now finds a toleranced hole's row through `TolerancedRow`. The builder, whose colour the writer applies, and the window's swatch therefore use the same lookup and the same `HolePrecision.ColourFit`. Tests check the swatch against `Geometry.FitColour` for every row, for Custom and for a Simple hole.
- **Width.** The content is fixed at 316 wide (`CompactDialogContent`), so the value column is 204. Widths measured in Segoe UI 13:
  - The longest closed entry, `N7  -0.004 / -0.016`, is 108 wide. The dropdown's padding adds 36.
  - With the swatch and `Normal fit` (58) beside it, the dropdown would be about 116 wide and would clip that entry.
  - With the swatch alone (14, plus 8 before it), the dropdown keeps 182.

  So the swatch is shown alone, and its name is its tooltip (see Defaults). The window cannot get wider, because the content's width is fixed.
- **Height.** The swatch is 14 tall and sits in a two-column grid on the dropdown's line, so the Tolerance row is as tall as the dropdown, as before. No row is added. This was not measured with a render harness, because task 118's harness was not kept.
- **Not compiled (changed under `src/InventorAddin`):** none. `UI/AwbHoleWindow.xaml` and `UI/Theme/Styles.xaml` were built on Windows. Neither has run inside Inventor.
- **Inventor API members not confirmed:** none. No Inventor API member was added or changed.
- **What changed:**
  - `src/InventorAddin.Core/Holes/HolePrecision.cs`: `TolerancedRow`, `ColourFitIn` and `ColourIn`.
  - `src/InventorAddin.Core/Holes/HoleRules.cs`: a toleranced hole's row is found through `HolePrecision.TolerancedRow`, with the same result as before. Two doc `cref`s now name their overload.
  - `src/InventorAddin.Core/Holes/HoleGeometry.cs`: a doc `cref` now names its overload.
  - `src/InventorAddin.Core/ViewModels/AwbHoleViewModel.cs`: `ShowsToleranceColour`, `ToleranceSwatch` and `ToleranceColourText`. They are raised with the other derived properties on every change, including Toleranced, Tolerance, Type, Standard and Units.
  - `src/InventorAddin/UI/AwbHoleWindow.xaml`: the Tolerance dropdown and the swatch share a line. The swatch's tooltip and automation name are its colour's name.
  - `src/InventorAddin/UI/Theme/Styles.xaml`: `TrailingColourSwatch`, with 8 before it and nothing after.
  - Tests: `tests/InventorAddin.Core.Tests/ViewModels/AwbHoleToleranceSwatchViewModelTests.cs` is new (17 tests). Nine tests are added to `tests/InventorAddin.Core.Tests/Holes/HoleTolerancedTests.cs`.
- **Manual checklist for Inventor** (build with Inventor closed; use any part with a sketch holding a point; ideally at 150 percent on a 1080p screen):
  1. Press **AWB Hole**. Choose Type Clearance, Standard AWB Clearance, Seat None and a metric size such as M6. Toleranced is unticked, there is no Tolerance row, and there is no swatch.
  2. Tick **Toleranced**. The Tolerance row opens with the Default row chosen, and a small coloured square sits at the right end of the dropdown's line. Hover over the square: the tooltip names the colour, for example "Normal fit". The dropdown shows its whole entry, not cut off.
  3. Choose a press row, a normal row and a slip row in turn, if the workbook has them. Each time, the square changes to that fit's colour from the workbook's Settings tab (press red, normal blue and slip green in the starter workbook).
  4. Choose the longest entry in the list, for example an N7 row. The closed dropdown shows it in full beside the square.
  5. Choose **Custom**. The square turns to the normal colour at once, before Upper and Lower are typed. Type Upper `0.02` and Lower `0`.
  6. Pick a centre and press **OK**. Every face of the hole takes the colour the square showed. Right-click the hole and choose Edit AWB Hole: the window opens toleranced, with the same square.
  7. Make another toleranced clearance hole on a press row and press OK. The faces are the press colour, matching the square.
  8. Change Type to **Simple**, type a diameter and keep Toleranced ticked. The square stays and follows the row. Change Type to **Tapped**: Toleranced and the square go. Change back to Clearance: Toleranced is unticked and there is no square.
  9. Set Standard to **AWB Dowel**. Fit's row shows its square and name (for example "Normal fit") as before, and the Tolerance row has no square.
  10. With Toleranced ticked, switch **Metric | Imperial**. The square follows the row chosen for the new units.
  11. With Toleranced ticked and the diagram open, check that the window is no taller than before this task. Task 118 measured a toleranced clearance hole through all at 647 at 150 percent. Check that the square is readable in both the light and dark themes.

## Defaults

- **The swatch alone, with its name in the tooltip.** The task asked for the swatch and its text, as Fit's row shows them for a dowel. With the text beside it, the Tolerance dropdown would be about 116 wide in the 316-wide content and would clip long entries such as `N7  -0.004 / -0.016`. So the window shows only the square, at the end of the dropdown's line. Its name (for example `Normal fit`) is its tooltip and its automation name. The view-model still provides `ToleranceColourText`. Aidan can overturn this. The alternatives are a narrower dropdown that clips, or the name on a line of its own, which task 118 rules out.
- **The swatch shows whenever the colour is known**, even while the hole cannot be built yet (a Simple hole with no diameter, or Custom with its limits not typed). It is the colour the faces will take once OK is possible. It is hidden when the tolerance name has no row for the units.

## Follow-ups

- Record the Default above in `DECISIONS.md` and in spec 09 ("Toleranced clearance and Simple holes" and question 28). Add it to `docs/ui-mockups/README.md` too, if that file lists changes to the AWB Hole window.
- Task 118's render harness was not kept, so this task's height claim rests on the layout, not on a measurement. A small kept harness, or a test that builds the window, would let later window tasks measure instead.
