# 025: Build says plainly whether the add-in was deployed

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (Notes for planning: follow-ups to fold in)
- **Status:** done
- **Depends on:** none
- **Needs:** cloud (MSBuild change that only runs on Windows; Aidan checks it)
- **Parallel-safe with:** 021, 022, 023, 024, 026 to 029 (not 030, which also edits the project file)

## Goal

When Inventor is open during a build, the add-in DLL is locked and the copy into Inventor's add-ins folder fails. Today that shows only as a warning that is easy to miss, followed by a "Deployed" message that is not true (Aidan hit this on 2026-10-06). After this task the build ends with one plain line: deployed, or not deployed because the files are locked.

## Scope

- `src/InventorAddin/InventorAddin.csproj` (the `DeployToInventor` target only)
- `README.md` (one sentence under "Build & run")

Out of scope:

- The proposed PowerShell build script in spec 08 ("Also in this milestone"). Aidan has not agreed to it.
- Stopping or detecting Inventor's process.

## Acceptance criteria

- [x] Both `Copy` tasks keep `ContinueOnError="WarnAndContinue"`, so a locked file still does not fail the build
- [x] After the copies, exactly one high-importance line is printed:
  - success: `Deployed add-in to <folder>. Start Inventor to load this build.`
  - failure of either copy: `NOT deployed: the add-in files in <folder> are locked, probably because Inventor is running. Close Inventor and build again.`
- [x] The failure line is a `Warning` (so it is counted in the build summary) and the success line a `Message`
- [x] The result is taken from the copy tasks themselves (for example `$(MSBuildLastTaskResult)` captured into a property after each copy), not from a guess about whether Inventor is running
- [x] `-p:DeployToInventor=false` still skips the whole target, and non-Windows builds still skip it
- [x] `README.md` says the build prints one line saying whether it deployed
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes (no Core change)

## Notes for the implementer

- The target is at the end of `src/InventorAddin/InventorAddin.csproj`. Keep its comment block accurate.
- `MSBuildLastTaskResult` is a reserved MSBuild property set after every task. Capture it into your own property straight after each `Copy` with a `PropertyGroup` inside the target, because the next task overwrites it.
- This cannot be run in the cloud: the target is conditioned on Windows and the project needs the Inventor interop. Write "not compiled" for the project file.

## Verification

Filled in by the implementer.

- **Ran:**
  - `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors.
  - `dotnet test tests/InventorAddin.Core.Tests`: 586 passed, 0 failed, 0 skipped.
  - The new `DeployToInventor` target logic, copied into a scratch project on Linux (Windows condition removed, `\` paths swapped for `/`), run with `dotnet msbuild` (SDK 10.0.112): normal copy prints only `Deployed add-in to ... Start Inventor to load this build.`; a blocked file in a subfolder (first Copy) and a blocked `.addin` (second Copy) each give the MSB3024 warning, then only the `NOT deployed` warning, and exit code 0; `-p:DeployToInventor=false` prints neither line. A real lock by a running Inventor was not tested.
  - The first Copy now uses `DestinationFiles` with an item transform instead of `DestinationFolder` with `%(RecursiveDir)`. The old form ran Copy once per subfolder, so `MSBuildLastTaskResult` could reflect only the last subfolder; the new form runs once and its result covers every file. The deployed layout is unchanged (checked in the scratch run).
- **Not compiled (changed under `src/InventorAddin`):** `src/InventorAddin/InventorAddin.csproj`
- **Inventor API members not confirmed:** none (MSBuild only)
- **Manual checklist for Inventor:**
  1. With Inventor closed, `dotnet build InventorAddin.slnx`: the last lines include `Deployed add-in to ...`. Start Inventor; About shows this build's time.
  2. With Inventor open, build again: the build succeeds and prints the `NOT deployed` warning, and does not print `Deployed`.
  3. `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: neither line is printed.

## Follow-ups

Things noticed but not done.

- Copy keeps its default retries (I believe 10 tries, 1 s apart, in current MSBuild; not checked on Windows), so with Inventor open the build may wait about 10 s before the `NOT deployed` line. If that is annoying, pass `Retries="0"` or a small number on both Copy tasks.
