# 190: Initial Views window view-model (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5: Place Initial Views" (a window before the print is created lets the user confirm or change the views))
- **Status:** done
- **Depends on:** 189
- **Needs:** cloud
- **Parallel-safe with:** 191

## Goal

A view-model for the window Create Print and Place Initial Views show: tick the views, pick the scale (or keep the suggested one), see the sheet size, and confirm.

## Scope

- `src/InventorAddin.Core/ViewModels/InitialViewsViewModel.cs`
- Tests.

Out of scope: the WPF window (192).

## Acceptance criteria

- [x] Starts with the plan's views ticked and its scale selected; unticking a view or changing the scale re-runs the plan and shows a warning when the views no longer fit.
- [x] OK is disabled with no views ticked; Cancel returns no result.
- [x] The scale list is the standard scales; "Suggested (1:2)" is the first entry.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Follow the existing view-models (`ObservableObject`, `RelayCommand`).
- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests` on Windows: 5097 passed, 0 failed (includes the new InitialViewsViewModelTests and InitialViewsFixedScaleTests).
- **Not compiled (changed under `src/InventorAddin`):** none; nothing under that folder changed.
- **Inventor API members not confirmed:** none; Core only.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Nothing to check in Inventor yet; the window (192) and the commands that show it come later. This task is covered by unit tests.

## Follow-ups

Things noticed but not done.

- Added an optional `fixedScale` parameter and `DoesNotFitAtScaleMessage` to `InitialViewsPlan.Plan` (task 189's file, outside this task's listed files) so a user-chosen scale can be laid out and checked; the existing behaviour is unchanged when it is null.
- Default chosen: unticking Front also unticks Top and Right (they are projected from it); ticking Top or Right ticks Front. Log in DECISIONS.md as Default.
- The sheet is fixed per window (passed to the constructor); no sheet size picker. Task 192 shows `SheetSizeText`.
- Window 192 binds `Views` (check boxes), `ScaleOptions`/`SelectedScale`, `Warning`/`HasWarning`, `OkCommand`/`CancelCommand`, and closes on `CloseRequested`; the command reads `Result` (null after Cancel).
