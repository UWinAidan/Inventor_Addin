# 009: About window

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Shared plumbing: About / Version)
- **Status:** done
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

- [x] The add-in csproj sets `<Version>0.1.0</Version>` and embeds the UTC build time as `AssemblyMetadata("BuildDate", …)` in ISO 8601 through an `AssemblyAttribute` item
- [x] `AboutViewModel` takes a version string, a build date string (may be missing or unparseable), the Inventor major version and the log folder path. It exposes the title "Inventor Workflow Tools", a version line, a build date line (`yyyy-MM-dd HH:mm` local, or "unknown" when missing or unparseable) and the log folder line
- [x] Tests cover a normal date, a missing date and a garbage date
- [x] The layout adds a small `WorkflowTools_About` button after Settings in the CAD Automation panel on Part, Assembly and Drawing. Layout tests updated
- [x] `AboutCommand` is query-only, reads the attributes from the add-in assembly, and shows the window through `WindowHost`
- [x] The window binds to the view-model and has a Close button
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists every add-in file as not compiled

## Notes for the implementer

- Read the attribute with `Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()` and pick the `BuildDate` key. The version comes from `AssemblyInformationalVersionAttribute` (the SDK may append a `+commit` suffix; show it as is).
- Follow the pattern task 008 set for windows, `WindowHost` and the shell commands.

## Verification

- **Ran:** `dotnet build src/InventorAddin.Core`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 211 passed, 0 failed, 0 skipped (25 more than after task 008: 24 in `AboutViewModelTests`, 1 net from the updated `RibbonLayoutTests`). Cloud session on Linux; `src/InventorAddin` was not built (`CLAUDE_CODE_REMOTE=true`, no interop DLL). The csproj pattern (`<Version>` plus an `AssemblyAttribute` item for `AssemblyMetadataAttribute("BuildDate", ...)` using `$([System.DateTime]::UtcNow.ToString("yyyy-MM-ddTHH\:mm\:ssZ"))`) was checked in a throwaway console project in the scratchpad: reflection read back `BuildDate=2026-10-06T15:53:20Z` and informational version `0.1.0`. After review: the colons are escaped because an unescaped `:` in a .NET date format is the current culture's time separator. With `dotnet msbuild` under `LANG=fi_FI.UTF-8` and `da_DK.UTF-8`, the unescaped format produced `2026-10-06T15.53.02Z`, which `AboutViewModel` would show as "unknown". The escaped format produced `15:53:02` under fi_FI, da_DK and en_US, and the console project read back a colon-separated `BuildDate` under all three. Core tests rerun after the fix: 211 passed, 0 failed.
- **Not compiled (changed under `src/InventorAddin`):**
  - `src/InventorAddin/InventorAddin.csproj` (`<Version>0.1.0</Version>`, `BuildDate` assembly attribute)
  - `src/InventorAddin/UI/AboutWindow.xaml` (new)
  - `src/InventorAddin/UI/AboutWindow.xaml.cs` (new)
  - `src/InventorAddin/Commands/ShellCommands.cs` (adds `AboutCommand`)
  - `src/InventorAddin/UI/RibbonSetup.cs` (registers `AboutCommand`)
- **Inventor API members not confirmed:** none new. `AboutCommand` reads the Inventor version through the existing `InventorHost.MajorVersion` (`Application.SoftwareVersion.Major`, already used at startup), wrapped in `ComSafe.Get`, and shows the window through task 008's `WindowHost` (its unconfirmed `Application.MainFrameHWND` still applies). It keeps the default `kQueryOnlyCmdType`.
- **Manual checklist for Inventor:**
  1. Build, start Inventor, open a drawing.
  2. The CAD Automation panel shows Settings then About (both small). Open a part and an assembly: same. With no document open there is no About button.
  3. Click About. "About Workflow Tools" opens centred over Inventor and blocks it until closed. It shows "Inventor Workflow Tools", "Version: 0.1.0" (possibly followed by `+<commit hash>`), "Built: " with today's date and the build time in local time (`yyyy-MM-dd HH:mm`), "Inventor major version: 30", and "Log folder: %APPDATA%\InventorWorkflowTools" expanded to the real path. The log folder text can be selected and copied. Close, Enter and Esc each close it.
  4. Rebuild and restart. The build time has changed.
  5. The log has `INFO Command WorkflowTools_About (About) started.` for each click.

Notes on choices the brief left open:

- `AboutViewModel(string? version, string? buildDate, int? inventorMajorVersion, string? logFolder, TimeZoneInfo? timeZone = null)`. The optional zone defaults to `TimeZoneInfo.Local`; tests pass fixed zones so they do not depend on the machine.
- Lines are labelled: "Version: ...", "Built: ...", "Inventor major version: ...", "Log folder: ...". Any missing value (including an Inventor version that could not be read and no log file) shows "unknown", matching the build date rule.
- The build date is parsed with `DateTimeOffset.TryParse` (invariant culture, a value without an offset is taken as UTC), so any ISO 8601 form is accepted (as are other invariant-culture date forms, which the build never writes); anything that fails to parse shows "unknown". Output is formatted with the invariant culture.
- The log folder is the directory of `AddinServices.LogFilePath`, so it is "unknown" when no log file is being written.
- Because the build date changes every build, the add-in assembly recompiles on every build, even when no source changed.

## Follow-ups

None.
