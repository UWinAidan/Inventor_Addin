# 078: Settings and About on every ribbon, including no document open

- **Milestone:** M3 (fix from Aidan's M3 checks, 2026-10-17)
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` ("Panels today", question 4)
- **Status:** done
- **Depends on:** none
- **Parallel-safe with:** 077

## Goal

Aidan answered spec 01 question 4: Settings and About are on the AWB Addin tab in every environment: no document open, part, assembly and drawing.

## Scope

- `src/InventorAddin.Core/Ribbon/RibbonLayout.cs` and `RibbonLayoutTests`
- Spec 01

## Acceptance criteria

- [x] The AWB panel (Settings, About) is on the ZeroDoc, Part, Assembly and Drawing ribbons, far right
- [x] `RibbonLayoutTests` pin the ZeroDoc layout with the AWB panel, and a test checks every environment has Settings and About in the last panel
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Verification

- **Changed:** the AWB panel's buttons use every environment. Neither command needs a document: Settings reads only the add-in's settings, About only the build and Inventor version. `RibbonSetup` already builds the ZeroDoc ribbon from `RibbonLayout`, so the add-in code is unchanged.
- **Ran (local, Windows, Aidan's PC):** `dotnet test tests/InventorAddin.Core.Tests`: 2616 passed. `dotnet build InventorAddin.slnx -tl:off`: build succeeded, not deployed (Inventor running).
- **Inventor API members not confirmed:** none.
- **Manual checklist for Inventor:**
  1. Start Inventor with no document open: the AWB Addin tab shows New Part, Mass Update, then the AWB panel with Settings and About, far right.
  2. Settings and About open and work with no document open (Settings saves; About shows the version and log folder).
  3. Open a part, an assembly and a drawing: Settings and About are still on the tab, far right.
  4. Close all documents: the ZeroDoc tab still has them. No new warnings in `awbaddin.log`.

## Follow-ups

None.
