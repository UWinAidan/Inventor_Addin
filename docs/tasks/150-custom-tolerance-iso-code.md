# 150: Custom tolerance takes a typed ISO 286 code

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Tolerance")
- **Status:** done
- **Depends on:** 113, 115, 132, 149
- **Needs:** Core, then windows build
- **Parallel-safe with:** 140 to 143, 148 (not 144 to 147 or 149: same window files)

## Goal

Aidan's answer (2026-11-09) to a first-look item: a Custom tolerance can be given as a typed ISO 286 code, for example H8 or k6, **as well as** the Upper and Lower limits it takes today. This works on dowels and on toleranced Clearance and Simple holes, wherever Custom is offered.

## Scope

- **Core.** `src/InventorAddin.Core/ViewModels/AwbHoleViewModel.cs`, `src/InventorAddin.Core/Holes/HoleSpec.cs` (beside `CustomUpper` and `CustomLower`), the stored data (`HoleStoredDataStore` or its Core codec; a new field needs a version step that older data still reads), and `Iso286`, plus tests:
  - Custom gets a way to enter a code. A suggested layout: a Code box before Upper and Lower. A valid code works out the limits at the chosen size through `Iso286`, as ISO rows do, and shows them in Upper and Lower, read-only. With no code, Upper and Lower are typed as today. Pick the clearest layout that fits the row, and log it as a Default.
  - An invalid code, or one `Iso286` has no limits for at this size, gives a message under the box, and OK is disabled. This follows the window's other typed boxes (task 124's typed records, task 130's live typing).
  - The code is remembered and stored with the hole, so an edit opens it as typed.
- **Add-in.** `src/InventorAddin/Holes/HoleFeatureWriter.cs` and `HoleFeatureReader.cs`: a Custom code is applied as ISO rows are, with Inventor's own fit tolerance. Read it back that way. An Inventor hole with a fit tolerance whose code has no row then opens as Custom with that code. This answers task 113's follow-up "a hole with an ISO code no row has", and this task removes that line from the follow-ups.
- **Colour.** A Custom code hole takes the same colour as Custom limits today (spec 09 question 17 Default).
- `docs/features/09-awb-hole.md`: the Tolerance row and the Toleranced section.

Out of scope:

- Workbook rows and their colours.

## Acceptance criteria

- [x] Core tests: valid codes in either case (H7 and h7 mean different things, so case is kept as typed), invalid codes, a code with no limits at a size, a size change working out the limits again, stored data round-trip, and old stored data still read.
- [ ] Inventor shows the code's fit tolerance on the hole diameter, and an edit opens with the code.
- [x] Built on Windows, 0 errors; Core tests pass.

## Notes for the implementer

Read tasks 113, 115 and 132 first, and spec 09 questions 17 and 18. Task 088 sets an ISO row with Inventor's fit tolerance; use the same call.

## Verification

- **Ran (Windows, Aidan's PC):**
  - `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings.
  - `dotnet test tests/InventorAddin.Core.Tests`: 4542 passed, 0 failed, 0 skipped. The first full run had 1 failure, `RenamePlanTests.ListFilesToSearch_AFolderThatDoesNotAnswer_IsSkippedAfterTheProbeTimeout`, the known timing test. It passed alone (198 of 198 in `RenamePlanTests`) and in a second full run (4542 of 4542). No other test failed.
  - Inventor was not running, so `dotnet build InventorAddin.slnx` ran with the deploy step: 0 errors, 8 warnings. The warnings are the existing CS8600 in the Create Print, New Part, Save As Copy and Save As Replace commands, each listed twice. Deployed to `%APPDATA%\Autodesk\Inventor 2026\Addins`.
- **What changed:**
  - `Holes/Iso286.cs`:
    - `ShaftLimits` covers f, g, h, js, k, m, n and p at IT5 to IT11. It mirrors the hole tables: es of f and g is minus EI of F and G; ei of k, m, n and p is minus ES of K, M, N and P before delta; k takes its value only up to IT7.
    - `Limits` sends capitals to `HoleLimits` and lowercase to `ShaftLimits`, and refuses mixed case such as Js7.
    - Also new: `IsShaftCode` and `ShaftLetters`.
    - `HoleLimits` is unchanged, so workbook rows still take hole classes only.
  - `Holes/HoleSpec.cs`: `CustomCode` and `HasCustomCode`. `Normalised`:
    - trims the code and keeps its case;
    - makes a blank code null;
    - drops `CustomUpper` and `CustomLower` when there is a code;
    - drops the code when Custom is not chosen.
  - `Holes/HoleRules.cs`:
    - New `HoleField.ToleranceCode` and `CustomCodeLimits(nominal, units, code)`.
    - A Custom code is resolved at the modelled diameter: the dowel's size, the Clearance size at its fit, or the Simple diameter. The result is a `HoleTolerance` named Custom that carries the code as `IsoCode`.
    - The colour is Custom's: normal blue on a toleranced hole, the dowel's own fit on a dowel.
    - A code that is not one, or has no limits at the size, is a problem under the Code box. Typed limits are not checked while there is a code.
  - `Holes/HoleGeometry.cs`: `HoleTolerance.IsShaftCode`, and doc comments.
  - `Holes/HoleStoredData.cs`:
    - **Version 6.** A Custom code is stored as a `CustomCode` text attribute, case kept, instead of `CustomUpper` and `CustomLower`.
    - Versions 1 to 5 read as before (tested at 4 and 5).
    - A `CustomCode` attribute in data marked version 5 or older is ignored. Such data then lacks its limits and is refused rather than misread.
    - An add-in from before this task refuses version 6 data with its "version 6" message.
    - The re-apply comparison now matches ISO codes case-sensitively, so h7 is not H7.
  - `Holes/HoleReadBack.cs`:
    - An Inventor hole with no AWB data, whose fit code no Precision Tol row has, opens as Custom with that code. This applies to a dowel and to a toleranced Clearance or Simple hole, with the existing "shown as a Custom tolerance" note. Before this task it took the default row.
    - Row matching by code is now case-sensitive.
  - `ViewModels/AwbHoleViewModel.cs`:
    - New: `ToleranceCode`, `ToleranceCodeText`, `HasToleranceCode` and `ToleranceCodeProblem`. `ToleranceCodeText` types live, as in task 130: the code applies about 300 ms after the last key, and a cleared box applies on commit.
    - While a code is given, `ToleranceUpperText` and `ToleranceLowerText` show its limits at the size, in the window's units (inches to 6 decimals). They refuse typing, and their own problems are hidden.
    - Typed limits are kept behind the code and come back when it is cleared.
    - The code is kept across size, units, type and tolerance changes. Edit opens with the stored code, and the diagram shows it.
    - The live-typing draft now holds an apply action (`SetRawText`), so a text box does not have to hold a number.
  - `src/InventorAddin/Holes/HoleFitWriter.cs`. The task named `HoleFeatureWriter.cs`, but the tolerance is applied in `HoleFitWriter.ApplyTolerance`; `HoleFeatureWriter` does not touch tolerances.
    - A hole class goes on as before: `SetToFits(kLimitsFitsStackedTolerance, code, "")`, the same call task 088 uses for ISO rows.
    - A shaft class goes on as `SetToFits(kLimitsFitsStackedTolerance, "", code)`. If Inventor throws, its limits go on with `SetToDeviation` and a warning is logged, so the hole keeps the tolerance the window showed.
    - `ApplyTolerance` takes an optional `ILog`.
  - `src/InventorAddin/Holes/HoleFeatureReader.cs`: for a fit, reads `Tolerance.HoleTolerance`, then `Tolerance.ShaftTolerance` when that is blank.
  - `src/InventorAddin/UI/AwbHoleWindow.xaml`: a Code row before Upper and Lower (see the layout Default below). Upper and Lower use the new `WorkedOutTextBox` style and bind `IsReadOnly` to `HasToleranceCode`.
  - `src/InventorAddin/UI/Theme/Styles.xaml`: `WorkedOutTextBox`, based on `DepthTextBox`. When read-only it shows muted text and the disabled border, and it is not a tab stop, so Tab and task 147's arrow keys pass over it.
  - `docs/features/09-awb-hole.md`: the Tolerance row; "Dowel and precision holes" (Custom, and what Inventor gets); "Toleranced clearance and Simple holes" (Custom and the stored data).
- **Tests:** 59 new tests in two files, plus updates to seven existing files.
  - `tests/InventorAddin.Core.Tests/Holes/HoleCustomCodeTests.cs`:
    - shaft limits checked against ISO 286-2 values;
    - H7 against h7;
    - invalid codes, and codes with no limits at a size;
    - builds on a toleranced Clearance hole, a Simple hole and a dowel, with their colours;
    - typed limits ignored while there is a code;
    - a size change, and inches;
    - `Normalised`;
    - stored data round trip in either case, version 6, versions 4 and 5 still read, and a code in version 5 data refused;
    - read-back from Inventor facts and from stored data;
    - case-sensitive comparison.
  - `tests/InventorAddin.Core.Tests/ViewModels/AwbHoleCustomCodeViewModelTests.cs`:
    - limits shown and read-only, and following a size change;
    - k6 on a Simple hole;
    - Z9x and K9 refused, with OK disabled;
    - clearing the code brings the typed limits back;
    - the code kept when Custom is left and chosen again;
    - inches, edit, and live typing.
  - Updated for version 6 and the new read-back:
    - `HoleStoredDataTests`, `HoleDepthReferenceTests`, `HoleFullDepthTests` and `HoleClearanceSeatWithoutRowTests`: version 6.
    - `HoleReadBackTests`: an ISO code no row has, on a dowel, now opens as Custom K6.
    - `HoleTolerancedTests`: the same on a clearance hole.
    - `HoleStateComparisonTests`: codes are now compared in their case.
- **Not compiled (changed under `src/InventorAddin`):** none. Every changed file under `src/InventorAddin` was built on Windows with the solution above. None of it has been run in Inventor.
- **Inventor API members not confirmed:**
  - `Tolerance.ShaftTolerance` compiles against the Inventor 2026 interop, but nothing has read it in Inventor yet.
  - `Tolerance.SetToFits` with an empty hole tolerance and a shaft class (`"", "k6"`) on a hole diameter has not been tried. It is unknown whether Inventor accepts it and how it shows it on the diameter; the fallback above covers a refusal. The hole-class call (`"H8", ""`) is the one task 088 confirmed.
  - Inventor accepting every code `Iso286` covers (for example H8, K6, JS7) is confirmed only for the codes tasks 088 and 132 tried.
- **Manual checklist for Inventor:**
  1. A 6 mm dowel, Tolerance Custom, code H8: the limits show for 6 mm; OK. Inventor's hole shows H8 on the diameter.
  2. Change the size to 8 mm: the limits follow.
  3. Type `Z9x`: a message under the box, and OK is disabled. Clear the code and type Upper and Lower: it works as before.
  4. A toleranced Simple hole, Custom k6: as step 1.
  5. Edit each hole: it opens on Custom with the code, and **no** re-apply offer is shown (an offer means Inventor reports the code back differently, for example another class or another case; note what `awbaddin.log` says).
  5a. If the workbook has a JS row: edit a hole made with it before this task. No re-apply offer, and it opens on its row (codes are now matched case-sensitively).
  6. In Inventor's own Hole, give a hole a fit tolerance with a code no workbook row has, then edit it in AWB Hole: it opens as Custom with that code.
  7. Added: at 150 percent the Code row (the box and the hint "e.g. H7 or k6") fits on one line. With a code typed, Upper and Lower look muted and do not accept typing, and Tab and the Up and Down arrows go from Code past them to the next field.
  8. Added, for step 4: open Inventor's Edit Hole dialog, or the diameter parameter, on the k6 hole.
     - If it shows k6 as a fit, the shaft call works.
     - If it shows deviations instead (+0.009 / +0.001 at 6 mm), Inventor refused the shaft class, and `awbaddin.log` has the "refused the shaft fit" warning.
     - Either way, editing the hole opens on Custom k6, from the stored data.

## Defaults

These choices were made to keep the work moving. Aidan can overturn any of them.

- **Layout: the Code box gets its own row, labelled Code, before Upper and Lower.**
  - It uses the same 104-wide box as the limits, so the three boxes line up. A short muted hint follows it, "e.g. H7 or k6", and a fuller tooltip explains it.
  - The label (100), the box (104) and the hint fit the 316-wide content at 150 percent (task 118). The message shows under the box.
  - With a code, Upper and Lower stay in place and show the code's limits, read-only and muted. Tab and the arrow keys skip them.
  - No access key: its letters are taken.
- **Case is kept as typed: capitals are a hole class, lowercase a shaft class.** Shaft classes are worked out for f, g, h, js, k, m, n and p at IT5 to IT11, the shaft counterparts of the hole letters `Iso286` already had. A mixed-case letter (Js7) is refused.
- **A shaft class goes to Inventor as the fit's shaft tolerance** (`SetToFits(type, "", code)`). If Inventor refuses it, it goes on as deviations, with a warning in the log.
- **The code applies live, about 300 ms after the last key, like the number boxes** (task 130). A cleared box waits for Enter or for focus to leave it.
- **Typed limits are kept behind a code** and come back when the code is cleared. The code is kept when Custom is left and chosen again, and across size, units and type changes.
- **Upper and Lower show the code's limits in the window's units**: mm as ISO gives them, inches to 6 decimals.
- **Stored data is version 6**, so an older add-in refuses a Custom code instead of asking for limits it does not have.
- **ISO codes now match in their case**, both when read back from Inventor and when compared for the re-apply offer.

## Follow-ups

- `TODO.md`, the task 113 line ("Toleranced holes in Core"): remove "a hole with an ISO code no row has" from the reviewer's note, since that hole now opens as Custom with its code. The rest of the line still stands: a Max, Min, Basic or Reference tolerance, and a dowel read back as Custom taking the Normal colour.
- Task 113's Defaults still say "An ISO code no row has takes the default row, with a note". This task replaces that.
- If checklist step 8 shows that Inventor refuses a shaft class on a hole diameter, decide whether the window should refuse lowercase codes instead of applying them as deviations.
- The closed Tolerance dropdown says only "Custom" for a Custom code. It could show the code, as an ISO row's entry does.
