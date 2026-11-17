# 167: Add-in: the Hole Table Wizard window and its ribbon button

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Target dialog")
- **Status:** done
- **Depends on:** 159, 163
- **Needs:** windows (add-in; Aidan checks in Inventor)
- **Parallel-safe with:** 164, 165

## Goal

A **Hole Table** button on the AWB Addin tab of the drawing ribbon opens the Hole Table Wizard window. The window is bound to task 159's view-model and task 163's source, in the shared window look.

## Scope

- **`UI/HoleTableWindow.xaml`.** Shown through `WindowHost`, with the shared style from `UI/Theme/`. No `UseLayoutRounding` (`CLAUDE.md`). Its layout follows the spec's "Target dialog":
  - **Header:** the drawing name, a Sheet list, and a View list.
  - **Create Tables tab:**
    - View, Analyze View and Create Tables;
    - the two lists, each row showing Tag, Description and the Split checkbox;
    - Swap and Delete between the lists;
    - Generate Precision Table and Generate Clearance Table.
  - **Edit Table tab** (task 162): the stored views, and NOTES per group.
  - **Bottom row:** Align, Renumber, Update, Cancel, a status line and a progress bar.
- **Command.** `Commands/HoleTableCommand.cs` (`Awb_HoleTable`), enabled only when a drawing is active. Its icon is a placeholder for now (Aidan, 2026-11-17; he will send a design): reuse the closest existing 16 and 32 px icon from the task 151 set, and note which in Follow-ups.
- Button text and tooltip in our own words.
- Until tasks 164 to 166 land, the table commands report "not built yet" through the source.

## Acceptance criteria

- [ ] Builds on Windows. The XAML rules test passes.
- [ ] The manual checklist below passes.

## Notes for the implementer

- Follow an existing window for structure and binding, for example `AwbHoleWindow` or `MassUpdateWindow`, and `RibbonSetup` for the drawing ribbon.
- From task 159's review: every Split toggle and Notes edit rebuilds both row collections. Bind Notes with the default `LostFocus` trigger, not `PropertyChanged`, or each keystroke rebuilds the list and the box loses focus. Prefer a `ListBox` or `ItemsControl` with templated rows over a `DataGrid`, which can throw when its source resets while committing an edit.
- The spec's header has a "Toggle View Names" button that no task covers yet. Leave it out and note it under Follow-ups.

## Verification

Filled in by the implementer (Windows, 2026-11-17).

- **Ran:**
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: succeeded, 0 errors (9 warnings, all existing: CS8600 in four older commands, one xUnit2012). Inventor was open, so nothing was deployed and Inventor was not driven.
  - `dotnet test tests/InventorAddin.Core.Tests`: 4796 passed, 0 failed (4789 at the start, plus 7 new rows in the ribbon tests). `WindowXamlRulesTests` passes with the new window and styles. An earlier run showed four Numbering tests failing on timing under load; they passed on the rerun.
- **Not compiled (changed under `src/InventorAddin`):** all of it compiled on Windows. The build checks XAML syntax and resource keys, but not templates at run time, so nothing here has been opened in Inventor: `UI/HoleTableWindow.xaml` and `.xaml.cs`, `Commands/HoleTableCommand.cs`, the new styles at the end of `UI/Theme/Styles.xaml` (`PageTabs`, `PageTab`, `WideList`, `RowDescriptionText`, `BetweenListsButton`, `BusyBar`), the registration in `UI/RibbonSetup.cs`, and the four `HoleTable.*.png` icon copies.
- **Inventor API members not confirmed:** none beyond what existing code already uses. New code uses `CommandTypesEnum.kNonShapeEditCmdType` (as `MaterialFinishCommand`), `DrawingDocument` (as `CreatePrintCommand` and `ModelExtractor`), `DocumentKinds.Of` and `InventorHost.ActiveEditDocument` (as `AwbHoleCommand`). Run-time WPF risks that the build cannot show: the tab strip and busy bar templates, and the busy bar's marquee animation.
- **Manual checklist for Inventor** (close and reopen Inventor after the deploy build; the add-in loads at start):
  1. Open a part, then an assembly: the AWB Addin tab has no Hole Table button. Open a drawing (`.idw`) with a part view that has holes: the AWB Addin tab has a large **Hole Table** button on a Drawing panel, with the AWB Hole icon (a placeholder). With no document open, there is no button.
  2. Click it with the drawing active: the window "Hole Table" opens, the header says the drawing's name. Sheet shows the active sheet, View shows its first view. Click it on a drawing with no views: the lists are empty and the status says to pick a view.
  3. Pick another sheet: the View list changes. Pick another view: the lists clear and the status says to click Analyze View.
  4. Click **Analyze View**: the Precision and Clearance lists fill, each row showing a tag (such as A1), a description and a Split checkbox. The status says how many holes were found.
  5. Click a row, then **Swap**: the hole moves to the other list and its tag changes. Click a row, then **Delete**: the row goes. **Renumber**: tags restart from A1 with no gaps. Tick **Split** on a row: that hole gets its own group and letter.
  6. Click **Create Tables**, **Generate Precision Table**, **Generate Clearance Table**, **Update** and **Align**: until tasks 164 to 166 land, each says "Not yet available." in the status line, and the drawing does not change (check the Undo list is unchanged). The thin busy bar flashes under the body while each runs.
  7. Open the **Edit Table** tab: Stored tables is empty (nothing can be stored yet). Notes lists one entry per group after an Analyze View. Type in a Notes box and click elsewhere: the box keeps its text and the list is rebuilt once, not on every key.
  8. Tab keys: Alt+C and Alt+E switch tabs; Alt+A analyzes; Esc and **Cancel** close the window with no change to the drawing and no Undo entry.
  9. Look at the window at 100% and at 150% display scale, in the light and dark Inventor themes: the tab strip underline, the two lists, the buttons between them and every text box and dropdown show their full borders. Resize the window by its edges: the lists widen. Close and reopen: the size is remembered.
  10. The log file `awbaddin.log` in the data folder (`%APPDATA%/AwbAddin`) shows "Command Awb_HoleTable (Hole Table) started." and no errors.

## Follow-ups

Things noticed but not done.

- **Placeholder icon.** The Hole Table button uses `HoleTable.{Light,Dark}.{16,32}.png`, which are byte copies of the AWB Hole icon (the closest in the task 151 set). When Aidan sends a design, replace those four files; no code changes. `IconNames.HoleTable` and the `HoleTable` icon test already cover them. The `AwbHole*.svg` sources were not copied.
- **Toggle View Names.** The spec's header button is left out, as the task says. No task covers it yet.
- **Busy bar is not progress.** The table commands run on the UI thread, so the bar only shows for the length of a command (and is drawn once before it starts). A real progress bar, or running a long Create Tables with a pumped message loop, needs a view-model change (a busy flag and progress text) and is best decided when tasks 165 and 166 show how long a command takes.
- **Tab styles are new.** `PageTabs` and `PageTab` are the first tab strip in the shared theme. If a later window needs tabs, reuse them rather than adding another.
- **Selecting a row after a Split toggle.** `HoleTableWizardViewModel.Fill` rebuilds both lists on each Split or Notes edit; the view-model re-selects the row by id. If the checkbox click leaves keyboard focus off the list in Inventor, that is a view-model follow-up, not a window one.
