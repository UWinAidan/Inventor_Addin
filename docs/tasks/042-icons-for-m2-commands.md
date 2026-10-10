# 042: Icons for the M2 commands, and a ribbon that survives one failing command

- **Milestone:** M2
- **Feature spec:** `docs/features/08-window-style.md` (Ribbon icons); `docs/features/01-ribbon-and-shell.md`
- **Status:** todo
- **Depends on:** none
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** 033 to 041

## Goal

The five new ribbon commands of M2 have icons ready before their commands exist, and adding five commands cannot take the whole ribbon down: one command failing to register is logged and skipped.

## Scope

- `src/InventorAddin/UI/Icons/` (new sources and PNGs): `NewPart`, `SaveAsCopy`, `SaveAsReplace`, `CreatePrint`, `MassUpdate`
- `src/InventorAddin.Core/Ribbon/RibbonModels.cs` (`IconNames` only)
- `tools/icons/README.md` (the list of names and the count of PNGs)
- `docs/ui-mockups/ribbon-icons.png` (re-rendered preview)
- `src/InventorAddin/UI/RibbonSetup.cs` and `src/InventorAddin/Commands/AddinCommand.cs` (registration robustness only)
- `tests/InventorAddin.Core.Tests/Ribbon/RibbonIconsTests.cs` if it lists names

Out of scope:

- Ribbon layout entries and `CommandNames` for the new commands: each command's own task adds them, because a button appears only once its command exists (spec 01).

## Acceptance criteria

Icons

- [ ] Each new icon has a 32 px and a 16 px SVG drawn in the style of the existing five (task 024: simple line icons, one weight, our own drawings), rendered with `python3 tools/icons/render.py` to Light and Dark PNGs at both sizes
- [ ] Ideas, not rules: New Part a sheet with a plus; Save As Copy two overlapping sheets; Save As and Replace two sheets with a curved arrow; Create Print a sheet with a small view rectangle and title block band; Mass Update a stack of sheets with a check mark
- [ ] `IconNames` gains the five names; the PNGs are embedded the same way as the existing ones (the project's wildcard for `UI/Icons/*.png` should already cover them; check)
- [ ] The render runs clean and a second run gives byte-identical files

Ribbon robustness (follow-ups from task 030)

- [ ] In `RibbonSetup`, registering each command is inside its own try/catch: a failure logs `ERROR` with the command's internal name and the exception, the command is left out, and its layout entries are skipped with one `WARN` each; every other command and ribbon is still built
- [ ] In `AddinCommand.Register`, when `AddButtonDefinition` with icons throws, the text-only retry first checks `ControlDefinitions[InternalName]` and reuses a definition Inventor already created, instead of failing on the duplicate name
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read `tools/icons/README.md` before drawing: only the SVG features listed there are read by the renderer.
- Follow how the existing code reads `ControlDefinitions` in `AddinCommand.Register` for the duplicate check, and wrap the COM lookup in `ComSafe.Get`.
- The new icons are not shown anywhere until their commands land in tasks 043 to 048; the add-in only loads icons for commands the layout shows.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. After this build, the ribbon looks as before and the log has no new warnings (the new icons are not used yet)
  2. Review `docs/ui-mockups/ribbon-icons.png`: do the five new icons read, and are they distinct from each other and from the existing five?

## Follow-ups

Things noticed but not done.
