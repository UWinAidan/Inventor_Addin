# 095: Tapped holes show their thread in the model

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Tapped holes")
- **Status:** done
- **Depends on:** 090
- **Needs:** windows build (and its own invisible Inventor for the investigation)
- **Parallel-safe with:** none (changes `HoleFeatureWriter` like 093)

## Goal

Fix the bug Aidan found in the 090 check: tapped holes made by AWB Hole show no thread texture in the model, while Inventor's own tapped holes do. Find out whether the tap drill workaround (`_CreateTapInfo` with `kThreadTapDrillDiameter`, spike 081) causes it, and make AWB tapped holes show their thread while keeping the workbook's tap drill if at all possible.

## Scope

- Investigation with a throwaway console app in the scratchpad, driving its own invisible Inventor (as spike 081 did; see task 093's notes). Never attach to an Inventor Aidan has open.
- `src/InventorAddin/Holes/HoleFeatureWriter.cs`; Core if a rule changes, with tests

## Acceptance criteria

- [x] On a scratch block, make tapped holes four ways and compare them: Inventor's `CreateTapInfo` (minor diameter), `_CreateTapInfo` as the writer calls it now, `_CreateTapInfo` with the thread's major and minor diameter limits filled in (arguments 10 to 15, which the writer passes as missing), and `_CreateTapInfo` with `kThreadMinorDiameter`. For each, record the `HoleTapInfo` values, whether the thread shows (render the view to a bitmap, for example `Camera.SaveAsBitmap` or `View.SaveAsBitmap`, and look at it; say which you used), and the modelled diameter.
- [x] If one way shows the thread and models the tap drill, the writer uses it. If none does, write what was found and the choices in Verification and in spec 09 as an open question (thread texture or our tap drill), keep the tap drill as it is, and stop there.
- [x] Also check the document's thread display setting, if there is one, and Inventor's tapped hole in the same part, so the comparison is fair.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Verification

Filled in by the implementer.

### Findings (spike 095)

A throwaway console app in the scratchpad (`Spike095`) drove its own Inventor 2026 (`Activator.CreateInstance`, never shown) on a 500 x 40 x 30 mm scratch block. Thread display was judged from `Camera.SaveAsBitmap` (500 x 500, oblique view into each hole), which renders correctly from the invisible instance. Display settings were checked first: `Application.DisplayOptions.AreTexturesOn`, `PartDocument.DisplaySettings.AreTexturesOn` and `View.AreTexturesOn` were all true, and the document's `ModelingSettings.TappedHoleDiameter` was `kThreadMinorDiameter` (Inventor's default). All holes were in the same part, M16x2 6H Through All unless stated.

| Way | `_DiameterInModel` | Limits (major, minor, pitch dia.) | `Pitch` | Modelled | Thread shows |
|---|---|---|---|---|---|
| A. `CreateTapInfo` (Inventor's own) | Minor | 16/16.501, 13.835/14.21, 14.701/14.913 | 2 | 13.835 mm (minor) | yes |
| B. `_CreateTapInfo`, tap drill 14, as the writer called it | TapDrill | empty | cannot be read | 14 mm | **no** |
| C. as B with arguments 10 to 15 filled from A | TapDrill | as A | cannot be read | 14 mm | **no** |
| D. `_CreateTapInfo` with `kThreadMinorDiameter` | | | | refused (E_INVALIDARG, with or without a tap drill) | |

- The cause is the missing pitch: a `_CreateTapInfo` hole has no pitch on its tap info or on its face's `ThreadInfos`, and Inventor draws no thread without one. Filling the limits (C) does not give it a pitch; neither do other nominal/pitch designation strings, re-setting `ThreadType` or `Class` and reassigning, or giving a `CreateTapInfo` hole a `_CreateTapInfo` tap info afterwards (that models the tap drill but loses the pitch and the thread).
- `ModelingSettings.TappedHoleDiameter = kThreadTapDrillDiameter` makes `CreateTapInfo` holes model their `TapDrillDiameter` with the thread showing, but it is document-wide and live: every standard tapped hole in the part follows it, Aidan's own included, and they go back when it is reset. Not used.
- **The fix used:** make the hole with `CreateTapInfo`, then on `hole.TapInfo` set `MinorDiameterMin` and `TapDrillDiameter` to the workbook's tap drill (table units) and assign it back. With the setting at Minor the hole is modelled at `MinorDiameterMin`; at Tap Drill, at `TapDrillDiameter`; so both are set. The pitch stays and the thread shows. Measured: M16x2 at 14.4 mm, M6x1 blind (thread 9, drill 12) at 5.1 mm, M6x1 at 4.8 mm (below the table's minor), 1/4-20 UNC at 0.2 in (5.08 mm), 10-32 UNF at 0.152 in (3.861 mm), all with the thread showing (these spike values were chosen to differ from both Inventor's table values and the workbook's 5, 14, 0.159 and 0.201, so a change is visible; the workbook's own values were not tried). They keep it after save, reopen and `Rebuild`, and after a thread depth edit. Neither value can be set on a tap info before the hole is made (E_FAIL). Abort and Undo revert everything.
- With the setting at Major or Pitch, Inventor models that diameter whatever the add-in sets (that is the user's choice in Document Settings).

### Changes

- `HoleFeatureWriter`: tapped holes are made with `CreateTapInfo` and then given the workbook's tap drill (`SetTapDrill`). The hidden `_CreateTapInfo` is no longer called. If Inventor refuses the tap drill, the hole stays at the minor diameter, with the thread showing, and the existing `TapDrillFallback` note says so. `NeedsRecreate` no longer requires `_DiameterInModel == kThreadTapDrillDiameter` (the add-in's holes now read Minor); it remakes a hole whose pitch cannot be read (made before this task, so it shows no thread) and one whose `TapDrillDiameter` or `MinorDiameterMin` is not the workbook's tap drill.
- Core: doc comments only (`HoleFeatureMessages.TapDrillFallback`, `HoleThread.TapDrillInTableUnits`); the message text is unchanged. Spec 09's tap drill line notes that the thread shows.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off` on Windows: 0 errors, 4 warnings, all CS8600 in files this task did not touch. `dotnet test tests/InventorAddin.Core.Tests`: 3235 passed, 0 failed. The spike exercised the same calls as the writer (`CreateTapInfo`, add, `Update`, set `MinorDiameterMin` and `TapDrillDiameter`, reassign `TapInfo`) in Inventor 2026; the writer itself has not been run in Inventor.
- **Not compiled (changed under `src/InventorAddin`):** none; `HoleFeatureWriter.cs` compiled on Windows.
- **Inventor API members not confirmed:** none. `HoleTapInfo.MinorDiameterMin` (set), `HoleTapInfo.TapDrillDiameter` (set), `HoleTapInfo.Pitch` (throws on a `_CreateTapInfo` hole) and `HoleFeature.TapInfo` (set) were all exercised in the spike.
- **Manual checklist for Inventor:**
  1. In a new part with a block, run AWB Hole and make a tapped M6, Through All, and another blind. The thread texture shows inside both, like an Inventor tapped M6 placed beside them with Inventor's Hole command. Measure each bore: it is the workbook's tap drill (the Inventor one is the minor diameter, 4.917 mm).
  2. Do the same for an imperial row (1/4-20 UNC, #10-32 UNF): thread shows, bore is the workbook's tap drill.
  3. Edit an AWB tapped hole with AWB Hole and change only its depth: it keeps the thread texture and the tap drill. Change its size to another thread: the new thread shows and its bore is the new tap drill.
  4. Open a part with a tapped hole made by AWB Hole before this change (no thread showing), edit it with AWB Hole and press OK: it is made again and now shows the thread.
  5. Edit an AWB tapped hole with Inventor's own Hole command (double-click it), change nothing and OK: check whether the bore stays at the tap drill or goes back to the minor diameter (not tried in the spike).
  6. Ctrl+Z after creating a tapped hole removes it in one step.

## Follow-ups

- `HoleThreadNames.NominalSize` (Core, with tests) is now used only by the removed `_CreateTapInfo` call, so nothing in the add-in calls it. Remove it, or keep it for the hole table, as the parent prefers.
- A tap drill change now could be made in place (set `MinorDiameterMin` and `TapDrillDiameter` and reassign) instead of remaking the hole; the writer still remakes it, as before.
- When a part's Document Settings Tapped Hole Diameter is Major or Pitch, AWB tapped holes are modelled at that diameter, not the tap drill, with no message. A note could say so.
- Whether Inventor's own Hole dialog keeps the tap drill when it edits an AWB tapped hole is untested (checklist item 5).
- From the review: `MinorDiameterMax` is left as Inventor's table has it, so a tap drill above the class's minor maximum gives Min > Max (Inventor accepted it for M16 at 14.4). Today's workbook rows all fall inside the band. Inventor's own thread notes and Hole dialog will show the tap drill as the minor minimum; not checked on a drawing.
- A hole left on the fallback (tap drill refused) never matches the workbook's `MinorDiameterMin`, so every later AWB Hole edit makes it again and repeats the note. Acceptable for a rare fallback.
- The `doc.Update()` between adding a tapped hole and setting its tap drill is wrapped in `ComSafe.Run` (review), like the one in `Check`.
