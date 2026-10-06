# 003: Rolling log file

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Shared plumbing: Logging)
- **Status:** todo
- **Depends on:** 001
- **Needs:** cloud
- **Parallel-safe with:** 002, 004

## Goal

Core has a small file logger that rolls over by size, so a failure inside Inventor can be diagnosed afterwards from a file next to the settings.

## Scope

- `src/InventorAddin.Core/Logging/ILog.cs` (new)
- `src/InventorAddin.Core/Logging/FileLog.cs` (new)
- `tests/InventorAddin.Core.Tests/Logging/FileLogTests.cs` (new)

Out of scope:

- Anything under `src/InventorAddin` (task 005 creates the log at startup)
- Logging libraries. No new packages.

## Acceptance criteria

- [ ] `ILog` has `Info(string message)`, `Warn(string message)` and `Error(string message, Exception? ex = null)`
- [ ] `FileLog` takes the log folder, and optionally a maximum file size and number of old files to keep (defaults: 1 MB and 5). It writes to `workflowtools.log` in that folder and creates the folder if needed
- [ ] Each entry is one line starting with a local timestamp (`yyyy-MM-dd HH:mm:ss.fff`) and the level (`INFO`, `WARN`, `ERROR`). An exception is written after its line in full (`ex.ToString()`)
- [ ] When a write would take the file past the maximum size, the file rolls: `workflowtools.log` becomes `workflowtools.1.log`, `.1` becomes `.2`, and so on; anything beyond the keep count is deleted
- [ ] Logging never throws to the caller. If the file cannot be written (for example, it is locked), the entry is dropped
- [ ] Writes are safe from more than one thread (a lock is enough)
- [ ] The clock can be injected for tests (a `Func<DateTime>` or `TimeProvider`)
- [ ] A `NullLog` implementation exists for code that runs before the log is set up and for tests
- [ ] Tests cover: line format, exception output, rollover order, keep count, and a locked file not throwing
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- File-scoped namespace `InventorAddin.Core.Logging`.
- Open, append, close on each write. The volume is low and it means Aidan can open the file while Inventor runs.
- The folder is passed in. The add-in will pass the same folder as the settings (`%APPDATA%\InventorWorkflowTools`), but Core does not know that.
- For the locked-file test, hold the file open with `FileShare.None` from the test. If that does not lock on Linux, test the "never throws" path another way (for example, a folder path that is a file) and note it.

## Verification

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none expected
- **Inventor API members not confirmed:** none expected
- **Manual checklist for Inventor:** none, Core only.

## Follow-ups
