# 066: The AWB Addin tab is active when Inventor starts with no document

- **Milestone:** M2 (fix run 2)
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` ("Target")
- **Status:** todo
- **Depends on:** 062
- **Needs:** windows build (local session); Core tested
- **Parallel-safe with:** none

## Goal

Aidan, 2026-10-12: when Inventor starts with no document open, the AWB Addin tab is the active tab. Only at startup, not when opening files.

Spike 062 (section 2) found: `ApplicationEvents.OnReady` fires about a second after the add-in is activated; setting `RibbonTab.Active = true` on the ZeroDoc tab then works, but between 0.5 s and 1.5 s later Inventor switches back to its own tab by itself. Setting it again at 2 s stuck, and Aidan saw Inventor end on the AWB Addin tab.

## Scope

- `src/InventorAddin.Core/Ribbon/StartupTabPlan.cs` (new): the decisions, as a small state machine
- `src/InventorAddin/UI/StartupTab.cs` (new): the Inventor side
- `src/InventorAddin/StandardAddInServer.cs`: create it after the ribbon, dispose it in `Deactivate`
- The matching tests under `tests/InventorAddin.Core.Tests/`

## Acceptance criteria

- [ ] `StartupTabPlan` (Core) decides, from what the add-in sees, whether to make the tab active and whether to stop. Inputs: activated with Inventor ready or not; `OnReady`; a periodic check with the elapsed time, whether a document is visible, and whether the AWB tab is the active ZeroDoc tab. Rules (Default, from spike 062):
  - Activated when Inventor is already ready (loaded from the Add-in Manager): do nothing, done.
  - `OnReady` with a document visible: do nothing, done. With none: make the tab active and start watching.
  - While watching: a document visible: done. The AWB tab no longer active, the first time: make it active again, done. The watch window (5 s, a constant) passes: done.
  - It never makes the tab active more than twice, and never after it is done.
  Tested for each rule
- [ ] `StartupTab` (add-in) follows the plan: reads `Application.Ready` at `Activate`; subscribes `ApplicationEvents.OnReady` (kept referenced) and acts on `kAfter` only; watches with a `System.Windows.Forms.Timer` every 250 ms; sets `RibbonTab.Active = true` on the add-in's tab on the ZeroDoc ribbon (found by `RibbonIds.TabId`). Unsubscribes and stops the timer when done and in `Dispose`. Every failure is logged and never reaches Inventor
- [ ] One log line when the tab is made active (and when made active again), and one saying why it stopped
- [ ] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spike 062's Results, section 2, first; every Inventor member used here is confirmed there. The throwaway test build's code is in the parent's scratchpad, `StartupTab.test.cs`; it may be used as a reference but its 10 s probe log and fixed 2 s and 6 s retries go.
- The ZeroDoc ribbon's name is `RibbonEnvironment.ZeroDoc.RibbonName()`.
- A tab Aidan clicks after the watch is done must stay; the watch is short and stops after the one retry for that reason.

## Verification

Filled in by the implementer.

- **Ran:**
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups

Things noticed but not done.
