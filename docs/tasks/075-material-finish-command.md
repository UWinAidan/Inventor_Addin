# 075: Material and Finish command, window and ribbon button

- **Milestone:** M3
- **Feature spec:** `docs/features/03-material-and-finish.md`; `docs/features/01-ribbon-and-shell.md` ("Target"); `docs/features/08-window-style.md`
- **Status:** done
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

- [x] A **Material and Finish** button, small, in the Properties panel after Mass Update, on the Part and Assembly ribbons (Default: Aidan's sizes rule makes everything but the three main buttons small), icon `MaterialFinish`
- [x] The command: reads settings, resolves the library and finish list paths (070), opens the library and reads the finish list file (a missing or unreadable file stops with a message naming it; finish rows with problems are logged one line each and the rest load), finds the targets (074; in an assembly with no part selected it says to select some and stops), reads the current values, and shows the window on Inventor's window through `WindowHost`. The window's star saves settings straight away
- [x] The window follows the reference layout in spec 03 on the shared look (spec 08): header with the target, current material, current finish and code; the filter row (Category, Preferred only, Show paints); two lists side by side, each with a search box above it; star column in Materials; Apply, OK, Cancel; status line. It passes the XAML rules test. Lists scroll; double-clicking a material selects it, double-clicking a finish does OK
- [x] One Undo after an Apply reverts the material, appearance and the four iProperties (spike 067)
- [x] `RibbonLayout` tests updated for the new button
- [x] Spec 01's panel table lists the button; spec 08 lists the window and the icon
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds; `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Follow `PartPropertiesCommand` and `MassUpdateCommand` for opening a window from a command, and their windows for list and status styling.
- The command's `CanRunOn`: parts and assemblies.
- Binding the lists (task 072): bind each list box's `SelectedItem` to `MaterialListSelection` and `FinishListSelection`, never to `SelectedMaterial` or `SelectedFinish`, so a selection hidden by a filter is kept. Do not set `IsSynchronizedWithCurrentItem`. Show `SelectionText` at all times (for example under the lists), since Ctrl+click can leave a list box showing nothing while the selection is kept. Bind the status tooltip to `StatusDetails` and the error style to `StatusIsError`.
- Wiring 074 (its Follow-ups): open the library first (`MaterialLibraryReader.Open`); when `MaterialLibrary.Error` is set, show it and stop. Build the `AppearanceFinder` after opening, so the opened library gets rank 2. Pass `edit => RunInTransaction(activeDoc, edit)` to `MaterialFinishTargets`, with the active (top-level) document. `AddinServices.AddinFolder` can throw; catch it and show a message. Decide and document the target when a part is edited in place inside an assembly (Default: the part being edited, as Part Properties does).
- Read the finish list file through `File.ReadAllText`; reading fails with a message naming the file. Log each `FinishListProblem` once (the view-model does not log them).

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (local session, Windows, Inventor 2026 installed). `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: build succeeded, 0 errors, 8 warnings, which are the 4 CS8600 warnings that were already there (`CreatePrintCommand`, `SaveAsReplaceCommand`, `SaveAsCopyCommand`, `NewPartCommand`), each reported twice. None is in a file this task touched. `dotnet test tests/InventorAddin.Core.Tests`: 2571 passed, 0 failed, 0 skipped, on the first run. That includes the XAML rules test (`Styling/WindowXamlRulesTests`), run against the new window and the changed `Styles.xaml`.
- **Core (with tests):**
  - `Ribbon/RibbonModels.cs`: `CommandNames.MaterialFinish`.
  - `Ribbon/RibbonLayout.cs`: the button. It is small, in Properties after Mass Update, on Part and Assembly, with icon `MaterialFinish`.
  - `Materials/MaterialLibraryMessages.cs`: four new members.
    - `AddinFolderUnknown`.
    - `FinishListMissing` and `FinishListUnreadable`. Each names the file and says whether it is the starter list or the one set in Settings.
    - `FinishListProblemLogLine`, one log line per finish list problem, naming the file.
  - `Materials/MaterialFinishTargeting.cs`: `TargetsEditedPart(open, edited)`, the in-place rule below.
  - Tests:
    - `Ribbon/RibbonLayoutTests.cs`: the layout, sizes, icon names, and the count of 11 buttons.
    - `Materials/MaterialLibraryMessagesTests.cs`
    - `Materials/MaterialFinishTargetingTests.cs`
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
  - `Commands/MaterialFinishCommand.cs` (new). It is a `DocumentEditCommand` on `InventorHost.ActiveDocument` with `kNonShapeEditCmdType`, and runs on parts and assemblies. The steps, in order:
    1. Find the target.
    2. `MaterialFinishTargets.Find`. A stop message is shown and ends the command.
    3. `AddinServices.AddinFolder`, with `InvalidOperationException` caught and shown.
    4. Resolve the library and finish list paths.
    5. Open the library with `MaterialLibraryReader.Open`. If `Error` is set, show it and stop.
    6. Read the finish list with `File.ReadAllText`. A missing or unreadable file shows a message that names it, and the command stops.
    7. `AppearanceFinder.Read`, after the library is open.
    8. Log the library and appearance warnings, and each `FinishListProblem` once.
    9. `ReadCurrent`.
    10. Build `MaterialFinishTargets` with `edit => RunInTransaction(doc, edit)`, where `doc` is the top-level document.
    11. Show the window through `WindowHost`.

    The star toggles the material with `LibraryPaths.TogglePreferred`, saves through `SettingsStore`, then calls `AddinServices.UpdateSettings`. A wait cursor shows while the library opens.
  - `UI/MaterialFinishWindow.xaml` and `.xaml.cs` (new). The list boxes bind to `MaterialListSelection` and `FinishListSelection`, and `IsSynchronizedWithCurrentItem` is not set. `SelectionText` is always shown in the "Selected" row, with a muted note when `SelectionIsHidden`. The status line binds to `StatusText`, `StatusIsError` and `StatusDetails` (its tooltip). The code-behind adds the shortcuts:
    - Enter in a search box moves to its list.
    - Space on a material toggles its star.
    - Double-clicking a material moves to Finishes.
    - Double-clicking a finish does OK.
  - `UI/RibbonSetup.cs`: registers `MaterialFinishCommand`.
  - **Outside the listed files**, needed for the shared look:
    - `UI/Theme/Styles.xaml`: new styles `PickList`, `PickListItem`, `PickListItemFocusOutline`, `StarButton` and `LeftColumn`. Without them a `ListBox` keeps WPF's system colours, and a window may not set its own. The `DialogFooter` template's status lines now show `StatusToolTip`. Added `xmlns:sys`.
    - `UI/Controls/DialogFooter.cs`: new `StatusToolTip` property, a tooltip on the status line only, so the buttons do not show the notes.
- **Docs:**
  - Spec 01, "Panels today": the button.
  - Spec 08: the window under its windows section, the new shared styles, and Material and Finish in "Ribbon icons" (073 follow-up).
  - Spec 03: open question 18, the in-place default.
- **Inventor API members not confirmed:** everything compiled against the Inventor 2026 interop. The only member not used elsewhere is `CommandTypesEnum.kNonShapeEditCmdType`. Spike 067 and earlier tasks confirmed every other member (`ActiveDocument`, `ActiveEditDocument`, `AssetLibraries.Open`, `TransactionManager.StartTransaction`). Behaviour not yet seen in Inventor:
  - that `kNonShapeEditCmdType` does not get in the way of the window's transactions or of Undo;
  - that `ActiveEditDocument` is the part while a part is edited in place, so the in-place target works;
  - that one Undo reverts the four iProperties on parts reached through an assembly (step 11).
- **Manual checklist for Inventor:** (Settings: Material library blank first, then Aidan's NAS library)
  1. Part ribbon: Material and Finish sits in Properties after Mass Update, small, with its icon.
  2. Open a test part. Run it: the header shows the part, its material and no finish. Category lists the starter categories; the materials list shows the starter materials.
  3. Pick the 4140 flame hardened steel: Finishes shows None and the steel finishes (no anodize). Untick Show paints: the paints go.
  4. Pick Hard chrome, Apply: the part turns blue chrome, mass changes, and iProperties > Custom has Finish "Hard chrome", Finish Code "HC", Finish Thickness "0.013-0.025 mm" and Finish Note with the chrome note. The header now shows the new values.
  5. Edit > Undo once: material, appearance and the four properties are all back as before.
  6. Pick an aluminium and Anodize Type II, black: black anodized look. Then pick None: the four properties are gone and the part shows its material's appearance.
  7. Star two materials, tick Preferred only: only they show. Apply one of the starred materials, close and rerun: both are still starred and Preferred only is ticked. (Preferred only is not saved: it opens ticked only when the part's material is starred or blank.)
  8. Search "6061" in Materials and "anod" in Finishes: the lists filter.
  9. Pick a steel and a paint finish, untick Show paints, Apply: the paint is applied. Change Category so the selected material is hidden, then back: the row is selected again.
  10. Assembly with two parts and a sub-assembly: run with nothing selected: the message says to select parts. Select both parts and the sub-assembly, run: header "2 parts selected", Apply changes both parts; the status notes the skipped sub-assembly. One Undo reverts both.
  11. After the assembly step's Undo, check iProperties > Custom on both parts: the four Finish properties are reverted too (not confirmed by spike 067).
  12. The Materials list: appearance names are read (no warnings in the log about appearances for every material).
  13. Select two occurrences of the same part: header "1 part selected".
  14. In Settings, point Material library at `B:\aidan\AWB_Design_Data\InventorMaterialLibrary.adsklib`: rerun: your own materials and categories are listed (opening takes about a second).
  15. Point Finish list at a copy with a bad row (for example Kind "Plate"): the window opens with a status line about one row, and the log names the line.
  16. Point Material library at a missing file: a message names the path; nothing opens.
  17. Point Finish list at a missing file: a message names the path; nothing opens. Open the finish list in Excel (which locks it) and run: either the window opens (Excel allowed the read) or a message names the file and says to close it.
  18. Look in dark and light themes: the lists use the input fill and border, the selected entry has a grey fill and is bold, and a filled star stands out from an empty one. There is no white system list in the dark theme. The window does not change size when a filter empties a list.
  19. Keyboard: Tab goes through Category, Preferred only, Show paints, Find material, the Materials list, Find finish, the Finishes list, Apply, OK and Cancel. Type in Find material and press Enter: focus moves to the list, and OK is not pressed. Press Space on a material: its star toggles. Esc closes the window.
  20. Double-click a material: focus moves to the Finishes list. Double-click a finish: it applies and the window closes (one Undo reverts it). Double-click a star quickly: the star toggles twice, and focus does not jump.
  21. Ctrl+click the selected material: the list shows no selection, but "Selected" still names it and Apply still uses it.
  22. Hover the status line after an Apply in an assembly with a skipped sub-assembly: the tooltip lists the note. Hover OK: no such tooltip.
  23. Assembly: double-click a part to edit it in place, then run: the header shows that part's file name (not "N parts selected"), whatever is selected. Apply, return to the assembly, and Undo once: the change is reverted. Check whether it shows in the Undo list while editing in place.
  24. With Material library blank: the header icon is the Material and Finish icon. Star a material, then open Settings: nothing else changed. Close and reopen Inventor: the star is still there.

## Follow-ups

Things noticed but not done.

- **Defaults for the parent to log.** None of these were decided by Aidan.
  - A part edited in place inside an assembly is the target, whatever is selected. The transaction is still started on the top-level document (spec 03, question 18; `MaterialFinishTargeting.TargetsEditedPart`).
  - The command type is `kNonShapeEditCmdType`. Material, appearance and iProperties are not geometry, and Part Properties uses `kFilePropertyEditCmdType` for its iProperties only.
  - The window title is "Select Material and Finish", as spec 03 names it. The ribbon button says "Material and Finish".
  - Keyboard and mouse:
    - Double-clicking a material also moves focus to the Finishes list. The brief only says it selects it.
    - Space on a material toggles its star, because the star button is not a Tab stop.
    - Enter in a search box moves focus to its list instead of pressing OK, so a search cannot apply by accident.
  - The library is opened before the finish list is read. A missing finish list therefore shows its message after the library has opened, which takes about 1 s from the NAS.
  - Each library, appearance and finish list warning is logged once per run, prefixed "Material and Finish: ".
- **Outside the listed files.** `UI/Theme/Styles.xaml` (pick list, star and column styles) and `UI/Controls/DialogFooter.cs` (`StatusToolTip`). Both were needed: the XAML rules forbid a window from styling its own `ListBox`, and the brief asks for a tooltip on the status line. The Core message and targeting members and their tests were added under the "logic in Core" rule.
- The pick list and the Mass Update list use WPF's default scroll bars, which stay light in the dark theme. A shared `ScrollBar` style would fix both.
- `MaterialFinishViewModel.CancelCommand` is not used. Cancel closes through `IsCancel`, as in Part Properties.
- Task 076 (Part Properties' Change button) can reuse this command's open-library-and-show flow. Moving the steps between "find the target" and "show the window" into a helper would avoid duplicating them.
