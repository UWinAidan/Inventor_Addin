# 056: Ribbon button sizes by importance

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` ("Decided by Aidan" for 2026-10-11); `docs/features/08-window-style.md` ("Ribbon icons")
- **Status:** done (Core only; nothing tested in Inventor)
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

- [x] `RibbonLayout`: New Part, Part Properties and Create Print are `ButtonSize.Large`; Save As Copy, Save As and Replace, Settings, About, Mass Update and both developer buttons are `ButtonSize.Small`, in every environment they appear in
- [x] A test pins the size of every button, so a later change is deliberate
- [x] The button order within each panel is unchanged unless a large button needs to come first for the ribbon to lay out cleanly; if you change the order, say why in the task file
- [x] Spec 08's "Ribbon icons" list names every button on the ribbon today with its size
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- `RibbonSetup` (add-in) already picks the 16 or 32 px icon from the layout's size; check it does, and change nothing there unless it does not.
- Inventor stacks small buttons in columns of three beside large ones; the CAD Automation panel will hold two large and four small buttons.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: build succeeded, only the 4 existing CS8600 warnings (CreatePrintCommand, NewPartCommand, SaveAsCopyCommand, SaveAsReplaceCommand). `dotnet test tests/InventorAddin.Core.Tests`: 1994 passed, 0 failed (baseline 1983, plus 11 new test cases).
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):** none. `RibbonSetup` already passes `button.Size == ButtonSize.Large` to `CommandControls.AddButton(definition, useLargeIcon)`, and `AddinCommand` sets both `StandardIcon` (16 px) and `LargeIcon` (32 px) on every definition, so the add-in needed no change. The whole solution was compiled locally; nothing was tested in Inventor.
- **Inventor API members not confirmed:** none new; no add-in code changed.
- **Button order:** unchanged. CAD Automation stays New Part, Save As Copy, (Save As and Replace), Part Properties, Settings, About. Inventor stacks the small buttons beside the large ones, so no reordering was needed. If Aidan would rather have the two large buttons side by side, moving Part Properties to second place would do it; that is his call.
- **Manual checklist for Inventor:**
  1. Close Inventor, build and deploy (`dotnet build InventorAddin.slnx`), start Inventor.
  2. With no document open, open the AWB Addin tab: New Part is a large button with its 32 px icon; Mass Update is small (and Export Libraries is small when developer tools are on).
  3. Open a part: CAD Automation shows a large New Part, a small Save As Copy, a large Part Properties, then Settings and About small and stacked. Drawing Tools shows a large Create Print. Modelling shows a small Mass Update. Developer (if on) shows two small buttons.
  4. Open an assembly: as for the part, with Save As and Replace small and stacked under Save As Copy between the two large buttons.
  5. Open a drawing: CAD Automation shows Settings and About, small; no New Part, Part Properties or Create Print.
  6. Switch Inventor to the other theme, restart, and check the large icons read on it too.
  7. If any button still shows at its old size, note which one (see the follow-up below).

## Follow-ups

Things noticed but not done.

- `RibbonSetup.AddButton` returns early when a control with the command's internal name is already on the panel. If Inventor ever keeps the add-in's ribbon controls between sessions, a size change in `RibbonLayout` would not reach the ribbon. Only matters if step 7 of the manual checklist shows a stale size.
