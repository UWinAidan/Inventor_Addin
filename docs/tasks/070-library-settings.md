# 070: Library settings, preferred materials, and the shipped starter files

- **Milestone:** M3
- **Feature spec:** `docs/features/03-material-and-finish.md` ("Add-in side"); `docs/features/01-ribbon-and-shell.md` ("Settings")
- **Status:** done
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

- [x] `AddinSettings` gains `MaterialLibraryPath` (default `""`), `FinishListPath` (default `""`) and `PreferredMaterials` (a list of display names, default empty; null stored as empty, null entries dropped, copied when set). All three are in `Equals` and `GetHashCode`. An old settings file loads with the defaults. Tested
- [x] `LibraryPaths.MaterialLibrary(settings, addinFolder)` and `LibraryPaths.FinishList(settings, addinFolder)`: the setting when not blank (trimmed), otherwise `<addinFolder>\libraries\StarterMaterials.adsklib` and `<addinFolder>\libraries\finishes.csv`. Also says whether the path is the starter one, so messages can say so. The file names are constants in `LibraryPaths`. Tested
- [x] `LibraryPaths.TogglePreferred(settings, name)` returns settings with the name added or removed (ignoring case), so the window's star can save straight away (Default, spec 03 question 11). Tested
- [x] Settings window: a "Material library" row and a "Finish list" row, each a text box with a Browse button (an Open File dialog filtered to `*.adsklib` and `*.csv`) and a help line "Blank uses the starter … shipped with the add-in." Validation: blank, or a full path to an existing file with the right extension. Preferred materials are not shown in Settings. Follows the shared look and the XAML rules test (spec 08); Browse sits beside its box as the folder rows do
- [x] `InventorAddin.csproj` copies `..\..\libraries\finishes.csv` and, when it exists, `..\..\libraries\StarterMaterials.adsklib` to `libraries\` in the output folder, so the existing deploy step copies them into Inventor's add-in folder. The build must not fail while the `.adsklib` does not exist yet (task 071 creates it)
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds; `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Follow how task 049 added the work folder and other CAD folders (settings, view-model, window rows, Browse beside the box).
- Aidan's own library path is entered by him in Settings; do not put `B:\` into code. The default stays blank (the starter).
- The add-in's folder is where `InventorAddin.dll` runs from; the command tasks pass it in.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` on Aidan's PC: succeeded, 0 errors, 8 warnings (all CS8600 in existing command files this task did not touch). `dotnet test tests/InventorAddin.Core.Tests`: 2417 passed, 0 failed, 0 skipped. The output folder `src/InventorAddin/bin/Debug/net8.0-windows/libraries/` holds `finishes.csv`. With a temporary dummy `libraries/StarterMaterials.adsklib` the build also copied it to `libraries\` in the output; without it the build succeeds and copies only `finishes.csv` (the dummy was deleted afterwards).
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):** `InventorAddin.csproj`, `UI/SettingsWindow.xaml`, `UI/SettingsWindow.xaml.cs`.
- **Inventor API members not confirmed:** none; no Inventor API is used. The file picker is WPF's `Microsoft.Win32.OpenFileDialog` (`Title`, `Filter`, `Multiselect`, `CheckFileExists`, `InitialDirectory`, `ShowDialog(Window)`, `FileName`), which compiled.
- **Choices made:**
  - Whether a library file exists is checked when Save is pressed, not while typing (a check per keystroke could block on a disconnected drive, spike 032), and only for a path that changed since the last save, so a library on an offline NAS does not stop the user saving another setting. A missing file shows "Material library: '…' was not found. Choose an existing file, or leave it blank to use the starter file." and nothing is written. While typing, the path must be blank or a full Windows path ending in `.adsklib` / `.csv` (ignoring case).
  - The two rows are in a new "Material and finish" section after Folders, before Developer. Validation reports problems in window order, so these come after the folder rows.
  - `TogglePreferred` adds the name trimmed at the end of the list, removes every entry that matches ignoring case and surrounding spaces, and throws for a blank name. A small `LibraryPaths.IsPreferred` was added for the star's state.
  - `LibraryPaths` returns a `LibraryPath(Path, IsStarter)` record. The starter path is built with `Path.Combine(addinFolder, "libraries", fileName)`.
  - `SettingsViewModel` takes an optional `fileExists` function (default `File.Exists`) so the save check is testable.
- **Manual checklist for Inventor:**
  1. Settings shows Material library and Finish list rows, blank, with Browse beside each. Labels fit the label column.
  2. Browse to `B:\aidan\AWB_Design_Data\InventorMaterialLibrary.adsklib`; Save; reopen Settings: the path is kept.
  3. Type a path to a file that does not exist: Save is refused with a message naming the setting.
  4. After a build with Inventor closed, `%APPDATA%\Autodesk\Inventor 2026\Addins\InventorAddin\libraries\finishes.csv` exists.
  5. Type `D:\x.txt` in Finish list: the status line says "Finish list must be a file ending in .csv." and Save is disabled. Clear it: the message goes.
  6. Browse beside Finish list shows only `*.csv` files and starts in the folder of the path in the box.

## Follow-ups

Things noticed but not done.

- The Browse buttons beside Material library and Finish list have no access key: every letter of "Browse" is already used in the window. Decide whether to relabel or leave them.
- The command tasks (072 onward) need the add-in folder (where `InventorAddin.dll` runs from) to pass to `LibraryPaths`; nothing in `AddinServices` exposes it yet.
- Spec 01, open question 3 lists the settings built so far; it could add `MaterialLibraryPath`, `FinishListPath` and `PreferredMaterials` (M3).
