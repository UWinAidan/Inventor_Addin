# 005: Add-in startup services and command error reporting

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Shared plumbing)
- **Status:** todo
- **Depends on:** 002, 003
- **Needs:** cloud (writes add-in code that is not compiled here; Aidan builds it)
- **Parallel-safe with:** none

## Goal

When Inventor loads the add-in, it finds its data folder, opens the log and loads settings. Every command's unhandled exception is logged in full and shown to the user as a short message, instead of a raw stack trace in a message box.

## Scope

- `src/InventorAddin/AddinServices.cs` (new: holds the data folder path, `ILog` and the loaded `AddinSettings` for the session)
- `src/InventorAddin/StandardAddInServer.cs` (create the services in `Activate`, log start and stop)
- `src/InventorAddin/Commands/AddinCommand.cs` (error reporting through the log)
- `src/InventorAddin.Core/Logging/ErrorMessages.cs` (new: builds the short user-facing text)
- `tests/InventorAddin.Core.Tests/Logging/ErrorMessagesTests.cs` (new)

Out of scope:

- Transactions and command types (task 006)
- Using the developer setting in the ribbon (task 007)
- Settings or About windows

## Acceptance criteria

- [ ] The data folder is `%APPDATA%\InventorWorkflowTools`, built with `System.Environment.GetFolderPath(SpecialFolder.ApplicationData)` (aliased, see CLAUDE.md namespace clashes)
- [ ] `Activate` creates the `FileLog` first, then loads settings through `SettingsStore`, and logs each settings warning with `Warn`
- [ ] `Activate` logs one `INFO` line with the add-in assembly version and `InventorHost.MajorVersion`. `Deactivate` logs one `INFO` line
- [ ] A failure while creating services must not stop the add-in loading: fall back to `NullLog` and default settings
- [ ] `AddinCommand.OnExecute` logs the command name at start (`INFO`) and, on exception, logs `ERROR` with the full exception and shows a message box with the text from `ErrorMessages`
- [ ] `ErrorMessages` (Core) returns: the command's display name, the exception's message (inner-most message if the outer one is a `TargetInvocationException` or `AggregateException`), and a last line telling the user where the log file is. Tested in Core
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes
- [ ] The verification section lists every changed add-in file as not compiled

## Notes for the implementer

- Keep `AddinServices` simple: a static class initialised in `Activate`, like `InventorHost`, is fine and matches the existing code. Reset it in `Deactivate`.
- Error message boxes stay WinForms `MessageBox`, as now. Do not introduce WPF in this task.
- The `INFO` line on each command start is useful for diagnosis; do not log on every UI event beyond that.
- Inventor API used here is only what exists already (`InventorHost.MajorVersion`, `ApplicationAddInSite.Application`). No new Inventor members should be needed.

## Verification

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Close Inventor, build, start Inventor.
  2. Open `%APPDATA%\InventorWorkflowTools\workflowtools.log`. There is an `INFO` start line with the add-in version and Inventor major version 30.
  3. With no document open, click *Export Libraries*. It works as before and the log shows an `INFO` line for it.
  4. Open a part and click *Export Model Data*. It works as before.
  5. Put `{ not json` in `settings.json`, restart Inventor. The add-in loads, `settings.json.bad` exists, and the log has a `WARN` line.
  6. Close Inventor. The log ends with the stop line.

## Follow-ups
