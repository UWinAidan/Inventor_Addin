# 085: AWB Hole window view-model (Core)

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window", "Types and seats")
- **Status:** todo
- **Depends on:** 084
- **Needs:** cloud
- **Parallel-safe with:** 086, 087, 088

## Goal

Everything the AWB Hole window decides, as a plain `INotifyPropertyChanged` view-model the WPF window binds to: the cascading fields, defaults from the workbook, edit mode, validation, and the spec it hands to the add-in for the preview.

## Scope

- `src/InventorAddin.Core/ViewModels/AwbHoleViewModel.cs` (and small option records)
- `tests/InventorAddin.Core.Tests/ViewModels/`

## Acceptance criteria

- [ ] Fields in spec 09's order: Units, Type, Seat, Standard, Fastener type, Size, Fit, Tolerance, Thread depth, Drill depth, Termination, Drill point, Direction. Type lists Clearance and Tapped; Standard lists AWB Clearance and AWB Dowel for Clearance and AWB Tapped for Tapped; Fastener type lists the seat tab's Screw values (spec 09 questions 2 and 3). Each list holds only what applies (for example Seat is None for dowel holes; Fastener type only for seated holes; Tolerance and fit colour only for dowel holes; depths only for tapped; Drill point only for blind holes), rows with `Show = N` are left out, and the workbook's order is kept.
- [ ] Changing a field keeps later choices when they still apply and otherwise picks the default (Default = Y rows; the workbook's default units at open). Units switch keeps the nearest matching size where one exists (Default), else the first.
- [ ] **Edit mode** starts from a `HoleSpec` (read from the hole) and shows it exactly, before any change is raised for preview (spike 081 round 5: the edit window must load the hole first). A hole made by Inventor's command opens with the closest spec Core can build from its geometry and a note that OK makes it an AWB hole.
- [ ] Exposes the current `HoleGeometry` and the check messages from 084; OK is enabled only when the spec is valid and there is at least one centre (the centre count is set by the add-in).
- [ ] Raises one "preview needed" notification per user change, not one per cascaded property.
- [ ] A "refused" status the add-in can set when Inventor refuses a change ("Inventor could not make this hole; the last good hole is kept").
- [ ] Header title and subline text, as other windows (`HeaderTitle`, `HeaderSubline`).
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- From task 084: list rows without the hidden ones (`HoleRules` still finds hidden rows by size, so an older hole opens); preselect with `FindTolerance` and `FindDrillPoint`; show each `HoleRules` problem beside the field it names. A seat on a tapped hole is refused until spec 09 question 6 is answered.

- The workbook reader (082) keeps duplicate rows and several Default = Y rows as they are; decide here which one wins (Default: the first in workbook order) and test it (task 082 follow-up).

- Follow `MaterialFinishViewModel` for the shape (lists, selections, status, commands) and its tests.
- Fit colour text for the window: the fit name with its colour swatch value from the workbook.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups
