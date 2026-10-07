# 021: Theme choice and ribbon icon names (Core)

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (Following Inventor's theme; Ribbon icons; Core logic)
- **Status:** done
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 022, 023, 024, 025

## Goal

Core decides which colour set a window uses from the theme name Inventor reports, and the ribbon layout names the icon of every button. The add-in only reads the theme name (026) and loads the named icons (030).

## Scope

- `src/InventorAddin.Core/Theming/UiTheme.cs` (new)
- `src/InventorAddin.Core/Ribbon/RibbonModels.cs` (`ButtonLayout` gains `IconName`; `IconNames` constants)
- `src/InventorAddin.Core/Ribbon/RibbonLayout.cs` (an icon name per button)
- `tests/InventorAddin.Core.Tests/Theming/UiThemeTests.cs` (new)
- `tests/InventorAddin.Core.Tests/Ribbon/RibbonLayoutTests.cs` (update)

Out of scope:

- Reading the theme from Inventor (026), the icon images (024) and loading them (030).

## Acceptance criteria

Theme

- [ ] `public enum UiTheme { Light, Dark }` in namespace `InventorAddin.Core.Theming`
- [ ] `UiThemes.FromInventorThemeName(string? name)` returns `Dark` when the trimmed name contains `dark` in any case, and `Light` for anything else, including null, blank and unknown names (the spec's fallback)
- [ ] `UiThemes.ResourceSuffix(UiTheme theme)` returns `"Light"` or `"Dark"`, for the add-in to build the dictionary and icon file names from; an undefined value throws `ArgumentOutOfRangeException`
- [ ] Tests cover `"LightTheme"`, `"DarkTheme"`, `"dark"`, `"  Dark Theme  "`, `"Light Gray"`, `null`, `""`, `"   "` and an unknown name

Icon names

- [ ] `ButtonLayout` becomes `ButtonLayout(string CommandInternalName, ButtonSize Size, string IconName)`
- [ ] `IconNames` (in `RibbonModels.cs`) holds one constant per command: `PartProperties`, `Settings`, `About`, `ExportModelData`, `ExportLibraries`. Each value is the command name without the `Awb_` prefix (for example `"PartProperties"`), so it is a plain file-name stem
- [ ] Every button in `RibbonLayout.Definitions` carries its icon name, and `RibbonLayout.For` passes it through
- [ ] Tests: every button in every environment, with developer tools on and off, has a non-blank icon name; the icon name of each command matches its `IconNames` constant; icon names are distinct
- [ ] Existing ribbon layout tests still pass, updated only where they construct or compare `ButtonLayout`
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- The theme names Inventor reports are not confirmed yet (spike 020). "Contains `dark`" is the default rule, logged in `DECISIONS.md`; 026 replaces it only if the spike shows a name it gets wrong. Keep the rule in one method so that change is one line.
- `RibbonSetup` in the add-in reads `ButtonLayout.Size` only and never constructs a `ButtonLayout`, so adding the parameter needs no add-in change. Confirm with a search; if any add-in file does construct one, stop and record it.
- Core uses file-scoped namespaces and implicit usings.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 586 passed, 0 failed, 0 skipped (cloud session).
- **Not compiled (changed under `src/InventorAddin`):** none. Searched the add-in for `ButtonLayout`: `UI/RibbonSetup.cs` only iterates `panelLayout.Buttons` and reads `Size`; nothing outside Core constructs a `ButtonLayout`, so the new parameter needs no add-in change.
- **Inventor API members not confirmed:** none (no Inventor API used)
- **Manual checklist for Inventor:** none (covered by 026 and 030). The ribbon is unchanged in Inventor by this task.

## Follow-ups

Things noticed but not done.
