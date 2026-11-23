# 234: Initial Views: live sheet preview

- **Milestone:** M5 (from Aidan's chunk 2 check, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md` (task 192)
- **Status:** todo
- **Depends on:** 232, 233
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

The Initial Views window shows a live preview of the sheet: border, title block, the view boxes at the scale and the front view direction, updating as units, views, front view, scale or sheet change.

## Scope

- Core: the preview geometry (sheet, border, title block, view rectangles) from the plan, tests.
- A WPF preview control in the window bound to it; no Inventor calls.

## Acceptance criteria

- [ ] Every change in the window updates the preview at once.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
