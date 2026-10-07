# 023: Settings and About view-model text (Core)

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (The three windows: Settings, About)
- **Status:** done
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 021, 022, 024, 025

## Goal

The Settings and About view-models offer the text their restyled windows show, and a failed Settings save no longer leaves its error on screen after the user edits again (follow-up from task 008).

## Scope

- `src/InventorAddin.Core/ViewModels/SettingsViewModel.cs`
- `src/InventorAddin.Core/ViewModels/AboutViewModel.cs`
- `tests/InventorAddin.Core.Tests/ViewModels/SettingsViewModelTests.cs`
- `tests/InventorAddin.Core.Tests/ViewModels/AboutViewModelTests.cs`

Out of scope:

- The windows (026 rewrites About, 028 rewrites Settings). They keep binding to the existing properties until then, so keep `VersionLine`, `BuildDateLine`, `InventorVersionLine` and `LogFolderLine`; 026 removes them once nothing binds to them.

## Acceptance criteria

Settings

- [x] `HeaderTitle` is `"Settings"` and `HeaderSubline` is `Branding.ProductName`
- [x] `DefaultDesignerHelp` is `"Filled in as Designer in Part Properties when a file has none."` (the text now typed in the window's XAML), as a public constant and a property
- [x] Editing either setting after a failed save clears the save error: `ErrorText` falls back to the validation error or null, and `PropertyChanged` is raised for `ErrorText` and `HasError` when they change. A validation error is still shown as now
- [x] Tests: a failed save shows the error; a following edit clears it; a following edit that is itself invalid shows the validation error instead; an edit back to the saved value also clears it

About

- [x] `HeaderSubline` is `"Version {Version}"` (`Version unknown` when unknown)
- [x] `InventorRelease` is the release year from the major version: major + 1996, so 30 gives `2026`; `unknown` when the major version is null or below 13 (Inventor 2009). The row is labelled "Inventor version" in the window
- [x] `BuildDate` and `LogFolder` stay as they are and are the values of the Built and Log folder rows
- [x] Tests for `HeaderSubline` and `InventorRelease`, including 30, 29, 13, 12, 0, negative and null
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- In `SettingsViewModel`, clearing the save error on edit belongs where an edit is already handled (`OnEditChanged`, or the setters), through `SetSaveError(null)` so the change notifications stay in one place.
- The year mapping (major + 1996) holds for every Inventor release since 2009. Showing the year is a default logged in `DECISIONS.md`.
- Do not change how the version or build date are parsed.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 662 passed, 0 failed, 0 skipped.
- **Changed behaviour in an existing test:** `ValidationError_TakesPrecedenceOverSaveError_AndSaveErrorReturnsWhenFixed` asserted that a save error came back once a validation error was fixed. That contradicts this task (any edit clears the save error), so it was replaced by `InvalidEditAfterFailedSave_ShowsValidationError` and `FixingInvalidEditAfterFailedSave_DoesNotBringBackSaveError`.
- **Notes:** `InventorRelease` adds in `long`, so an absurd major version cannot overflow into a negative year. Setting a setting to the value it already has is not an edit and leaves a save error in place.
- **Not compiled (changed under `src/InventorAddin`):** none
- **Inventor API members not confirmed:** none
- **Manual checklist for Inventor:** none (covered by 026 and 028)

## Follow-ups

Things noticed but not done.

None.
