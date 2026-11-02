# 113: Core: toleranced Clearance and Simple holes, Custom tolerance

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Toleranced clearance and Simple holes", "Dowel and precision holes"); `docs/features/04-hole-table-wizard.md` (question 1)
- **Status:** done
- **Depends on:** 112
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** 110, 119

## Goal

Core can describe a toleranced Clearance or Simple hole and a Custom tolerance (Aidan, 2026-10-24), and says which holes are precision, for the colour now and the hole table later. The add-in side is task 115.

## Scope

- `HoleSpec.cs`, `HoleGeometry.cs` (`HoleTolerance`): a Toleranced flag on Clearance (AWB Clearance) and Simple holes; a Custom tolerance with Upper and Lower in the window's units (numbers only, Upper above Lower; Default).
- The list (Default, question 17): for a toleranced Clearance or Simple hole every Precision Tol row for the units, the Default = Y row preselected, then Custom; for a dowel the rows of its fit, then Custom (Default, question 18). Each entry's text at the hole's modelled diameter (the Clearance size at its fit, or the Simple diameter), as task 098 does for dowels.
- One Core rule for "is precision": a dowel, or a toleranced Clearance or Simple hole (Aidan); a hole with an Inventor tolerance on its diameter and no AWB data counts too (Default, spec 04 question 1). Used for the colour here and by the hole table in M4 part 2.
- The colour (Default, question 17): the chosen Precision Tol row's fit colour; Custom takes the normal colour.
- Unticking Toleranced, or changing the hole to a type that cannot be toleranced, removes the tolerance and the colour, as for a dowel (spec 09, spike round 5); task 096's every-face reset applies.
- `HoleStoredData.cs`, `HoleReadBack.cs`, `HoleReapply.cs`: the toleranced state, the row or the Custom limits.
- `ViewModels/AwbHoleViewModel.cs`: the Toleranced checkbox (one row, off by default, Clearance and Simple only), the Tolerance row it opens, the Custom Upper and Lower boxes.
- `HoleDiagram.cs`: the tolerance on the diameter label where the mockup shows one for dowels.
- Tests for all of it.

## Acceptance criteria

- [x] Toleranced is offered on Clearance and Simple only, off by default; ticked, the list and Custom appear as above.
- [x] Custom refuses Upper not above Lower and non-numbers, with a message, and OK waits.
- [x] The precision rule and the colour choice are Core functions with tests.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

- **Ran (Windows):**
  - `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings.
  - `dotnet test tests/InventorAddin.Core.Tests`: 4110 passed, 0 failed. Before the task: 4032 passed. 78 tests were added; 7 existing tests were updated for the version bump and for Custom at the end of the dowel list.
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: succeeded, 0 errors. The 4 warnings were already there (CS8600 in the Create Print, New Part, Save As Copy and Save As Replace commands).
- **What was built, file by file:**
  - `Holes/HolePrecision.cs` (new):
    - `CanBeToleranced(kind)`: Clearance and Simple only.
    - `IsPrecision(spec)`: a dowel, or a toleranced Clearance or Simple hole.
    - `IsPrecision(awbSpec, inventorToleranceOnDiameter)`: the hole table's rule. AWB data decides when there is some; with none, a tolerance on the diameter counts (spec 04 question 1).
    - `ColourFit` and `Colour`: a dowel's own fit; a toleranced hole's row fit; Normal for Custom on a toleranced hole.
    - `CustomName`.
  - `Holes/HoleSpec.cs`:
    - New init-only properties: `Toleranced`, `CustomTolerance`, `CustomUpper`, `CustomLower`, and `IsPrecision`.
    - `Normalised` drops `Toleranced` on other kinds and keeps `Tolerance` for every precision hole. It keeps `PrecisionFit` for a toleranced hole's row (it is the row's fit) and drops the row name for Custom.
  - `Holes/HoleRules.cs`:
    - `HoleField.ToleranceUpper` and `ToleranceLower`.
    - `FindAnyTolerance(workbook, units, name, fit?)`: any fit for the units. A name prefers the given fit's row when two rows share it; no name takes the first Default = Y row, else the first row.
    - The dowel's tolerance code is now a shared `ResolveTolerance` that also runs for a toleranced hole, on its modelled diameter (the through hole's, never the seat's). The resolved `PrecisionFit` is the row's fit.
    - `ResolveCustomTolerance`: both limits are required; Upper must be above Lower. `CustomUpperMissingText` and `CustomLowerMissingText`.
  - `Holes/HoleGeometry.cs`: doc comments only. `Tolerance` and `FitColour` are now set for every precision hole.
  - `Holes/HoleStoredData.cs`:
    - Version 4. New attributes: `Toleranced`, `CustomTolerance`, `CustomUpper` and `CustomLower`.
    - `PrecisionFit` and `Tolerance` are written for every precision hole.
    - Versions 1 to 3 still read; a `Toleranced` flag in version 3 data is ignored (tested).
  - `Holes/HoleReadBack.cs` (Inventor facts only; stored data reads as before):
    - A Clearance or Simple hole with a tolerance on its diameter opens toleranced, at the row of its units (any fit) whose code or limits match.
    - Deviations no row has, with no ISO code, open as Custom, with a note.
    - Otherwise it opens on the default row, with a note.
    - A dowel whose deviations no row has now opens as Custom too, instead of the default row.
  - `Holes/HoleSessionRules.cs`: a `ResetsFaceAppearances(edit, openedPrecision, openedFitColoured, keptPrecision)` overload. The kind overload now calls it.
  - `Holes/HoleReapply.cs`: unchanged. The applied state already compares the tolerance and the colour; tests cover a toleranced hole.
  - `Holes/HoleDiagram.cs`: doc comment only. The label already shows any `Tolerance` given.
  - `ViewModels/AwbHoleViewModel.cs`:
    - New properties: `ShowsToleranced`, `Toleranced`, `ShowsCustomTolerance`, `ToleranceUpper`/`ToleranceLower`, `ToleranceUpperText`/`ToleranceLowerText` and `ToleranceUpperProblem`/`ToleranceLowerProblem`. `ShowsTolerance` now includes toleranced holes.
    - `HoleToleranceOption.CustomValue`, `CustomText` and `Custom`.
    - The list shows each entry's tolerance at the modelled diameter (`ToleranceNominal`).
    - The Custom boxes use task 124's typed-value record (`MarkTyped` in the setters, `SettleBox` in `Refresh`).
    - The Diameter block in `Refresh` moved above Tolerance, so a Simple hole's list uses the current diameter.
    - The diagram shows a toleranced hole's code or limits, or the Custom limits.
- **Tests added:**
  - `tests/InventorAddin.Core.Tests/Holes/HoleTolerancedTests.cs`: precision rule, colour, `Normalised`, rules (rows of every fit, seated through hole, Simple diameter, Custom checks, inches), `FindAnyTolerance`, stored data round trips and refusals, version 3, read-back from Inventor facts, re-apply offer, face reset, diagram summary.
  - `tests/InventorAddin.Core.Tests/ViewModels/AwbHoleTolerancedViewModelTests.cs`: checkbox per type, list and default, text at the diameter, colour, Custom boxes (empty, not a number, Upper not above Lower, valid), units switch, kept across Custom and back and Clearance to Simple, untick, type change, dowel Custom, edit, one preview.
- **Not compiled (changed under `src/InventorAddin`):** none. Nothing under `src/InventorAddin` changed, and the whole solution was built on Windows against the changed Core.
- **Inventor API members not confirmed:** none are used. This task writes no Inventor code.
- **Before task 115:** the window does not bind `Toleranced` or the Custom boxes yet, so nothing toleranced can be made from the window.
  - Editing an Inventor-made Clearance or Simple hole that has a tolerance now opens it toleranced. OK then keeps the tolerance and colours the hole through the existing `HoleFitWriter.Finish`. Before this task, OK dropped the tolerance.
  - A Custom tolerance on a dowel is in the dowel's list now. The window shows Custom, but its Upper and Lower boxes are not bound, so choosing it leaves OK disabled until task 115.
- **Manual checklist:** none for this task, since no add-in code changed. Task 115's checklist covers the window and Inventor.

## Defaults

These choices were made to keep the work moving. Aidan can overturn any of them.

- **The preselected row for a toleranced hole is the first Default = Y row for the units in workbook order**, whatever its fit. The tab has one Default per fit, so there are up to three.
- **A toleranced hole stores its row's fit** (`PrecisionFit`), and the rules resolve it from the row. The colour comes from it, and it picks between two rows of the units that share a name. Without it, the first row of that name wins. Custom on a toleranced hole stores Normal.
- **The Custom entry's value is `<Custom>`**, shown as "Custom", last in the list. A Precision Tol row named `<Custom>` is left out of the list.
- **Custom limits are relative to the modelled diameter, in the window's units, numbers only and signed as typed.** Both are required. Upper must be strictly above Lower; equal limits are refused. Nothing else is checked, for example a lower limit larger than the hole.
- **The Custom boxes start empty**; no last values are remembered. A typed value is kept across units, size, type and tolerance changes (task 124). On a units switch it shows converted, to 4 decimals of a mm or 5 of an inch, instead of the 3 or 4 decimals other numbers-only boxes use, so small limits survive.
- **Changing to a kind that cannot be toleranced unticks Toleranced, and changing back leaves it unticked**, as the spec says for a dowel. Clearance to Simple keeps it ticked, along with the row chosen.
- **A dowel with Custom keeps its own fit's colour** (question 18 says only that Custom is in the dowel list).
- **Stored data is version 4**, so an add-in from before refuses toleranced data instead of reading it as an untoleranced hole. Versions 1 to 3 read as before.
- **Read-back from Inventor:**
  - Deviations with no ISO code, that no row has, read as Custom, on a dowel too (before, a dowel took its default row).
  - An ISO code no row has takes the default row, with a note.
  - Deviations where Upper is not above Lower also take the default row.
- **A toleranced hole on a size whose ISO limits cannot be worked out** reports the ISO message under Tolerance, as a dowel does.

## Follow-ups

- **Task 115, window bindings.** Bind `ShowsToleranced`, `Toleranced`, `ShowsCustomTolerance`, `ToleranceUpperText`, `ToleranceLowerText`, `ToleranceUpperProblem` and `ToleranceLowerProblem`. Change the Tolerance row's visibility binding to the widened `ShowsTolerance`. The Custom entry's `Text` is "Custom" and its `Detail` is null.
- **Task 115, OK.** `HoleSession` can call the new precision overload of `HoleSessionRules.ResetsFaceAppearances` with `HolePrecision.IsPrecision` of the opened and kept specs. `HoleFitWriter.Finish` already applies the tolerance and colour whenever `FitColour` is set, and names the appearance by `build.Resolved.PrecisionFit`, which is now the row's fit. So the existing path works; only the reset decision is coarser (it resets on keeping a toleranced Clearance hole too, then recolours).
- **The fit colour swatch.** `ShowsFitColour`, `FitSwatch` and `FitColourText` are still dowel-only. A toleranced hole shows no swatch for its colour. Decide in task 115 whether the Tolerance row should show one.
- **Task 132.** `FindAnyTolerance` takes the first Default = Y row of the units for a toleranced hole. Once every fit has several levels, check this is still the row Aidan expects preselected.
- **Hole table (M4 part 2).** Use `HolePrecision.IsPrecision(awbSpec, inventorToleranceOnDiameter)` to sort holes.
