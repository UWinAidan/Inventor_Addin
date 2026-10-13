# 070: Library settings, preferred materials, and the shipped starter files

- **Milestone:** M3
- **Feature spec:** `docs/features/03-material-and-finish.md` ("Add-in side"); `docs/features/01-ribbon-and-shell.md` ("Settings")
- **Status:** todo
- **Depends on:** none
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** 068, 069

## Goal

Settings can point the add-in at a material library and a finish list; blank means the starter files shipped with the add-in, so a plain download works (Aidan, 2026-10-12). Preferred materials are stored in settings. The build copies `libraries/` next to the add-in.

## Scope

- `src/InventorAddin.Core/Settings/AddinSettings.cs`, `SettingsStore.cs` (if loading needs it)
- `src/InventorAddin.Core/Materials/LibraryPaths.cs` (new)
- `src/InventorAddin.Core/ViewModels/SettingsViewModel.cs`
- `src/InventorAddin/UI/SettingsWindow.xaml` and `.xaml.cs`
- `src/InventorAddin/InventorAddin.csproj` (copy `libraries/` to the output)
- The matching tests

## Acceptance criteria

- [ ] `AddinSettings` gains `MaterialLibraryPath` (default `""`), `FinishListPath` (default `""`) and `PreferredMaterials` (a list of display names, default empty; null stored as empty, null entries dropped, copied when set). All three are in `Equals` and `GetHashCode`. An old settings file loads with the defaults. Tested
- [ ] `LibraryPaths.MaterialLibrary(settings, addinFolder)` and `LibraryPaths.FinishList(settings, addinFolder)`: the setting when not blank (trimmed), otherwise `<addinFolder>\libraries\StarterMaterials.adsklib` and `<addinFolder>\libraries\finishes.csv`. Also says whether the path is the starter one, so messages can say so. The file names are constants in `LibraryPaths`. Tested
- [ ] `LibraryPaths.TogglePreferred(settings, name)` returns settings with the name added or removed (ignoring case), so the window's star can save straight away (Default, spec 03 question 11). Tested
- [ ] Settings window: a "Material library" row and a "Finish list" row, each a text box with a Browse button (an Open File dialog filtered to `*.adsklib` and `*.csv`) and a help line "Blank uses the starter … shipped with the add-in." Validation: blank, or a full path to an existing file with the right extension. Preferred materials are not shown in Settings. Follows the shared look and the XAML rules test (spec 08); Browse sits beside its box as the folder rows do
- [ ] `InventorAddin.csproj` copies `..\..\libraries\finishes.csv` and, when it exists, `..\..\libraries\StarterMaterials.adsklib` to `libraries\` in the output folder, so the existing deploy step copies them into Inventor's add-in folder. The build must not fail while the `.adsklib` does not exist yet (task 071 creates it)
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds; `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Follow how task 049 added the work folder and other CAD folders (settings, view-model, window rows, Browse beside the box).
- Aidan's own library path is entered by him in Settings; do not put `B:\` into code. The default stays blank (the starter).
- The add-in's folder is where `InventorAddin.dll` runs from; the command tasks pass it in.

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Settings shows Material library and Finish list rows, blank, with Browse beside each. Labels fit the label column.
  2. Browse to `B:\aidan\AWB_Design_Data\InventorMaterialLibrary.adsklib`; Save; reopen Settings: the path is kept.
  3. Type a path to a file that does not exist: Save is refused with a message naming the setting.
  4. After a build with Inventor closed, `%APPDATA%\Autodesk\Inventor 2026\Addins\InventorAddin\libraries\finishes.csv` exists.

## Follow-ups

Things noticed but not done.
