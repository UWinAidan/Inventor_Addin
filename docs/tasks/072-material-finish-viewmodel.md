# 072: Material and Finish window view-model (Core)

- **Milestone:** M3
- **Feature spec:** `docs/features/03-material-and-finish.md` ("Target window")
- **Status:** done
- **Depends on:** 068, 069, 070
- **Needs:** cloud or windows (Core only)
- **Parallel-safe with:** 071, 073

## Goal

A view-model for the "Select Material and Finish" window, holding every decision the window makes, so the WPF window (075) only binds to it.

## Scope

- `src/InventorAddin.Core/ViewModels/MaterialFinishViewModel.cs` (new)
- `src/InventorAddin.Core/Materials/IMaterialFinishTarget.cs`, `MaterialFinishChoice.cs`, `MaterialFinishResult.cs` (new)
- `tests/InventorAddin.Core.Tests/ViewModels/MaterialFinishViewModelTests.cs`

## Acceptance criteria

- [x] Built from: the target header (the part's file name, or "N parts selected"), the current material, finish name and finish code (each may be blank; "Varies" when the selected parts differ), the library materials (069), the valid finishes and the finish list's problems (068), the preferred names, a callback that saves a preferred toggle (070's `TogglePreferred`), and an `IMaterialFinishTarget`
- [x] Filters: Category (069's list, "All" selected), *Preferred only* (ticked when at least one preferred material exists in the library, otherwise unticked: Default, so the list never opens empty; also unticked when the current material is in the library but not preferred, so it is shown and preselected: parent decision during review), *Show paints* (ticked), and a search box over each list. Changing any filter updates the lists but never changes the selection: a selected material or finish that a filter hides stays selected, is what Apply uses, and shows again when the filter allows it (parent decision during review). No *Enable Filters*
- [x] Materials list rows show the name and whether it is preferred; a star command toggles it and calls the save callback; with *Preferred only* ticked, un-starring removes the row
- [x] Finishes list: "None" first, then the finishes that suit the selected material (069). Empty until a material is selected. If the selected finish stops suiting when the material changes, the selection goes back to None (only then)
- [x] The current material and finish are selected when the window opens, if the material is in the library and the finish suits it
- [x] Apply and OK are enabled when a material is selected. Apply calls `IMaterialFinishTarget.Apply(MaterialFinishChoice)` (material display name, finish or null, and `FinishProperties.For(finish)`), shows the result's message in the status line, and refreshes the header's current values; OK does the same and then asks to close (an event or `RequestClose`, as the other view-models do); Cancel closes without applying. A failure (exception or failed result) shows in the error style and does not close
- [x] `MaterialFinishResult` carries a success flag, a status message and notes (for example "Appearance 'X' not found; the part keeps its material's appearance", "Skipped: ASSY_X (not a part)"); the status shows the message and the count of notes, and every note is listed in the status' tooltip or a details text
- [x] The finish list's problems are shown in the status line when the window opens (for example "2 finish rows could not be read; see the log"), not as an error
- [x] Tested: filters, star toggling, finish matching on selection change, preselection, enabling, Apply success and failure, OK closing, problems shown
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Follow `PartPropertiesViewModel` and `MassUpdateViewModel` for the pattern: `ObservableObject`, `RelayCommand`, a target interface the add-in implements, status text and its error flag.
- Header text and labels follow spec 08's wording style (sentence case, no colons in labels).

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` (Windows): succeeded, 0 errors, 8 warnings, all CS8600 in files this task did not touch (CreatePrintCommand, NewPartCommand, SaveAsCopyCommand, SaveAsReplaceCommand). `dotnet test tests/InventorAddin.Core.Tests`: 2495 passed, 0 failed (53 in `MaterialFinishViewModelTests`). Rerun after the parent's two review changes.
- **Not compiled (changed under `src/InventorAddin`):** none; nothing under `src/InventorAddin` was changed.
- **Inventor API members not confirmed:** none (Core only).
- **Manual checklist for Inventor:** none (Core only; 075 checks the window)

Choices made while implementing (for review; none overturns a spec decision):

- `MaterialFinishCurrent` (in the view-model file) is the header input. It has `ForPart`, `ForParts` (the "N parts selected" text and "Varies" per value, compared ignoring case, blank counting as a value) and `Combine`, so 074 can build the header without its own rule.
- Header display: the title is the target; `CurrentMaterialText` is "No material" when blank; `CurrentFinishText` is "Hard chrome (HC)", the name alone when there is no code, "No finish" when blank, and "Varies" when the parts differ; `HeaderSubline` joins the two with " · " (no labels, no colons).
- **Selection versus visible lists (parent decision during review).** The filters and search boxes only change `Materials` and `Finishes`, the visible lists; they never change `SelectedMaterial` or `SelectedFinish`, which are what Apply uses. A WPF ListBox writes null to its bound SelectedItem when its items change and the selected item is no longer in them, which would clear a hidden selection. So the list boxes bind to separate properties, `MaterialListSelection` and `FinishListSelection`: each returns the selection while the visible list holds it and null while it is hidden, sets the selection when given a row, and ignores null (raising its change again so the list box shows nothing selected). `SelectionIsHidden` is true while a filter hides either selection, and `SelectionText` ("Steel, Test 100 · Test chrome (TC)", or "... · None") names the selection, so task 075 can show it in the header or status. The selected finish goes back to None only when it no longer suits the selected material; *Show paints* and the finish search only hide it. None is always listed, whatever the finish search. Rows (`MaterialRow`, `FinishRow`) are created once, so a row stays the same object.
- **Preferred only at open (parent decision during review).** Ticked when the library holds a preferred material, except when the current material is in the library but not preferred: then unticked, so it shows and is preselected. When the current material is blank, "Varies" or not in the library, the Default applies.
- Preselection: the current material is selected when it is in the library; the current finish is matched among the finishes that suit it, by code first, then by name; "Varies" never matches.
- Star: the callback gets the trimmed display name; on an exception the star stays as it was and the status shows "Could not save the preferred materials: ..." in the error style. Rows sharing the name all change.
- Status: Apply's message (default "Applied. Save the document to keep the changes." when blank), then " N notes." when the result has notes; the notes are in `StatusNotes` and `StatusDetails` (one per line, for the tooltip). A failure (exception or failed result) uses the error style, keeps the header and does not close. At open, finish list problems give "N finish rows could not be read; see the log." (problems on the same line count once; "The finish list could not be read; see the log." when no finish loaded), not in the error style, with each problem in the details. The view-model does not log the problems: task 075 logs them when it reads the file.
- Cancel is a `CancelCommand` that raises `CloseRequested`, as OK does after a successful apply.
- `MaterialFinishChoice.For(name, finish)` builds the choice with `FinishProperties.For(finish)`; `MaterialFinishResult.Success` and `Failure` build results.

## Follow-ups

Things noticed but not done.

- Both earlier follow-ups (no preselection under *Preferred only*, and the finish search resetting the selection) were fixed in this task at the parent's request.
- Task 075: bind the list boxes' SelectedItem to `MaterialListSelection` and `FinishListSelection` (not `SelectedMaterial` and `SelectedFinish`), and show `SelectionText` somewhere visible, at least while `SelectionIsHidden` is true.
