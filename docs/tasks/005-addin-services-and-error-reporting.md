# 005: Add-in startup services and command error reporting

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Shared plumbing)
- **Status:** done
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

- [x] The data folder is `%APPDATA%\InventorWorkflowTools`, built with `System.Environment.GetFolderPath(SpecialFolder.ApplicationData)` (aliased, see CLAUDE.md namespace clashes)
- [x] `Activate` creates the `FileLog` first, then loads settings through `SettingsStore`, and logs each settings warning with `Warn`
- [x] `Activate` logs one `INFO` line with the add-in assembly version and `InventorHost.MajorVersion`. `Deactivate` logs one `INFO` line
- [x] A failure while creating services must not stop the add-in loading: fall back to `NullLog` and default settings
- [x] `AddinCommand.OnExecute` logs the command name at start (`INFO`) and, on exception, logs `ERROR` with the full exception and shows a message box with the text from `ErrorMessages`
- [x] `ErrorMessages` (Core) returns: the command's display name, the exception's message (inner-most message if the outer one is a `TargetInvocationException` or `AggregateException`), and a last line telling the user where the log file is. Tested in Core
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes
- [x] The verification section lists every changed add-in file as not compiled

## Notes for the implementer

- Keep `AddinServices` simple: a static class initialised in `Activate`, like `InventorHost`, is fine and matches the existing code. Reset it in `Deactivate`.
- Error message boxes stay WinForms `MessageBox`, as now. Do not introduce WPF in this task.
- The `INFO` line on each command start is useful for diagnosis; do not log on every UI event beyond that.
- Inventor API used here is only what exists already (`InventorHost.MajorVersion`, `ApplicationAddInSite.Application`). No new Inventor members should be needed.

## Verification

- **Ran:** `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 144 passed, 0 failed, 0 skipped (22 of them new in `ErrorMessagesTests`). Cloud session on Linux. `src/InventorAddin` was not built (`CLAUDE_CODE_REMOTE=true`, no interop DLL). As an extra check only, `AddinServices.cs` (which has no Inventor reference) was compiled with warnings as errors in a scratch project outside the repo, against Core and a stub `StandardAddInServer`, and run on Linux with `{ not json` in `settings.json`: the file was renamed to `settings.json.bad`, the log got one `WARN` line, and a following `Info` entry was written. This does not count as compiling the add-in.
- **Not compiled (changed under `src/InventorAddin`):**
  - `src/InventorAddin/AddinServices.cs` (new; see the scratch check above, not built as part of the add-in)
  - `src/InventorAddin/StandardAddInServer.cs`
  - `src/InventorAddin/Commands/AddinCommand.cs`
- **Inventor API members not confirmed:** none. No new Inventor members are used. The code uses `ApplicationAddInSite.Application` (already in `Activate`), `InventorHost.MajorVersion` and `InventorHost.Initialize`/`Shutdown` (existing code), and `ComSafe.Get` (existing). To rule out a name clash with the Inventor interop, which cannot be checked here, `AddinCommand.cs` calls `ErrorMessages` by its full name (`InventorAddin.Core.Logging.ErrorMessages`). Locals are declared with `var`, so `ILog` and `Version` are not named in files that import `Inventor`.
- **Manual checklist for Inventor:**
  1. Close Inventor, build, start Inventor.
  2. Open `%APPDATA%\InventorWorkflowTools\workflowtools.log`. There is an `INFO` start line, `Workflow Tools <assembly version> started in Inventor major version 30.` (the assembly version is `1.0.0.0` until a version is set in the csproj).
  3. With no document open, click *Export Libraries*. It works as before and the log shows `INFO Command WorkflowTools_ExportLibraries (Export Libraries) started.`
  4. Open a part and click *Export Model Data*. It works as before, with a matching `INFO` line.
  5. Error path: in the ZeroDoc environment (no document open), *Export Model Data* is not on the ribbon, so there is no button that fails on demand today. If you want to see the error path, temporarily add `throw new InvalidOperationException("Test failure");` to the start of `ExportLibrariesCommand.Execute`, build and click it. The message box title is "Export Libraries" and it shows three parts: `Export Libraries failed.`, `Test failure`, and `Details are in the log file: <path>`, with no stack trace. The log has an `ERROR Command WorkflowTools_ExportLibraries failed.` line followed by the full exception. Remove the throw afterwards.
  6. Put `{ not json` in `settings.json`, restart Inventor. The add-in loads, `settings.json.bad` exists, and the log has a `WARN` line.
  7. Close Inventor. The log ends with `INFO Workflow Tools stopped.`

Notes on behaviour the criteria left open:

- `ErrorMessages.CommandFailed(displayName, ex, logFilePath)` returns three parts separated by blank lines: `{name} failed.`, the message, and `Details are in the log file: {path}`. With no log file (the `NullLog` fallback), the last line is `No log file is available for the details.` A blank display name becomes "The command".
- "Inner-most message" is read as: `TargetInvocationException` and `AggregateException` are unwrapped repeatedly until the first exception that is not one of them (the first inner exception for an `AggregateException` with several). A non-wrapper exception keeps its own message even if it has an inner exception, because its own message usually says more to the user. An empty message falls back to the exception type name. The message is trimmed.
- `AddinServices` is a static class in namespace `InventorAddin`, like `InventorHost`. Before `Initialize`, after `Shutdown`, and on failure it holds `NullLog.Instance`, default `AddinSettings`, and null `DataFolder` and `LogFilePath`, so callers never check for null. If the `FileLog` was created and a later step fails, the real log is kept and the failure is logged as `ERROR` with default settings; `NullLog` is used only when the log itself could not be created. `GetFolderPath` returning an empty string counts as a failure.
- The Inventor version in the start line is read through `ComSafe.Get`, so a COM failure prints "unknown" and does not stop loading.
- `Deactivate` disposes the ribbon, logs the stop line, then resets `AddinServices` and `InventorHost`.
- The start of each command is logged with the internal and display names. Ribbon creation failures in `Activate` are not caught or logged (unchanged behaviour; see Follow-ups).

## Follow-ups

- `AddinServices.Settings` has a private setter. Task 008 ("After a save, `AddinServices` holds the new settings") needs a way to replace it, for example an internal `UpdateSettings(AddinSettings)`, and needs to decide how to show a `SettingsStore.Save` exception to the user (carried over from task 002).
- `RibbonSetup.Create()` failures in `Activate` are not logged; Inventor reports them itself. Task 007 (which rebuilds the ribbon) could wrap ribbon creation and log the exception through `AddinServices.Log`.
- The assembly has no `Version` in the csproj, so the start line shows `1.0.0.0`. Task 009 expects `0.1.0`; it should set the version.
