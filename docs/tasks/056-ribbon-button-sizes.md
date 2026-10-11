# 056: Ribbon button sizes by importance

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Decided by Aidan" for 2026-10-11); `docs/features/08-window-style.md` ("Ribbon icons")
- **Status:** todo
- **Depends on:** none
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** none

## Goal

Aidan's item 12 (2026-10-11): New Part, Part Properties and Create Print are large ribbon buttons; every other button is small. There is no Generate ribbon button and none is added (Aidan: Generate stays in Part Properties).

## Scope

- `src/InventorAddin.Core/Ribbon/RibbonLayout.cs`
- `tests/InventorAddin.Core.Tests/Ribbon/` (the layout tests)
- `docs/features/08-window-style.md`, "Ribbon icons" list only (folded in from `TODO.md`: it names only the first five buttons)

Out of scope:

- New icons. Every icon already has its 32 px files.

## Acceptance criteria

- [ ] `RibbonLayout`: New Part, Part Properties and Create Print are `ButtonSize.Large`; Save As Copy, Save As and Replace, Settings, About, Mass Update and both developer buttons are `ButtonSize.Small`, in every environment they appear in
- [ ] A test pins the size of every button, so a later change is deliberate
- [ ] The button order within each panel is unchanged unless a large button needs to come first for the ribbon to lay out cleanly; if you change the order, say why in the task file
- [ ] Spec 08's "Ribbon icons" list names every button on the ribbon today with its size
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- `RibbonSetup` (add-in) already picks the 16 or 32 px icon from the layout's size; check it does, and change nothing there unless it does not.
- Inventor stacks small buttons in columns of three beside large ones; the CAD Automation panel will hold two large and four small buttons.

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
