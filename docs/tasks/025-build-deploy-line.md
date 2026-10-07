# 025: Build says plainly whether the add-in was deployed

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (Notes for planning: follow-ups to fold in)
- **Status:** todo
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

- [ ] Both `Copy` tasks keep `ContinueOnError="WarnAndContinue"`, so a locked file still does not fail the build
- [ ] After the copies, exactly one high-importance line is printed:
  - success: `Deployed add-in to <folder>. Start Inventor to load this build.`
  - failure of either copy: `NOT deployed: the add-in files in <folder> are locked, probably because Inventor is running. Close Inventor and build again.`
- [ ] The failure line is a `Warning` (so it is counted in the build summary) and the success line a `Message`
- [ ] The result is taken from the copy tasks themselves (for example `$(MSBuildLastTaskResult)` captured into a property after each copy), not from a guess about whether Inventor is running
- [ ] `-p:DeployToInventor=false` still skips the whole target, and non-Windows builds still skip it
- [ ] `README.md` says the build prints one line saying whether it deployed
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes (no Core change)

## Notes for the implementer

- The target is at the end of `src/InventorAddin/InventorAddin.csproj`. Keep its comment block accurate.
- `MSBuildLastTaskResult` is a reserved MSBuild property set after every task. Capture it into your own property straight after each `Copy` with a `PropertyGroup` inside the target, because the next task overwrites it.
- This cannot be run in the cloud: the target is conditioned on Windows and the project needs the Inventor interop. Write "not compiled" for the project file.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** `src/InventorAddin/InventorAddin.csproj`
- **Inventor API members not confirmed:** none (MSBuild only)
- **Manual checklist for Inventor:**
  1. With Inventor closed, `dotnet build InventorAddin.slnx`: the last lines include `Deployed add-in to ...`. Start Inventor; About shows this build's time.
  2. With Inventor open, build again: the build succeeds and prints the `NOT deployed` warning, and does not print `Deployed`.
  3. `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: neither line is printed.

## Follow-ups

Things noticed but not done.
