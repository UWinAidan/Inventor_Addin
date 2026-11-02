# 085: AWB Hole window view-model (Core)

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window", "Types and seats")
- **Status:** done
- **Depends on:** 084
- **Needs:** cloud
- **Parallel-safe with:** 086, 087, 088

## Goal

Everything the AWB Hole window decides, as a plain `INotifyPropertyChanged` view-model the WPF window binds to: the cascading fields, defaults from the workbook, edit mode, validation, and the spec it hands to the add-in for the preview.

## Scope

- `src/InventorAddin.Core/ViewModels/AwbHoleViewModel.cs` (and small option records)
- `tests/InventorAddin.Core.Tests/ViewModels/`

## Acceptance criteria

- [x] Fields in spec 09's order: Units, Type, Seat, Standard, Fastener type, Size, Fit, Tolerance, Thread depth, Drill depth, Termination, Drill point, Direction. Type lists Clearance and Tapped; Standard lists AWB Clearance and AWB Dowel for Clearance and AWB Tapped for Tapped; Fastener type lists the seat tab's Screw values (spec 09 questions 2 and 3). Each list holds only what applies (for example Seat is None for dowel holes; Fastener type only for seated holes; Tolerance and fit colour only for dowel holes; depths only for tapped; Drill point only for blind holes), rows with `Show = N` are left out, and the workbook's order is kept.
- [x] Changing a field keeps later choices when they still apply and otherwise picks the default (Default = Y rows; the workbook's default units at open). Units switch keeps the nearest matching size where one exists (Default), else the first.
- [x] **Edit mode** starts from a `HoleSpec` (read from the hole) and shows it exactly, before any change is raised for preview (spike 081 round 5: the edit window must load the hole first). A hole made by Inventor's command opens with the closest spec Core can build from its geometry and a note that OK makes it an AWB hole.
- [x] Exposes the current `HoleGeometry` and the check messages from 084; OK is enabled only when the spec is valid and there is at least one centre (the centre count is set by the add-in).
- [x] Raises one "preview needed" notification per user change, not one per cascaded property.
- [x] A "refused" status the add-in can set when Inventor refuses a change ("Inventor could not make this hole; the last good hole is kept").
- [x] Header title and subline text, as other windows (`HeaderTitle`, `HeaderSubline`).
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- From task 084: list rows without the hidden ones (`HoleRules` still finds hidden rows by size, so an older hole opens); preselect with `FindTolerance` and `FindDrillPoint`; show each `HoleRules` problem beside the field it names. A seat on a tapped hole is refused until spec 09 question 6 is answered.

- The workbook reader (082) keeps duplicate rows and several Default = Y rows as they are; decide here which one wins (Default: the first in workbook order) and test it (task 082 follow-up).

- Follow `MaterialFinishViewModel` for the shape (lists, selections, status, commands) and its tests.
- Fit colour text for the window: the fit name with its colour swatch value from the workbook.

## Verification

- **Ran:** `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 3084 passed, 0 failed. 52 of those are new, in `AwbHoleViewModelTests`. The run happened on Aidan's PC with task 088's work in progress in the same tree, and its tests are in the total.
- **Not compiled (changed under `src/InventorAddin`):** none. This task changes Core and tests only.
- **Inventor API members not confirmed:** none used.
- **Manual checklist for Inventor:** nothing to check in Inventor yet. The WPF window and the session (089, 090) bind to this view-model. When they exist: open a part with a sketch of hole centres and click AWB Hole. The window opens on the workbook's default units, Clearance, AWB Clearance, the first size, Normal fit and Through All. Change Standard to AWB Dowel: Seat shows only None, Fit shows Slip, Normal and Press with colour swatches, and Tolerance preselects the Default = Y row. Set Termination to Distance: Drill point appears with its default, and OK stays disabled until a depth is entered. Edit a counterbored AWB hole: it opens as it was made, seat and fastener included, before anything previews.

What was built: `src/InventorAddin.Core/ViewModels/AwbHoleViewModel.cs`, with the option records `HoleOption<T>` and `HoleFitOption`.

- Factories: the constructor for a new hole, `ForEdit(workbook, partName, spec)` for a hole with AWB data, and `ForInventorHole(workbook, partName, closestSpec, notes)` for a hole made by Inventor's command. The last one sets `InventorHoleNote` and `InventorHoleNotes`.
- The add-in sets `CentreCount`, previews `Geometry` on `PreviewNeeded`, calls `ShowRefused(reason)` or `ClearRefused()`, handles `OkRequested` and `CancelRequested`, and stores `BuildResult.Resolved` on OK.
- Checks: `Checks`, `ProblemFor(HoleField)`, and one property per field (`SizeProblem`, `SeatProblem`, ...).
- The view-model does not build the closest spec itself. Task 088 has added `HoleReadBack` to Core, which does that, so `ForInventorHole` takes its spec and notes.

Behaviours picked to keep work moving (not decided by Aidan; the parent may log them as Default):

- When the workbook repeats a size, fastener, tolerance or drill point, it is listed once and the first row wins. When several rows are marked Default = Y, the first in workbook order wins, as `HoleRules.FindTolerance` and `FindDrillPoint` already do (task 082 follow-up).
- A new hole starts as Clearance, AWB Clearance, seat None, first size, Normal fit, Through All, not flipped.
- Changing the Type puts the seat back to None, following spike 081 round 5: "keep a seat unless the user changes the type". So switching to Tapped never lands on the refused tapped seat. Seat still lists all three for a tapped hole, as spec 09 allows. Choosing a seat there shows the `HoleRules` refusal beside Seat until question 6 is answered.
- Fit is listed only for an unseated clearance hole (Close, Normal, Loose) and for a dowel (Slip, Normal, Press). Seated holes take the fit from the row, so they show no Fit. A fit the new list lacks goes back to Normal.
- Thread depth and drill depth are shown only for a tapped hole that ends at a Distance. Through holes are threaded the full depth, so the depths have no effect there. They reset to the Tapped row's defaults when the size or units change, and clearing one also puts back the default.
- A clearance or dowel hole ending at a Distance has a Depth field, which starts blank. OK stays disabled, with "Enter a depth greater than zero." beside it, until a depth is entered. The workbook gives no default depth. The depth is kept while the hole is through and is converted when the units change.
- On a Units switch the size with the nearest nominal diameter is kept. A seated size uses its Clearance row's nominal. With no nominal to compare, the first size is used.
- A choice that is hidden or not in the workbook is listed and kept while the fields above it are unchanged, so an edited hole shows exactly what it was made with. Once a field above it changes, the choice is dropped.
- `PreviewNeeded` is raised only when the normalised spec changes, so typing a depth on a through hole raises nothing. It is not raised for `CentreCount`.
- The status at open counts the workbook's problems ("The hole workbook has 2 problems; see the details."), with each problem in the details. The refused status replaces it until the next user change or `ClearRefused()`.

## Follow-ups

- Task 088/089: open edit mode with `ForEdit(..., stored.Spec)` when `HoleReadBack.Read` returns an AWB source. Otherwise use `ForInventorHole(..., result.Spec, result.Notes)`. Set `CentreCount` whenever the picked centres change, and call `ClearRefused()` after a successful preview.
- Spec 09 has no default depth for a blind clearance or dowel hole. Aidan may want one, for example a multiple of the size or the last depth used, rather than a blank field.
- Spec 09 open question 6 (seat sizes for a tapped hole) is still open. Once it is answered, revisit the Type-change seat reset and the refusal.
