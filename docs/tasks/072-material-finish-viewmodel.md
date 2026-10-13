# 072: Material and Finish window view-model (Core)

- **Milestone:** M3
- **Feature spec:** `docs/features/03-material-and-finish.md` ("Target window")
- **Status:** todo
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

- [ ] Built from: the target header (the part's file name, or "N parts selected"), the current material, finish name and finish code (each may be blank; "Varies" when the selected parts differ), the library materials (069), the valid finishes and the finish list's problems (068), the preferred names, a callback that saves a preferred toggle (070's `TogglePreferred`), and an `IMaterialFinishTarget`
- [ ] Filters: Category (069's list, "All" selected), *Preferred only* (ticked when at least one preferred material exists in the library, otherwise unticked: Default, so the list never opens empty), *Show paints* (ticked), and a search box over each list. Changing any filter updates the lists. No *Enable Filters*
- [ ] Materials list rows show the name and whether it is preferred; a star command toggles it and calls the save callback; with *Preferred only* ticked, un-starring removes the row
- [ ] Finishes list: "None" first, then the finishes that suit the selected material (069). Empty until a material is selected. If the selected finish stops suiting when the material changes, the selection goes back to None
- [ ] The current material and finish are selected when the window opens, if they are in the lists
- [ ] Apply and OK are enabled when a material is selected. Apply calls `IMaterialFinishTarget.Apply(MaterialFinishChoice)` (material display name, finish or null, and `FinishProperties.For(finish)`), shows the result's message in the status line, and refreshes the header's current values; OK does the same and then asks to close (an event or `RequestClose`, as the other view-models do); Cancel closes without applying. A failure (exception or failed result) shows in the error style and does not close
- [ ] `MaterialFinishResult` carries a success flag, a status message and notes (for example "Appearance 'X' not found; the part keeps its material's appearance", "Skipped: ASSY_X (not a part)"); the status shows the message and the count of notes, and every note is listed in the status' tooltip or a details text
- [ ] The finish list's problems are shown in the status line when the window opens (for example "2 finish rows could not be read; see the log"), not as an error
- [ ] Tested: filters, star toggling, finish matching on selection change, preselection, enabling, Apply success and failure, OK closing, problems shown
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Follow `PartPropertiesViewModel` and `MassUpdateViewModel` for the pattern: `ObservableObject`, `RelayCommand`, a target interface the add-in implements, status text and its error flag.
- Header text and labels follow spec 08's wording style (sentence case, no colons in labels).

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** none (Core only; 075 checks the window)

## Follow-ups

Things noticed but not done.
