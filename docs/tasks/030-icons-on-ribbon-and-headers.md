# 030: Icons on the ribbon buttons and window headers

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (Ribbon icons; Window layout: header)
- **Status:** todo
- **Depends on:** 020 (its Results must be filled in), 021, 024, 025, 026, 028, 029
- **Needs:** cloud, add-in not compiled
- **Parallel-safe with:** none

## Goal

Every ribbon button shows its icon at 16 px and 32 px in Inventor's current theme, and each window's header tile shows the icon of the command that opened it. A failure to load an icon leaves that button text-only and is logged; it never stops the ribbon from being built. Also folds in the follow-up from tasks 005 and 007: one ribbon failing no longer stops the others.

## Before starting

Read the Results of task 020, question 3. Use the conversion method and the members it found to work. If the Results are empty, stop: this task is not runnable yet.

## Scope

- `src/InventorAddin/InventorAddin.csproj` (the PNGs from 024 as embedded resources)
- `src/InventorAddin/UI/Icons/IconLoader.cs` (new): finds and loads an icon by name, theme and size
- `src/InventorAddin/UI/Icons/PictureDispConverter.cs` (new): bitmap to `IPictureDisp`, by 020's method
- `src/InventorAddin/Commands/AddinCommand.cs` (icons on the button definition)
- `src/InventorAddin/UI/RibbonSetup.cs` (passes the icon name and theme; per-ribbon error handling)
- `src/InventorAddin/UI/AboutWindow.xaml`, `SettingsWindow.xaml`, `PartPropertiesWindow.xaml` (header `Icon` binding only) and, if needed, their code-behind or `WindowHost` to supply the icon

Out of scope:

- New icon drawings or changes to them (024).
- Updating icons when the user switches Inventor's theme while it runs: the ribbon is built once at start, like the developer-tools setting. Log the theme used.

## Acceptance criteria

Ribbon

- [ ] The 20 PNGs are embedded resources with predictable names; `IconLoader` finds `{IconName}.{Light|Dark}.{16|32}.png` from the `IconName` on `ButtonLayout` (021) and `UiThemes.ResourceSuffix` (021)
- [ ] `RibbonSetup.Create` reads the theme once (the same `ComSafe` read and Core mapping `WindowHost` uses; share the code rather than repeat it) and logs one `INFO` line naming the theme used for icons
- [ ] Each command's definition gets its 16 px icon as the standard icon and its 32 px icon as the large icon, through the method from 020. When the definition already exists (add-in reloaded), icons are set through the setters 020 confirmed, or skipped with an `INFO` line if 020 found none
- [ ] A command can appear on several ribbons but has one definition: its icon is chosen once, from its `IconName`. If the layout gives one command two different icon names, log a warning and use the first
- [ ] A missing resource, or a failed conversion, logs one `WARN` line naming the icon and leaves the button text-only; the rest of the ribbon is built
- [ ] Buttons keep showing their text next to the icon (`ButtonDisplayEnum.kAlwaysDisplayText`, unchanged)
- [ ] Each environment's ribbon is built inside its own try/catch: a failure logs `ERROR` with the ribbon name and the exception, and the remaining ribbons are still built
- [ ] GDI handles from `GetHbitmap` are released (`DeleteObject`) once the picture owns a copy, or kept alive for the add-in's life if 020 found the picture does not copy; say which

Window headers

- [ ] About, Settings and Part Properties show the 32 px icon of their command (`About`, `Settings`, `PartProperties`) in the header tile, in the theme the window opened in, loaded through `ThemeResources`/`IconLoader` as a WPF `ImageSource`. No colour or size set in the windows
- [ ] A missing header icon leaves the tile empty and logs a `WARN`; the window still opens

General

- [ ] Icon names come only from Core (`IconNames`, `ButtonLayout.IconName`); the add-in does not repeat the strings
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists every add-in file as not compiled

## Notes for the implementer

- Keep `ButtonDefinition` objects referenced as now (`RibbonSetup._commands`); icons do not change that.
- `AddinCommand.Register` currently passes `Type.Missing` for the two icon arguments of `AddButtonDefinition`. Pass the pictures (or `Type.Missing` when none loaded).
- The header icon is the same PNG as the ribbon's 32 px icon. Loading it as a WPF `BitmapImage` from the embedded resource stream is simplest; freeze it.
- Check every member against 020's Results, the Inventor API reference or existing usage. List any you could not confirm.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Light theme: start Inventor, open a part. Part Properties, Settings and About on the CAD Automation panel show their icons at small size, with their text. Turn on developer tools (Settings, restart): Export Model Data and Export Libraries show theirs.
  2. Switch to the dark theme and restart Inventor: the dark-theme icons are used and read clearly.
  3. Right-click the ribbon and customise or enlarge a button if Inventor allows it, or check the 32 px icon in Customize: the large icon shows and is sharp.
  4. Each window's header tile shows its command's icon, in both themes.
  5. The log has one `INFO` line naming the theme used for icons and no icon warnings.
  6. Drawing ribbon and the no-document ribbon still build with their buttons (both themes).
  7. Optional, if simple: rename one embedded PNG in a test build. That button shows text only, a `WARN` names the icon, and every other button has its icon.

## Follow-ups

Things noticed but not done.
