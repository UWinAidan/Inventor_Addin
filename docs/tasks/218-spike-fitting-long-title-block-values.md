# 218: Spike: fitting long title block values

- **Milestone:** M5 (from Aidan's task 187 check, 2026-11-22)
- **Feature spec:** `docs/features/05-drawing-tools.md`
- **Status:** todo
- **Depends on:** 187
- **Needs:** **windows** (spike, parent with Inventor)
- **Parallel-safe with:** any

## Goal

Title block values (Finish Spec, Description, Material) can be longer than their cells. Aidan asked for a safety net: shrink a too-long value to fit. Find out whether Inventor can fit or wrap title block field text, and report the options before anything is built.

## Scope

- In a scratch copy of the template:
  - a field text box made with `TextBoxes.AddByRectangle` (does it wrap a long property value?);
  - `TextBox.WidthScale` or a `<StyleOverride>` width factor on a field (does it compress?);
  - `FitTextToBox` or similar members, if the interop has any.
- Look at what a drawing-time step could do instead: Create Print or an Update measuring `GetResultText` with the task 215 width estimate and setting a per-drawing override.
- Record each result under Results and list the options for Aidan. Then the parent writes the build task.

## Acceptance criteria

- [ ] Results list what Inventor can and cannot do, and the options with their costs.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
