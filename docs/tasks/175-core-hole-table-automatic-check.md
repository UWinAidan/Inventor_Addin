# 175: Core: the automatic out-of-date check of a drawing's hole tables, its prompt and its setting

- **Milestone:** M4 part 2 (request from Aidan, 2026-11-20)
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Decided behaviour": Update)
- **Status:** todo
- **Depends on:** 171, 172
- **Needs:** cloud (Core only; runs on Windows too)
- **Parallel-safe with:** 174

## Goal

Request (Aidan, 2026-11-20): when a drawing with stored hole tables becomes the active document, or after it updates, the add-in runs task 171's out-of-date check. If any view's tables are out of date, a small window names the views with **Update now** and **Later**. Later does not ask again until something else changes. It never fires while the user is editing the part. A Settings switch turns it off; it is on by default.

This task builds every decision and the prompt's view-model in Core. Task 176 hooks it to Inventor.

## Scope

- `src/InventorAddin.Core/HoleTable/IHoleTableSource.cs`: add `GetKeyedSheets()`, a default interface method returning `GetSheets()`, documented like `GetKeyedViews`: the sheets that already have a key, never writing one. (Task 176 overrides it in `HoleTableSource`.)
- `src/InventorAddin.Core/HoleTable/HoleTableStoredCheck.cs` (new): the check loop now in `HoleTableWizardViewModel.CheckStoredTables`, moved into a static `Run(IHoleTableSource source, IEnumerable<HoleTableSheetInfo> sheets)` that returns the out-of-date and could-not-check lists (`HoleTableStoredViewCheck`). Only `GetKeyedViews`, `ReadState` and `Analyze` are called. The wizard calls it with its `Sheets`; the automatic check calls it with `GetKeyedSheets()`. Behaviour of the wizard is unchanged.
- `src/InventorAddin.Core/HoleTable/HoleTableStoredUpdate.cs` (new): `UpdateStored` from the wizard view-model moved into a static method both the wizard and the prompt call (same text, same result). The wizard's own path for a view open in the window stays in the wizard.
- `src/InventorAddin.Core/HoleTable/HoleTableAutoCheck.cs` (new): a plain class holding the policy, with time passed in so it is testable. It decides:
  - **When a check is due.** A drawing activated, or a drawing updated (the add-in passes only updates of the drawing's references, see task 176), makes a check due for that drawing after a quiet time (`QuietTime`, 1.5 s since the last such event for it). Nothing is due when the setting is off.
  - **Whether a due check may run now.** Only when that drawing is the active document, it is being edited itself (not a sketch or other object in edit), no Inventor command is running, and no AWB window is open. Otherwise it stays due and is tried again on the next tick. A drawing that updated while not active runs its check when it is next activated. A closed drawing is forgotten.
  - **Whether to show the prompt** after a check: only when at least one view is out of date (could-not-check views alone do not prompt; they are logged), and the result differs from the one last answered with **Later** for that drawing. The result compared is the set of (sheet key, view key, reason text), ignoring order, so another hole moving changes it and asks again.
  - **Later** remembers that result for the drawing, for this Inventor session only. **Update now** forgets it.
  - **The Hole Table window** (task 172) counts as having asked: when it closes, the add-in runs the check once more without prompting and remembers its result as answered with Later, so closing the wizard after "Not now" does not bring the prompt straight up.
  - Drawings are identified by a string the add-in passes (full file name, or the document's internal name for an unsaved drawing); compared ignoring case.
- `src/InventorAddin.Core/ViewModels/HoleTableOutOfDatePromptViewModel.cs` (new): takes the source, the drawing's display name and the out-of-date list. Exposes a heading (`HoleTableMessages`, for example "Some hole tables in DRAWING.idw are out of date:"), the view lines (`HoleTableMessages.ViewLine` with each reason), `UpdateNowCommand` and `LaterCommand`, and raises `CloseRequested` with an outcome (`Updated`, `Later`). Update now updates each view through `HoleTableStoredUpdate` in its own call (each its own transaction in the add-in); when every view updates, it closes with `Updated`; when any fails, it stays open, lists the failed views with their message, and Later reads "Close". The result text for the log is a property.
- `src/InventorAddin.Core/Settings/AddinSettings.cs`: `CheckHoleTablesAutomatically`, default `true` (a settings file without it loads as on), in `Equals` and the hash. `SettingsViewModel`: the property, a label constant ("Check hole _tables when a drawing opens or updates", check the Holes section's access keys) and a note constant ("When a drawing's hole tables no longer match the model, says which views and offers Update."). `SettingsSummaries.Holes`: add "hole table check off" when off, if the summary has room; keep existing summary tests passing.
- `src/InventorAddin.Core/HoleTable/HoleTableMessages.cs`: the new texts.
- `src/InventorAddin.Core/ViewModels/HoleTableWizardViewModel.cs`: use the two moved pieces.
- Tests in `tests/InventorAddin.Core.Tests` for each new class and the setting (load without the key gives true; round trip).
- `docs/features/04-hole-table-wizard.md`: extend the Update bullet in "Decided behaviour" with the automatic check (Aidan, 2026-11-20; tasks 175, 176), and add the defaults above under "Open questions" as numbered defaults (quiet time; could-not-check does not prompt; Later is per session; closing the wizard counts as Later).

Out of scope: anything under `src/InventorAddin` (task 176).

## Acceptance criteria

- [ ] The wizard's existing tests pass unchanged (or with only the moved code's tests moved).
- [ ] `HoleTableStoredCheck.Run` calls only `GetKeyedViews`, `ReadState` and `Analyze` (a fake source that throws on any write method proves it).
- [ ] `HoleTableAutoCheck` tests: activation makes a check due after the quiet time and not before; a second event restarts the quiet time; setting off: never due; not active, editing something else, command running or an AWB window open: stays due, runs once the condition clears; an update while not active runs on the next activation; prompt only when something is out of date; Later then the same result: no prompt; Later then a different reason or view: prompt; Update now then the same result again: prompt; the wizard's close result counts as Later; a closed drawing is forgotten; drawing names compare ignoring case.
- [ ] Prompt view-model tests: Update now with every view succeeding closes with `Updated`; with one failing stays open, lists it, Later says Close; Later closes with `Later`.
- [ ] Setting tests: default true, a JSON file without the key loads true, the value round-trips.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes.

## Notes for the implementer

- Core has no Inventor or WPF reference. Match `HoleTableWizardViewModel`'s use of `ObservableObject` and `RelayCommand`.
- Look at `HoleSessionRules.EndsOnDeactivate` and `StartupTabPlan` for how other add-in event logic was kept testable in Core.
- Check how `HoleDiagramExpanded` gets its `true` default through `SettingsStore` before adding the new setting.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** none for this task alone; task 176 carries the checks.

## Follow-ups

Things noticed but not done.
