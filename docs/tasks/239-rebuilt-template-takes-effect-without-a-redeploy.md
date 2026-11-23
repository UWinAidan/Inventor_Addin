# 239: A rebuilt template takes effect without a redeploy

- **Milestone:** M5 (from Aidan's check of 222, 226 and 237, 2026-11-23)
- **Feature spec:** `docs/features/05-drawing-tools.md` (tasks 186, 187)
- **Status:** todo
- **Depends on:** 187
- **Needs:** windows build
- **Parallel-safe with:** none

## Goal

Build Print Template saves into the folder Aidan picks (the repo's `libraries\Templates`), but Create Print reads the starter copy beside the deployed add-in (`%APPDATA%\Autodesk\Inventor 2026\Addins\InventorAddin\libraries\Templates`), which only changes on a build. After a rebuild Create Print kept using the old template until the add-in was rebuilt and redeployed. A rebuilt template must take effect at once.

## Scope

Default (parent, 2026-11-23; Aidan can overturn): Build Print Template also writes the deployed copy.

- After both templates are saved to the chosen folder, the command copies them into the starter templates folder beside the running add-in (`LibraryPaths` gives the folder; the same folder Create Print falls back to when the Settings template path is blank). Skip the copy when the chosen folder already is that folder.
- The done message and the log name both places. When a Settings template path is set (not blank), the message also says Create Print uses that path, not the starter copy.
- A copy that fails (file locked because a drawing made from it is open counts only if Windows refuses) is a warning in the message, not a failure of the build.
- Core: the decision of where to copy and what the message says (chosen folder, starter folder, Settings paths), with tests.

The other option, Create Print reading one agreed location (for example the repo folder recorded in Settings), was not chosen because it makes every install depend on a path that only exists on Aidan's PC.

## Acceptance criteria

- [ ] After Build Print Template, Create Print uses the new template with no build or redeploy.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors; every Inventor API member is confirmed in a spike or existing code, or listed under Verification.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**

## Follow-ups

Things noticed but not done.
