# 066: The AWB Addin tab is active when Inventor starts with no document

- **Milestone:** M2 (fix run 2)
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` ("Target")
- **Status:** done
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

- [x] `StartupTabPlan` (Core) decides, from what the add-in sees, whether to make the tab active and whether to stop. Inputs: activated with Inventor ready or not; `OnReady`; a periodic check with the elapsed time, whether a document is visible, and whether the AWB tab is the active ZeroDoc tab. Rules (Default, from spike 062):
  - Activated when Inventor is already ready (loaded from the Add-in Manager): do nothing, done.
  - `OnReady` with a document visible: do nothing, done. With none: make the tab active and start watching.
  - While watching: a document visible: done. The AWB tab no longer active, the first time: make it active again, done. The watch window (5 s, a constant) passes: done.
  - It never makes the tab active more than twice, and never after it is done.
  Tested for each rule
- [x] `StartupTab` (add-in) follows the plan: reads `Application.Ready` at `Activate`; subscribes `ApplicationEvents.OnReady` (kept referenced) and acts on `kAfter` only; watches with a `System.Windows.Forms.Timer` every 250 ms; sets `RibbonTab.Active = true` on the add-in's tab on the ZeroDoc ribbon (found by `RibbonIds.TabId`). Unsubscribes and stops the timer when done and in `Dispose`. Every failure is logged and never reaches Inventor
- [x] One log line when the tab is made active (and when made active again), and one saying why it stopped
- [x] `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` succeeds and `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- Read spike 062's Results, section 2, first; every Inventor member used here is confirmed there. The throwaway test build's code is in the parent's scratchpad, `StartupTab.test.cs`; it may be used as a reference but its 10 s probe log and fixed 2 s and 6 s retries go.
- The ZeroDoc ribbon's name is `RibbonEnvironment.ZeroDoc.RibbonName()`.
- A tab Aidan clicks after the watch is done must stay; the watch is short and stops after the one retry for that reason.

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (local session), `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false`: succeeded, 0 errors, 8 warnings (all CS8600 in existing command files, none in this task's files). `dotnet test tests/InventorAddin.Core.Tests`: 2187 passed, 0 failed, 0 skipped (22 of them new, in `Ribbon/StartupTabPlanTests.cs`).
- **Changed under `src/InventorAddin` (compiled locally, not tested in Inventor):** `src/InventorAddin/UI/StartupTab.cs` (new); `src/InventorAddin/StandardAddInServer.cs` (creates `StartupTab` after the ribbon and calls `Start`; disposes it first in `Deactivate`).
- **Inventor API members not confirmed:** none. Every member used (`Application.Ready`, `Application.ApplicationEvents`, `ApplicationEvents.OnReady` and its delegate, `EventTimingEnum.kAfter`, `HandlingCodeEnum.kEventNotHandled`, `Documents.VisibleDocuments.Count`, `UserInterfaceManager.Ribbons[name]`, `Ribbon.RibbonTabs`, `RibbonTab.InternalName`, `RibbonTab.Active` get and set) is in spike 062's confirmed list and its test build, which ran in Inventor 2026.
- **Behaviour notes:** an unreadable `Application.Ready` counts as ready, an unreadable document count as a document open, and an unreadable tab state as the tab still active, so when in doubt the add-in leaves the tab alone. If the tab is not found on the ZeroDoc ribbon, a warning is logged and the watch stops.
- **Manual checklist for Inventor:** build and deploy (`dotnet build InventorAddin.slnx` with Inventor closed). After each step, read the lines starting `Startup tab:` in `%APPDATA%\AwbAddin\awbaddin.log`.
  1. Start Inventor from the Start menu with no file. Inventor should end on the AWB Addin tab and stay there. The log shows "made AWB Addin the active tab", usually "Inventor switched tabs; made AWB Addin the active tab again after N s", then one "stopped; ..." line.
  2. Close Inventor. Double-click a part file (`.ipt`) in Explorer so Inventor starts with it open. The Part ribbon shows the tab Inventor normally picks; the add-in does not change it. The log shows "stopped; a document is open, so the tab is left alone." and no "made ... active" line. Close the part: the ZeroDoc ribbon is not switched to AWB Addin.
  3. With Inventor running (no file), unload the add-in and load it again from Tools > Add-ins (Add-in Manager). The tab is not changed; the log shows "stopped; Inventor was already running when the add-in loaded, so the tab is left alone."
  4. Start Inventor with no file and, in the first few seconds after the window appears, click another tab (for example Get Started). Expected: at most one switch back to AWB Addin, only within the first 5 s; a tab clicked after that stays. If a switch back overrides your click, note how many seconds after start it happened (the log has the time).
  5. Start Inventor with no file and open a part within the first 5 s: the log shows "stopped; a document is open" and the tab is not changed again.

## Follow-ups

Things noticed but not done.

- The plan cannot tell a tab Aidan clicks from Inventor's own switch-back, so a click inside the 5 s window, before Inventor's switch, can be overridden by the one retry (checklist item 4). Spike 062 measured Inventor's switch at 0.5 to 1.5 s after `OnReady`. If this is a nuisance, the watch window could be shortened to 2 or 3 s (one constant, `StartupTabPlan.WatchWindow`); Aidan's call.
