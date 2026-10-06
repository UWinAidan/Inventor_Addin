# 004: Ribbon layout description in Core

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Target)
- **Status:** done
- **Depends on:** 001
- **Needs:** cloud
- **Parallel-safe with:** 002, 003

## Goal

Which panels and buttons appear on which ribbon is decided in Core and tested, so the add-in's `RibbonSetup` (task 007) only has to draw what Core describes.

## Scope

- `src/InventorAddin.Core/Ribbon/RibbonLayout.cs` (new)
- `src/InventorAddin.Core/Ribbon/RibbonModels.cs` (new: the description types)
- `tests/InventorAddin.Core.Tests/Ribbon/RibbonLayoutTests.cs` (new)

Out of scope:

- Anything under `src/InventorAddin`
- Buttons for commands that do not exist yet. The spec says a button appears only once its command exists. Feature tasks add their own entries later.

## Acceptance criteria

- [x] An enum for the environments: `ZeroDoc`, `Part`, `Assembly`, `Drawing`, each mapping to the Inventor ribbon name of the same spelling
- [x] Description types: a panel has an internal id, a display name and an ordered list of buttons; a button has the command internal name and a size (`Large` or `Small`)
- [x] `RibbonLayout.For(environment, showDeveloperTools)` returns the ordered panels for that environment
- [x] With today's commands the layout is: a "Developer" panel (`id_Panel_WorkflowTools_Dev`) holding `WorkflowTools_ExportModelData` (Part, Assembly, Drawing only) and `WorkflowTools_ExportLibraries` (all four environments), both small
- [x] The Developer panel is absent when `showDeveloperTools` is false
- [x] Panels with no buttons are never returned. `RibbonLayout` exposes whether an environment has any panels, so the add-in can skip creating an empty tab
- [x] Tab id and name are constants in Core: `id_Tab_WorkflowTools`, "Workflow Tools"
- [x] Panel ids for the target layout that will be needed are constants now (`id_Panel_WorkflowTools_CadAutomation` "CAD Automation", `id_Panel_WorkflowTools_DrawingTools` "Drawing Tools"), so later tasks only add buttons. Constants only; no empty panels are returned
- [x] Tests cover every environment with the developer setting on and off, and that no panel is empty
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- File-scoped namespace `InventorAddin.Core.Ribbon`. Records are a good fit for the description types.
- Command internal names are strings here. Keep them as constants in one Core class (for example `CommandNames`) so the add-in commands can use the same constants in task 007.
- The panel order in the spec is CAD Automation, Drawing Tools, Modelling, then Developer last. Keep Developer last.
- The existing behaviour to match is in `src/InventorAddin/UI/RibbonSetup.cs`: read it, do not change it.
- Large buttons without icons: not this task's concern. Size is just data here.

## Verification

- **Ran:** `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 122 passed, 0 failed, 0 skipped (57 of them in `RibbonLayoutTests`).
- **Not compiled (changed under `src/InventorAddin`):** none. Nothing under `src/InventorAddin` was changed.
- **Inventor API members not confirmed:** none. No Inventor API is used.
- **Manual checklist for Inventor:** none, Core only.

Notes for task 007:

- `CommandNames` lives in `RibbonModels.cs` to stay inside this task's file list. The add-in commands should switch to these constants in 007.
- The description records are named `PanelLayout` and `ButtonLayout`, not `RibbonPanel` and `RibbonButton`. `RibbonSetup.cs` has `using Inventor;`, and `Inventor.RibbonPanel` would clash.
- Behaviour change once 007 uses this: with `ShowDeveloperTools` off (the default), no environment has any panels today, so `HasPanels` is false everywhere and no "Workflow Tools" tab is created at all. The current `RibbonSetup` always shows the Developer panel.

## Follow-ups

- Open question 4 in spec 01 (Settings and About on ZeroDoc) decides whether ZeroDoc gets a CAD Automation panel. The layout has no entries for it until that is answered.
- No panel id constant for "Modelling" yet. Its only button, Mass Update, is open question 1 in spec 01.
