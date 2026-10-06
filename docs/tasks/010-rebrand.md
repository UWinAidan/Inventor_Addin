# 010: Rebrand to "AWB Addin"

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md`
- **Status:** todo
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

- [ ] `Branding` in Core holds the tab name, product name, id prefix, data folder name and log file name. Nothing else in `src/` repeats those strings; ids are built from the prefix
- [ ] A search of `src/`, `tests/`, `README.md` and `CLAUDE.md` for `WorkflowTools`, `Workflow Tools` and `workflowtools` finds nothing
- [ ] Task files for finished tasks (`docs/tasks/001` to `009`) are left as written; they are history
- [ ] The `.addin` file shows the new display name, and its `ClassId`, `ClientId` and `Assembly` path are unchanged
- [ ] Layout and log tests pass with the new names
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists every changed add-in file as not compiled

## Notes for the implementer

- Changing a tab, panel or command id makes Inventor treat it as new. That is intended here.
- `RibbonSetup.cs` had its own copies of the tab and panel ids before task 007. If any remain, remove them in favour of the Core constants.

## Verification

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Close Inventor. Delete `%APPDATA%\InventorWorkflowTools` if it exists. Build.
  2. Start Inventor and open a part. The tab reads "AWB Addin" and there is no "Workflow Tools" tab. If an old tab is still shown, note it under Follow-ups.
  3. Tools > Add-Ins lists "AWB Addin" as loaded.
  4. `%APPDATA%\AwbAddin\awbaddin.log` exists with a start line naming "AWB Addin".
  5. Settings and About open, and About shows "AWB Addin".

## Follow-ups
