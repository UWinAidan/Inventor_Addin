# 042: Icons for the M2 commands, and a ribbon that survives one failing command

- **Milestone:** M2
- **Feature spec:** `docs/features/08-window-style.md` (Ribbon icons); `docs/features/01-ribbon-and-shell.md`
- **Status:** done (add-in not compiled)
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

- [x] Each new icon has a 32 px and a 16 px SVG drawn in the style of the existing five (task 024: simple line icons, one weight, our own drawings), rendered with `python3 tools/icons/render.py` to Light and Dark PNGs at both sizes
- [x] Ideas, not rules: New Part a sheet with a plus; Save As Copy two overlapping sheets; Save As and Replace two sheets with a curved arrow; Create Print a sheet with a small view rectangle and title block band; Mass Update a stack of sheets with a check mark
- [x] `IconNames` gains the five names; the PNGs are embedded the same way as the existing ones (the project's wildcard for `UI/Icons/*.png` should already cover them; check)
- [x] The render runs clean and a second run gives byte-identical files

Ribbon robustness (follow-ups from task 030)

- [x] In `RibbonSetup`, registering each command is inside its own try/catch: a failure logs `ERROR` with the command's internal name and the exception, the command is left out, and its layout entries are skipped with one `WARN` each; every other command and ribbon is still built
- [x] In `AddinCommand.Register`, when `AddButtonDefinition` with icons throws, the text-only retry first checks `ControlDefinitions[InternalName]` and reuses a definition Inventor already created, instead of failing on the duplicate name
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read `tools/icons/README.md` before drawing: only the SVG features listed there are read by the renderer.
- Follow how the existing code reads `ControlDefinitions` in `AddinCommand.Register` for the duplicate check, and wrap the COM lookup in `ComSafe.Get`.
- The new icons are not shown anywhere until their commands land in tasks 043 to 048; the add-in only loads icons for commands the layout shows.

## Verification

Filled in by the implementer.

- **Ran:**
  - `python3 tools/icons/render.py` (Pillow 12.3.0): runs clean, writes 40 PNGs and the preview. A second run gives byte-identical files (md5 of all PNGs and the preview compared). The 20 existing PNGs are unchanged.
  - `dotnet build src/InventorAddin.Core`: 0 errors.
  - `dotnet test tests/InventorAddin.Core.Tests`: 1145 passed, 0 failed (the working tree also holds the uncommitted tests of 034 and 037). `RibbonIconsTests.EveryIcon_HasItsPngInTheAddinIconsFolder` reads `IconNames` by reflection, so it now checks the 20 new PNGs too; no test needed editing.
  - Embedding: the project's `<EmbeddedResource Include="UI\Icons\*.png" .../>` wildcard already covers the new PNGs. Nothing changed in the `.csproj`.
- **Not compiled (changed under `src/InventorAddin`):** `src/InventorAddin/UI/RibbonSetup.cs`, `src/InventorAddin/Commands/AddinCommand.cs`. This is a cloud session (`CLAUDE_CODE_REMOTE=true`), so the add-in was not built.
- **Inventor API members not confirmed:** none new. The duplicate check uses `ControlDefinitions[string]` cast to `ButtonDefinition` through `ComSafe.Get`, the same as the existing lookup at the top of `AddinCommand.Register`.
- **Also changed, outside the listed files:** `tools/icons/render.py`. Its `ICONS` list names what gets rendered, so the five names had to be added there. The preview's label column was also widened from 250 to 300 px, because "Save As and Replace" ran into the 16 px column. This changes only `docs/ui-mockups/ribbon-icons.png`.
- **Manual checklist for Inventor:**
  1. After this build, the ribbon looks as before and the log has no new warnings (the new icons are not used yet)
  2. Review `docs/ui-mockups/ribbon-icons.png`: do the five new icons read, and are they distinct from each other and from the existing five?
  3. Optional, to try the error path: temporarily add `if (InternalName == InventorAddin.Core.Ribbon.CommandNames.About) throw new InvalidOperationException("test");` as the first line of `AddinCommand.Register`, build and start Inventor. Expect one `ERROR` line "Could not register command ..." in `awbaddin.log`, one `WARN` "Command ... failed to register, so its button on panel ... is skipped" for each of its buttons, and every other button and ribbon still present. Revert afterwards.

## Follow-ups

Things noticed but not done.

- Spec 08, "Ribbon icons", lists only the first five buttons. Each M2 command's task (043 to 048) could add its icon to that list when it adds its layout entry and `CommandNames` constant.
- When Inventor refuses the icons but has already created the definition, `AddinCommand` now reuses it. The first warning still says "the button shows text only", which may not be true for the reused definition. A second warning says the definition was reused, so the log stays readable. Merging the two lines would need the retry to know the outcome before logging.
