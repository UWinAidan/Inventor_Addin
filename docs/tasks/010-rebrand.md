# 010: Rebrand to "AWB Addin"

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md`
- **Status:** done
- **Depends on:** 009
- **Needs:** cloud (writes add-in code that is not compiled here; Aidan builds it)
- **Parallel-safe with:** none

## Goal

The add-in carries Aidan's own brand everywhere a user or a file system sees it, and the names live in one place so they can be changed again cheaply. Done now, before anything depends on the old names.

## The names

| What | Old | New |
|---|---|---|
| Ribbon tab | Workflow Tools | AWB Addin |
| Product name (About title, `.addin` display name, log lines, error text) | Inventor Workflow Tools | AWB Addin |
| Internal id prefix (tab, panels, commands) | `WorkflowTools` | `Awb` |
| Data folder under `%APPDATA%` and the temp export folder | `InventorWorkflowTools` | `AwbAddin` |
| Log file | `workflowtools.log` | `awbaddin.log` |

If Aidan has changed the tab or product name in this table before the task runs, use what the table says.

## Scope

- `src/InventorAddin.Core/Branding.cs` (new: the names above as constants)
- Every place under `src/` and `tests/` that hard-codes one of the old names, changed to use `Branding`. Find them with a search for `WorkflowTools`, `Workflow Tools` and `workflowtools`
- `src/InventorAddin/InventorAddin.addin` (display name and description; this file is static XML, so edit the text)
- `README.md`, `CLAUDE.md` ("Our names" line and the title), `docs/features/01-ribbon-and-shell.md`, `docs/PLAN.md` (add the decision to "Decisions made")
- Tests that assert on the old names

Out of scope:

- Project, assembly, namespace and file names (`InventorAddin`, `InventorAddin.Core`). They stay.
- The add-in GUID. It stays.
- Migrating an existing data folder. Only Aidan's machine has one; the checklist tells him to delete it.
- Icons or any visual branding.

## Acceptance criteria

- [x] `Branding` in Core holds the tab name, product name, id prefix, data folder name and log file name. Nothing else in `src/` repeats those strings; ids are built from the prefix
- [x] A search of `src/`, `tests/`, `README.md` and `CLAUDE.md` for `WorkflowTools`, `Workflow Tools` and `workflowtools` finds nothing
- [x] Task files for finished tasks (`docs/tasks/001` to `009`) are left as written; they are history
- [x] The `.addin` file shows the new display name, and its `ClassId`, `ClientId` and `Assembly` path are unchanged
- [x] Layout and log tests pass with the new names
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists every changed add-in file as not compiled

## Notes for the implementer

- Changing a tab, panel or command id makes Inventor treat it as new. That is intended here.
- `RibbonSetup.cs` had its own copies of the tab and panel ids before task 007. If any remain, remove them in favour of the Core constants.

## Verification

- **Ran:** in the cloud (`CLAUDE_CODE_REMOTE=true`).
  - `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings, 0 errors.
  - `dotnet test tests/InventorAddin.Core.Tests`: 214 passed, 0 failed, 0 skipped. Includes the new `BrandingTests` (literal names and the ids Inventor sees) and the layout, log, About and error-message tests, now building their expected names from `Branding`.
  - Case-insensitive search of `src/`, `tests/`, `README.md` and `CLAUDE.md` (excluding `bin/` and `obj/`) for `workflow ?tools` (covers `WorkflowTools`, `Workflow Tools`, `workflowtools`, `InventorWorkflowTools`, `workflow tools`): no matches.
  - Search of `src/` for `AWB`, `Awb` and `awbaddin` outside `Branding.cs`: only the `.addin` `DisplayName`, which is static XML (commented to match `Branding.ProductName`).
  - `src/InventorAddin` was not built: it needs `Autodesk.Inventor.Interop.dll`, which the cloud does not have.
- **Not compiled (changed under `src/InventorAddin`):**
  - `src/InventorAddin/AddinServices.cs` (removed `AddinServices.DataFolderName`; uses `Branding.DataFolderName`)
  - `src/InventorAddin/StandardAddInServer.cs` (start and stop log lines use `Branding.ProductName`)
  - `src/InventorAddin/Commands/ShellCommands.cs` (Settings and About descriptions)
  - `src/InventorAddin/Commands/ExportDataCommands.cs` (temp export folder is `%TEMP%\AwbAddin`)
  - `src/InventorAddin/UI/SettingsWindow.xaml` and `SettingsWindow.xaml.cs` (title attribute removed from XAML; set in the constructor as "AWB Addin Settings")
  - `src/InventorAddin/UI/AboutWindow.xaml` and `AboutWindow.xaml.cs` (same; "About AWB Addin")
  - `src/InventorAddin/InventorAddin.addin` (`DisplayName` only; `ClassId`, `ClientId`, `Assembly` and `Description` unchanged)
  - `RibbonSetup.cs` needed no change: it already uses `RibbonIds` from Core and has no copies of the tab or panel ids.
- **Inventor API members not confirmed:** none. No Inventor API member was added or changed. The add-in now references `InventorAddin.Core.Branding` in files that also have `using Inventor;`; no `Inventor.Branding` type is known, but an ambiguity would show up as a compile error on Windows.
- **Manual checklist for Inventor:**
  1. Close Inventor. Delete `%APPDATA%\InventorWorkflowTools` if it exists. Build.
  2. Start Inventor and open a part. The tab reads "AWB Addin" and there is no "Workflow Tools" tab. If an old tab is still shown, note it under Follow-ups.
  3. Tools > Add-Ins lists "AWB Addin" as loaded.
  4. `%APPDATA%\AwbAddin\awbaddin.log` exists with a start line naming "AWB Addin".
  5. Settings and About open, and About shows "AWB Addin". The Settings window title reads "AWB Addin Settings" and the About window title reads "About AWB Addin".
  6. With developer tools on, *Export Model Data* writes its JSON under `%TEMP%\AwbAddin`.

## Follow-ups

- `.claude/agents/implementer.md` and `.claude/agents/reviewer.md` still say "Inventor Workflow Tools add-in". They were outside this task's file list.
