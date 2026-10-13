# 075: Material and Finish command, window and ribbon button

- **Milestone:** M3
- **Feature spec:** `docs/features/03-material-and-finish.md`; `docs/features/01-ribbon-and-shell.md` ("Target"); `docs/features/08-window-style.md`
- **Status:** todo
- **Depends on:** 072, 073, 074
- **Needs:** windows build (local session)
- **Parallel-safe with:** none

## Goal

Aidan can pick a material and finish for a part, or for the selected parts of an assembly, from a ribbon button, and see it applied.

## Scope

- `src/InventorAddin/Commands/MaterialFinishCommand.cs` (new)
- `src/InventorAddin/UI/MaterialFinishWindow.xaml` and `.xaml.cs` (new)
- `src/InventorAddin.Core/Ribbon/RibbonLayout.cs`, `RibbonModels.cs` (`CommandNames`), and the ribbon tests
- `src/InventorAddin/UI/RibbonSetup.cs` (register the command)
- `docs/features/01-ribbon-and-shell.md` and `08-window-style.md` (the button and the window)

## Acceptance criteria

- [ ] A **Material and Finish** button, small, in the Properties panel after Mass Update, on the Part and Assembly ribbons (Default: Aidan's sizes rule makes everything but the three main buttons small), icon `MaterialFinish`
- [ ] The command: reads settings, resolves the library and finish list paths (070), opens the library and reads the finish list file (a missing or unreadable file stops with a message naming it; finish rows with problems are logged one line each and the rest load), finds the targets (074; in an assembly with no part selected it says to select some and stops), reads the current values, and shows the window on Inventor's window through `WindowHost`. The window's star saves settings straight away
- [ ] The window follows the reference layout in spec 03 on the shared look (spec 08): header with the target, current material, current finish and code; the filter row (Category, Preferred only, Show paints); two lists side by side, each with a search box above it; star column in Materials; Apply, OK, Cancel; status line. It passes the XAML rules test. Lists scroll; double-clicking a material selects it, double-clicking a finish does OK
- [ ] One Undo after an Apply reverts the material, appearance and the four iProperties (spike 067)
- [ ] `RibbonLayout` tests updated for the new button
- [ ] Spec 01's panel table lists the button; spec 08 lists the window and the icon
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds; `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Follow `PartPropertiesCommand` and `MassUpdateCommand` for opening a window from a command, and their windows for list and status styling.
- The command's `CanRunOn`: parts and assemblies.
- Binding the lists (task 072): bind each list box's `SelectedItem` to `MaterialListSelection` and `FinishListSelection`, never to `SelectedMaterial` or `SelectedFinish`, so a selection hidden by a filter is kept. Do not set `IsSynchronizedWithCurrentItem`. Show `SelectionText` at all times (for example under the lists), since Ctrl+click can leave a list box showing nothing while the selection is kept. Bind the status tooltip to `StatusDetails` and the error style to `StatusIsError`.
- Read the finish list file through `File.ReadAllText`; reading fails with a message naming the file. Log each `FinishListProblem` once (the view-model does not log them).

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (Settings: Material library blank first, then Aidan's NAS library)
  1. Part ribbon: Material and Finish sits in Properties after Mass Update, small, with its icon.
  2. Open a test part. Run it: the header shows the part, its material and no finish. Category lists the starter categories; the materials list shows the starter materials.
  3. Pick the 4140 flame hardened steel: Finishes shows None and the steel finishes (no anodize). Untick Show paints: the paints go.
  4. Pick Hard chrome, Apply: the part turns blue chrome, mass changes, and iProperties > Custom has Finish "Hard chrome", Finish Code "HC", Finish Thickness "0.013-0.025 mm" and Finish Note with the chrome note. The header now shows the new values.
  5. Edit > Undo once: material, appearance and the four properties are all back as before.
  6. Pick an aluminium and Anodize Type II, black: black anodized look. Then pick None: the four properties are gone and the part shows its material's appearance.
  7. Star two materials, tick Preferred only: only they show. Close and rerun: still starred and Preferred only is ticked.
  8. Search "6061" in Materials and "anod" in Finishes: the lists filter.
  9a. Pick a steel and a paint finish, untick Show paints, Apply: the paint is applied. Change Category so the selected material is hidden, then back: the row is selected again.
  9. Assembly with two parts and a sub-assembly: run with nothing selected: the message says to select parts. Select both parts and the sub-assembly, run: header "2 parts selected", Apply changes both parts; the status notes the skipped sub-assembly. One Undo reverts both.
  10. Select two occurrences of the same part: header "1 part selected".
  11. In Settings, point Material library at `B:\aidan\AWB_Design_Data\InventorMaterialLibrary.adsklib`: rerun: your own materials and categories are listed (opening takes about a second).
  12. Point Finish list at a copy with a bad row (for example Kind "Plate"): the window opens with a status line about one row, and the log names the line.
  13. Point Material library at a missing file: a message names the path; nothing opens.

## Follow-ups

Things noticed but not done.
