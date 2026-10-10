# 040: New Part dialog view-model and creation properties (Core)

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (Written on creation; Commands: New Part, Save As Copy)
- **Status:** done
- **Depends on:** 033, 034, 037
- **Needs:** cloud
- **Parallel-safe with:** 039, 041, 042

## Goal

Core has everything the New Part dialog decides: the fields, their validation, the file name preview, and the exact properties to write on the new file. The same pieces serve Save As Copy. Task 043 adds the window and the Inventor side.

## Scope

- `src/InventorAddin.Core/Numbering/CreationProperties.cs` (new)
- `src/InventorAddin.Core/Numbering/NewFileKind.cs` (new)
- `src/InventorAddin.Core/ViewModels/NewPartViewModel.cs` (new)
- `tests/InventorAddin.Core.Tests/Numbering/CreationPropertiesTests.cs` (new)
- `tests/InventorAddin.Core.Tests/ViewModels/NewPartViewModelTests.cs` (new)

Out of scope:

- Assigning the number (the command calls `RegisterStore`), templates, and the window (043).

## Acceptance criteria

Kinds

- [x] `enum NewFileKind { Part, SheetMetalPart, Assembly }` with display names `Part`, `Sheet metal part`, `Assembly`, the file extension (`.ipt`, `.ipt`, `.iam`), and the matching `DocumentKind`

Creation properties

- [x] `CreationProperties.Build(PartNumber number, string partName, string? partType, string? defaultDesigner)` returns the `PropertyWrite` list, in this order: Part Number, Description (the normalised name with spaces), Designer and Detailer (both `defaultDesigner`, normalised; both left out when blank), Part Type (left out when blank). Locations from `PartPropertyStorage`; Part Type in the list's spelling (`PartTypes.Find`)
- [x] Tests: every property present; blank designer; blank part type; a lower-case part type

View-model

- [x] Constructor takes the default designer, default units (`UnitsSystem`), the default folder, and an `Action<NewPartRequest>`-style callback or returns the request from a `Create` method the window calls (pick one and follow `PartPropertiesViewModel`'s pattern for commands)
- [x] Fields: `Kind` (default Part), `PartType` (with the same options list as Part Properties: blank first, then `PartTypes.All`), `PartName`, `Units`, `Folder`
- [x] `PartType` is pre-filled from `PartTypes.DefaultFor(kind)` (A for an assembly) when the user has not chosen one, and follows a change of `Kind` until the user picks a type
- [x] `FileNamePreview`: `AWB-?????,{name}{extension}` (the placeholder is a constant), or empty when the name is invalid
- [x] `ValidationMessage`: the first of `PartFileName.ValidateName`, `"Choose a folder."` when blank, `PartFileName.ValidatePath` (using the number placeholder's length, which equals a real number's). `CanCreate` is true only with no message
- [x] `CreateCommand` produces a `NewPartRequest(NewFileKind Kind, string PartName, string PartType, UnitsSystem Units, string Folder)` with the normalised name and trimmed folder; the window closes on it
- [x] Header text constants for the window: title `New Part`, subline `Branding.ProductName` (as the Settings header does)
- [x] Tests for defaults, the part type following the kind, validation order, the preview, and the request
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- What is written on creation is Aidan's list (2026-10-10). Leaving Designer and Detailer out of the dialog is a Default.
- Reuse `PartTypeOption` from `PartPropertiesViewModel.cs`. Leave it where it is: task 039 edits that file and may run at the same time.
- The folder default (active document's folder or project workspace) is decided by the add-in in 043; the view-model just takes a string.
- Whether the target file already exists is checked by the command at Create time, not here (needs the real number).

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 1198 passed, 0 failed, 0 skipped (53 of them new in `CreationPropertiesTests` and `NewPartViewModelTests`). Run in the cloud (`CLAUDE_CODE_REMOTE=true`).
- **Not compiled (changed under `src/InventorAddin`):** none; nothing under `src/InventorAddin` was changed.
- **Inventor API members not confirmed:** none; no Inventor API is used.
- **Manual checklist for Inventor:** none (covered by 043)

Choices made inside the brief:

- Command pattern follows `PartPropertiesViewModel`: `CreateCommand` stores `Request` (a `NewPartRequest`) and raises `CloseRequested`; the window closes on that event and the command reads `Request`.
- `NewPartRequest` and a `NewFileKindOption` record (for the File type dropdown) live in `NewPartViewModel.cs`. The view-model also exposes `KindOptions` and `UnitsOptions` (reusing the `UnitsOption` record from `SettingsViewModel.cs`) for 043's dropdowns.
- The default designer is shown read-only as `Designer` (trimmed); it is not in the request, because the command reads `DefaultDesigner` from Settings when it calls `CreationProperties.Build`.
- Setting `PartType` to the value it already holds is not a choice: it keeps following the kind. Any other value, blank included, stops it following.
- `CreationProperties.Build` throws `ArgumentException` for a name that fails `PartFileName.ValidateName`, as `PartFileName.Build` does. A part type not in the list is written trimmed, as Part Properties keeps it.
- `NewFileKind` helpers are extension methods in `NewFileKinds` (`DisplayName()`, `Extension()`, `DocumentKindOf()`, `All`); the display name comes from `DocumentKindNames`.

## Follow-ups

Things noticed but not done.

- The Metric/Imperial option list is now built in both `SettingsViewModel` (private) and `NewPartViewModel`. A shared `UnitsOption.All` would keep the display text in one place. Not done because 039/042 may be editing nearby files.
- The folder is only checked for blank and path length. A typed folder with characters Windows does not allow, or a relative path, is not caught until the command saves. 043 may want a check like the Settings register folder's (full path, invalid characters), or to make the folder Browse-only.
