# 190: Initial Views window view-model (Core)

- **Milestone:** M5
- **Feature spec:** `docs/features/05-drawing-tools.md` ("Decided for M5: Place Initial Views" (a window before the print is created lets the user confirm or change the views))
- **Status:** todo
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

- [ ] Starts with the plan's views ticked and its scale selected; unticking a view or changing the scale re-runs the plan and shows a warning when the views no longer fit.
- [ ] OK is disabled with no views ticked; Cancel returns no result.
- [ ] The scale list is the standard scales; "Suggested (1:2)" is the first entry.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Follow the existing view-models (`ObservableObject`, `RelayCommand`).
- Read spec 05's "Added for M5" section and the 2026-11-21 rows of `DECISIONS.md` first. Defaults are marked there; do not change them.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
