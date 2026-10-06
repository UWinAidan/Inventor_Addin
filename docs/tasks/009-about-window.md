# 009: About window

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Shared plumbing: About / Version)
- **Status:** todo
- **Depends on:** 008
- **Needs:** cloud (writes add-in code that is not compiled here; Aidan builds it)
- **Parallel-safe with:** none

## Goal

An About button shows the add-in version and build date, so Aidan can tell which build Inventor loaded.

## Scope

- `src/InventorAddin.Core/ViewModels/AboutViewModel.cs` (new)
- `src/InventorAddin.Core/Ribbon/RibbonLayout.cs` and the command name constants (add the About button)
- `tests/InventorAddin.Core.Tests/ViewModels/AboutViewModelTests.cs` (new)
- `tests/InventorAddin.Core.Tests/Ribbon/RibbonLayoutTests.cs` (update)
- `src/InventorAddin/InventorAddin.csproj` (version and build date attribute)
- `src/InventorAddin/UI/AboutWindow.xaml` and `.xaml.cs` (new)
- `src/InventorAddin/Commands/ShellCommands.cs` (add `AboutCommand`)
- `src/InventorAddin/UI/RibbonSetup.cs` (register the command)

Out of scope:

- Checking for a newer version (spec 01 open question 2)
- Icons

## Acceptance criteria

- [ ] The add-in csproj sets `<Version>0.1.0</Version>` and embeds the UTC build time as `AssemblyMetadata("BuildDate", …)` in ISO 8601 through an `AssemblyAttribute` item
- [ ] `AboutViewModel` takes a version string, a build date string (may be missing or unparseable), the Inventor major version and the log folder path. It exposes the title "Inventor Workflow Tools", a version line, a build date line (`yyyy-MM-dd HH:mm` local, or "unknown" when missing or unparseable) and the log folder line
- [ ] Tests cover a normal date, a missing date and a garbage date
- [ ] The layout adds a small `WorkflowTools_About` button after Settings in the CAD Automation panel on Part, Assembly and Drawing. Layout tests updated
- [ ] `AboutCommand` is query-only, reads the attributes from the add-in assembly, and shows the window through `WindowHost`
- [ ] The window binds to the view-model and has a Close button
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists every add-in file as not compiled

## Notes for the implementer

- Read the attribute with `Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()` and pick the `BuildDate` key. The version comes from `AssemblyInformationalVersionAttribute` (the SDK may append a `+commit` suffix; show it as is).
- Follow the pattern task 008 set for windows, `WindowHost` and the shell commands.

## Verification

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Build, start Inventor, open a drawing.
  2. The CAD Automation panel shows Settings then About.
  3. Click About. It shows version 0.1.0, today's build date and time, Inventor 30, and the log folder path. Close works.
  4. Rebuild and restart. The build time has changed.

## Follow-ups
