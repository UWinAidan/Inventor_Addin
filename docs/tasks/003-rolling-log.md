# 003: Rolling log file

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Shared plumbing: Logging)
- **Status:** done
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

- [x] `ILog` has `Info(string message)`, `Warn(string message)` and `Error(string message, Exception? ex = null)`
- [x] `FileLog` takes the log folder, and optionally a maximum file size and number of old files to keep (defaults: 1 MB and 5). It writes to `workflowtools.log` in that folder and creates the folder if needed
- [x] Each entry is one line starting with a local timestamp (`yyyy-MM-dd HH:mm:ss.fff`) and the level (`INFO`, `WARN`, `ERROR`). An exception is written after its line in full (`ex.ToString()`)
- [x] When a write would take the file past the maximum size, the file rolls: `workflowtools.log` becomes `workflowtools.1.log`, `.1` becomes `.2`, and so on; anything beyond the keep count is deleted
- [x] Logging never throws to the caller. If the file cannot be written (for example, it is locked), the entry is dropped
- [x] Writes are safe from more than one thread (a lock is enough)
- [x] The clock can be injected for tests (a `Func<DateTime>` or `TimeProvider`)
- [x] A `NullLog` implementation exists for code that runs before the log is set up and for tests
- [x] Tests cover: line format, exception output, rollover order, keep count, and a locked file not throwing
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- File-scoped namespace `InventorAddin.Core.Logging`.
- Open, append, close on each write. The volume is low and it means Aidan can open the file while Inventor runs.
- The folder is passed in. The add-in will pass the same folder as the settings (`%APPDATA%\InventorWorkflowTools`), but Core does not know that.
- For the locked-file test, hold the file open with `FileShare.None` from the test. If that does not lock on Linux, test the "never throws" path another way (for example, a folder path that is a file) and note it.

## Verification

- **Ran:** `dotnet build src/InventorAddin.Core`: succeeded, 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 65 passed, 0 failed, 0 skipped (31 of them new in `FileLogTests`). Run in a cloud session on Linux only; not yet run on Windows.
- **Not compiled (changed under `src/InventorAddin`):** none. No add-in files were changed.
- **Inventor API members not confirmed:** none. No Inventor API used.
- **Manual checklist for Inventor:** none, Core only.

Notes on behaviour the criteria left open:

- Entry format is `{timestamp} {LEVEL} {message}` with single spaces and no padding, for example `2026-01-02 03:04:05.006 WARN text`. The timestamp is formatted with the invariant culture so it reads the same under any Windows regional setting. Lines end with `Environment.NewLine` and the file is UTF-8 without a BOM.
- Line breaks inside a message are replaced with spaces so each entry's first line stays one line. A null message is written as empty. Exception text (`ex.ToString()`) follows on its own lines, unchanged.
- The size check counts the bytes of the whole entry, exception text included. Reaching the maximum exactly does not roll; only going past it does. An entry larger than the maximum written to an empty file is written as is (no roll to an empty file).
- `keepCount: 0` is allowed and means the current file is deleted on rollover. On each rollover, any `workflowtools.N.log` with N above the keep count is deleted, so files left by an earlier, larger keep count are cleaned up. Other files in the folder are left alone.
- If the rollover itself fails (for example, a rolled file is held open by another program), the entry is still appended to the current file, which grows past the limit until a later rollover succeeds. If the append fails, the entry is dropped. Any exception in the write path, including from the injected clock, is swallowed.
- The folder is created on the first write, not in the constructor, so constructing a `FileLog` never touches the disk and cannot throw for I/O reasons. The constructor does throw `ArgumentException`/`ArgumentOutOfRangeException` for an empty folder, a non-positive size or a negative keep count (programming errors).
- Each write opens the file with `FileShare.ReadWrite | FileShare.Delete` and closes it again, so the file can be opened while Inventor runs. The lock is per `FileLog` instance; two instances on the same folder in one process are not serialised against each other (task 005 should create one instance).
- Clock injection is a `Func<DateTime>` (default `DateTime.Now`).
- `NullLog` lives in `ILog.cs` (scope listed no separate file), as `NullLog.Instance`.

Platform notes:

- The locked-file tests hold the file with `FileShare.None` and assert only that logging does not throw and resumes after release; they do not assert whether the locked entry was dropped. On Linux, .NET's advisory lock did cause the entry to be dropped (checked with a scratch program, not in the tests). Windows behaviour is expected to be the same but has not been run.
- The "never throws" path is also covered without locking: the folder path is an existing file, and the log path is an existing directory. These do not depend on exact exception types.
- One test sets the thread culture to `ar-SA` to check the invariant timestamp. It passed on Linux; that culture exists on Windows.

## Follow-ups

- Task 005: create one `FileLog` for the settings folder at startup and pass `ILog` (or `NullLog.Instance` before that) to code that needs it. Log `SettingsLoadResult.Warnings` through it.
