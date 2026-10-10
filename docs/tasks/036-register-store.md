# 036: Register store: shared folder, local copy, lock, offline (Core)

- **Milestone:** M2
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md` (The register)
- **Status:** todo
- **Depends on:** 035
- **Needs:** cloud
- **Parallel-safe with:** 034, 037, 038, 042

## Goal

One Core class assigns part numbers and keeps the register on disk: the shared file on the NAS when it can be reached, the local copy and pending entries when it cannot, a lock while writing, and a sync that reports duplicates. The add-in only constructs it and shows its results.

## Scope

- `src/InventorAddin.Core/Numbering/RegisterStore.cs` (new)
- `src/InventorAddin.Core/Numbering/RegisterAssignment.cs` (new: request and result types)
- `src/InventorAddin.Core/Numbering/IRegisterEnvironment.cs` (new: clock, user, computer, folder probe; see Notes)
- `tests/InventorAddin.Core.Tests/Numbering/RegisterStoreTests.cs` (new)

Out of scope:

- Settings (037) and any add-in wiring (039).

## Acceptance criteria

Construction and files

- [ ] `RegisterStore(string sharedFolder, string localFolder, IRegisterEnvironment environment, ILog? log = null)`. File names are constants: `SharedFileName = "part-register.csv"`, `LockFileName = "part-register.lock"`, `LocalFileName = "part-register.csv"`, `PendingFileName = "pending.csv"`
- [ ] The local folder is created when needed; the shared folder never is (a missing shared folder means the NAS is unreachable)

Reading

- [ ] `PartRegister ReadLocal()`: the local copy plus the pending entries, never touching the shared folder. A missing file is an empty register. An unreadable file gives an empty register and a logged warning, never an exception
- [ ] `bool HasNumber(string? partNumberProperty)` is `ReadLocal().HasNumber(...)`

Assigning

- [ ] `RegisterAssignment Assign(RegisterRequest request)`, where the request carries name, part type, kind and file path. The result carries the assigned `PartNumber`, `bool Offline`, and the duplicates found by the sync done on the way
- [ ] Reachable: takes the lock (creates the lock file with `FileMode.CreateNew`; retries for up to `LockWait` = 5 s; a lock file older than `StaleLockAge` = 2 min is deleted and logged), reads the shared file, merges pending entries (`PartRegister.Merge`), picks `NextNumber()`, appends the new entry, writes the shared file through a temporary file in the same folder that then replaces it, deletes the pending file, writes the local copy, and deletes the lock file in a `finally`
- [ ] Unreachable (see probe below): picks the next number from `ReadLocal()`, appends the entry to the pending file, and returns `Offline = true`
- [ ] Could not take the lock within `LockWait`: throws `RegisterBusyException` with a message saying another PC is writing the register; nothing is assigned
- [ ] A shared file that exists but cannot be parsed: throws `RegisterUnreadableException` naming the file; nothing is assigned and nothing is overwritten
- [ ] `AssignedAt`, `AssignedBy` and `Computer` come from the environment

Syncing

- [ ] `SyncResult Sync()`: when reachable, the same lock, merge, write and local refresh as Assign without adding an entry; when unreachable, does nothing and says so in the result (`Reached = false`)

Reachability

- [ ] Reachability is decided by `IRegisterEnvironment.ProbeFolder(string folder, TimeSpan timeout)`. The real implementation (`RegisterEnvironment`, also in this task) runs `Directory.Exists` on a background task and treats no answer within the timeout as unreachable. The timeout is `ProbeTimeout` = 3 s
- [ ] Tests use a fake environment with a fixed clock and a probe the test controls, and real temporary folders for the files

Tests

- [ ] First assignment in an empty shared folder gives `AWB-10001` and writes both files; the next gives `AWB-10002`
- [ ] Offline assignment uses local copy + pending; two offline assignments give consecutive numbers; a later reachable Assign syncs them first and then assigns after the highest
- [ ] A pending number another PC also used is reported as a duplicate and both rows end up in the shared file
- [ ] A stale lock is cleared; a fresh lock held by the test makes Assign throw `RegisterBusyException` (use a short `LockWait` through a constructor option so the test is fast)
- [ ] A corrupt shared file throws and is left unchanged
- [ ] No temporary or lock file is left behind after success or failure
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- The register design is Aidan's; the file names, lock rule, timeouts and sync timing are Defaults in `DECISIONS.md`. Spike 032 question 8 measures how long a disconnected drive blocks; if its Results are in when you start, check 3 s against them and note any change in Follow-ups (do not change the spec).
- Follow `SettingsStore` for the temporary-file write and for catching only `IOException` and `UnauthorizedAccessException`.
- The store does not decide the folders: the add-in passes `Settings.RegisterFolder` and `%APPDATA%\AwbAddin\register` (task 039).
- Keep everything synchronous apart from the probe; the add-in calls it from a command.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** none here; task 039 checks the register on Aidan's NAS

## Follow-ups

Things noticed but not done.
