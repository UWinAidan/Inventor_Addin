# 092: Edit Feature and double-click open AWB Hole; re-apply offer

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command"); spike results in `docs/tasks/081-spike-awb-hole.md`
- **Status:** done
- **Depends on:** 090
- **Needs:** windows build
- **Parallel-safe with:** 091

## Goal

Every way of editing a hole in Inventor opens the AWB Hole window on it: Edit Feature from the browser menu and from the viewport's mini toolbar, and double-click. A hole changed outside AWB Hole is offered a re-apply of its fit, tolerance and colour.

## Scope

- `src/InventorAddin/Holes/` (an edit-routing class hooked at startup), the "Edit Feature" button definition with Inventor's icon
- Core: the re-apply decision (from 084's comparison) and its message, with tests

## Acceptance criteria

- [x] Browser right-click (`OnLinearMarkingMenu`): when the selection is a hole or a face made by one (`Face.CreatedByFeature`), Inventor's `PartNGxHoleEditCtxCmd` is replaced by our "Edit Feature" in the same place.
- [x] Viewport mini toolbar (`OnContextualMiniToolbar`): `PartNGxHoleEditCtxCmd` swapped for ours in the `NameValueMap`; our definition carries Inventor's Edit Feature icon (copied from `PartNGxHoleEditCtxCmd.StandardIcon`/`LargeIcon`), since the toolbar shows icons only.
- [x] Double-click (`OnDoubleClick`, `kEventHandled`) on a hole or its face opens our window.
- [x] Backstop: if `PartNGxHoleEditCtxCmd` starts anyway (`OnActivateCommand`), `StopActiveCommand()` and open our window.
- [x] The window always opens after Inventor's event has returned; while it is open, a second request brings it to the front.
- [x] Only while AWB Hole has taken over (switch off): with "Use Inventor's Hole command" on, Edit Feature and double-click stay Inventor's.
- [x] Re-apply: when an AWB hole is opened and its stored data no longer matches the hole (diameter, tolerance, colour), the window says so and offers to re-apply; Inventor's own edit keeps the colour (spike 081).
- [x] An Inventor hole opens with "OK makes this an AWB hole" (spec 09 question 5).
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx` builds

## Notes for the implementer

- Since this brief was written: the window was redesigned (104), depth boxes take expressions (100), and dowel seats are sized in the window (106). The re-apply comparison could also cover a dowel's seat sizes and the stored depth expressions (follow-ups of 100 and 106); include them if they fit the comparison cleanly, else leave them as follow-ups.
- Today's edit path (`HoleReadBack`, `HoleSession` opening an existing hole) is how the window opens on a hole; route to it rather than building a second one.

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (Windows, Inventor 2026 installed), `dotnet build src/InventorAddin.Core`: 0 warnings, 0 errors. `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: build succeeded, 0 errors; the only warnings are the existing CS8600 ones in `CreatePrintCommand`, `NewPartCommand`, `SaveAsCopyCommand` and `SaveAsReplaceCommand`. `dotnet test tests/InventorAddin.Core.Tests`: 3592 passed, 0 failed (59 new). Nothing was run inside Inventor.
- **What was built:**
  - Core: `Holes/HoleEditRules.cs` (the routing decisions: redirect only while AWB Hole has the Hole command and a part is open in the window; the one hole a selection is about; the menu swap and the mini toolbar's insert index; the backstop; log lines), `Holes/HoleReapply.cs` (`HoleReapplyOffer`, `HoleReapplyRules.Check` and the window's texts), `CommandNames.AwbHoleEdit`, `HolePickingWatch` taking more own commands (our Edit Feature pressed while the window is open brings it to the front instead of cancelling the session), `HoleStateComparison.Compare(stored, current, units)` and `LengthText` (an imperial hole is described in inches), and `AwbHoleViewModel` `HasReapplyOffer`, `ReapplyHeading`, `ReapplyDifferences`, `ReapplyNote` (a new `reapplyOffer` argument on `ForEdit` and `ForReadBack`, ignored for an Inventor hole).
  - Add-in: `Holes/HoleEditRedirect.cs` (started by `RibbonSetup` after the takeover, disposed before it), `Commands/HoleEditFeatureCommand.cs` (our "Edit Feature": no ribbon button, `kShapeEditCmdType`), `HoleFeatureReader.ReadForEdit` (the spec and the re-apply offer from one read of the hole), `AwbHoleWindowLauncher` using it and logging the differences, `HoleCommandTakeover.Owner`, the session's watch, and a note section in `AwbHoleWindow.xaml` under the Inventor-hole note.
  - The edit path is the existing one: every route calls `AwbHoleWindowLauncher.ShowForEdit`, which opens the window after Inventor's event has returned and brings an open window to the front.
- **What the re-apply comparison covers:** the diameter, tolerance and colour (084's comparison; a tapped hole's diameter is not compared, since its thread sets it and the writer never writes it); the seat type and how the hole ends; a blind hole's depth, or a tapped hole's drill and thread depths; a dowel's own counterbore diameter and depth, or countersink diameter and angle (task 106's follow-up). A depth stored as an expression is compared by what it comes to today against the part's parameters, so a hole that followed `d0` is not offered and one whose depth was changed in Inventor is (task 100's follow-up). Values the hole does not report, sizes that older stored data does not hold, and expressions that cannot be read now are skipped, so such a gap makes no offer. Two reads are not skipped: an extent Inventor cannot report reads as one AWB Hole does not offer, and a tolerance that cannot be read reads as none, so either can offer a re-apply (rare; the failed read is logged).
- **Defaults for the parent to log** (picked to keep work moving; Aidan may overturn):
  1. The offer is a note at the top of the window: "This hole was changed outside AWB Hole:", one line per difference, then "The AWB Hole settings it was made with are shown. OK re-applies them, with the fit, tolerance and colour; Cancel leaves the hole as it is." There is no separate Re-apply button. Until the user changes something, the preview shows the hole as it is now, not as OK will make it.
  2. Editing follows whoever has the Hole command: if taking over failed and Inventor's Hole was given back, Edit Feature and double-click stay Inventor's too.
  3. A selection of two different holes, or of a hole and something else, is left to Inventor. If Inventor's hole edit starts with no single hole selected, the backstop lets it run rather than stop it with nothing to open.
  4. As for the AWB Hole command (task 090), only a part open in the window is redirected; a hole in a part edited in place inside an assembly keeps Inventor's Edit Feature.
  5. If Inventor's Edit Feature icon cannot be read, ours uses the AWB Hole icon.
- **Not compiled (changed under `src/InventorAddin`):** none; every change was compiled on Windows. None of it has been run in Inventor.
- **Inventor API members not confirmed:** none by name. Each was used in spike 081 (commit `1ef94fa`, `Spike081.cs`) and checked against `Autodesk.Inventor.Interop.dll` by reflection: the `UserInputEvents` handlers for `OnLinearMarkingMenu` (`ObjectsEnumerator`, `SelectionDeviceEnum`, `CommandControls`, `NameValueMap`), `OnContextualMiniToolbar` (`ObjectsEnumerator`, `NameValueMap`, `NameValueMap`), `OnDoubleClick` (with `out HandlingCodeEnum`) and `OnActivateCommand`; `CommandControls.AddButton(def, bool, bool, string, bool)`, `CommandControls[name]`, `CommandControl.Delete` and `InternalName`; `NameValueMap.Count`, `Name[i]`, `Value[name]`, `Insert(name, value, index, before)` and `Remove`; `CommandManager.StopActiveCommand`; `ControlDefinition.StandardIcon` and `LargeIcon`; `Face.CreatedByFeature`; `PartDocument.SelectSet`. Behaviour not yet seen in Inventor:
  - the mini toolbar entry is inserted with the value Inventor gave its own entry, as the spike did; the spike never recorded what that value is, so if pressing it runs Inventor's edit, the backstop catches it;
  - whether our Edit Feature still finds the hole in `SelectSet` when pressed from the mini toolbar (it falls back to the hole the menu or toolbar was built for);
  - a double-click during a sketch edit, and on a patterned hole's face (whose `CreatedByFeature` is the pattern, so it is left to Inventor).
- **Manual checklist for Inventor** (a scratch part with a few holes made by AWB Hole: a counterbored clearance hole, a blind dowel, a tapped hole; and one made with Inventor's Hole; "Use Inventor's Hole command" off):
  1. Browser right-click on the counterbored hole: the menu shows one "Edit Feature" where Inventor's was. Click it: the "Edit AWB Hole" window opens on that hole with its settings; every setting kept (counterbore stays, dowel stays ticked). Cancel. The log has "AWB Hole: Edit Feature on Hole1 opens the AWB Hole window."
  2. Left-click a face of the dowel: the mini toolbar shows Inventor's Edit Feature icon; click it: our window on the dowel.
  3. Double-click a hole in the browser, then double-click a face of a hole in the model: our window each time, never Inventor's Hole dialog.
  4. With our window open, right-click another hole > Edit Feature, and double-click another hole: the open window comes to the front, the session is not cancelled, and picking hole centres still works.
  5. Edit the hole made with Inventor's Hole: the window says it was made by Inventor's Hole command and OK makes it an AWB hole. OK, then edit it again: it opens as an AWB hole with no note.
  6. A hole made with Inventor's Hole (switch on), then switch off: Edit Feature opens ours, and OK makes it an AWB hole.
  7. Switch on ("Use Inventor's Hole command", Save): right-click > Edit Feature and double-click open Inventor's Hole dialog again. Switch off: ours again, without a restart.
  8. Re-apply: switch on, change the blind dowel with Inventor's Hole (a new diameter and depth, and remove the tolerance), OK. Switch off, open the dowel in AWB Hole: the window lists each change ("The hole diameter is ...; AWB Hole made it ...", "The depth is ...", "The tolerance is none; ...") and says OK re-applies. OK: the hole is back at the stored size, depth and tolerance, still coloured. Undo once: Inventor's version is back. Open it again and Cancel: nothing changes.
  9. A dowel whose depth is `d0` (task 100): change `d0` in Parameters, then open it: no offer. Change the hole's depth in Inventor's Hole to a number, then open it: offered, naming `d0` and its value.
  10. A dowel with a counterbore (task 106): change the counterbore in Inventor's Hole, then open it: the counterbore diameter and depth are offered.
  11. If Inventor's hole Edit Feature can be started some other way (for example the radial marking menu, if it lists it): Inventor's dialog does not stay open and our window opens. The log has "Inventor's Edit Feature on ...".
  12. Open an assembly, edit a part in place, double-click one of its holes: Inventor's own edit opens (not redirected).

## Follow-ups

- Review, fixed by the parent: `_offeredHole` is cleared at the start of each menu and toolbar event, so a hole left from another part's menu is not opened; `HoleCommandTakeover._owner` starts as Inventor; the `ShowForEdit` comment names `ReadForEdit`. Rebuilt on Windows.
- A depth stored as an expression and replaced in Inventor by a plain number of the same value is not offered, though the link to the parameter is gone.
- With the window open, the backstop's `PartNGxHoleEditCtxCmd` cancels the session (unsaved edits lost) and the window reopens on the new hole while the log says "brought to the front"; confirm in Inventor.

- The re-apply offer has no button of its own: OK re-applies and Cancel leaves the hole. A "Re-apply" button that previews the stored settings before OK would make the preview match the window; ask Aidan whether the note alone is enough.
- A tapped hole's thread is not compared (the stored size is the workbook's `M6`, Inventor's designation `M6x1`), so a thread changed in Inventor's Hole is not offered. It could be compared through `HoleInventorNames` and the Tapped row.
- A clearance or tapped hole's counterbore and countersink sizes come from the workbook row and are not stored, so they are not compared; a seat resized in Inventor's Hole is not offered, and OK puts the row's sizes back.
- `HoleFeatureReader.ReadSpec` and `ReadAppliedState` are no longer called (the launcher uses `ReadForEdit`); remove them or keep them for other callers.
- `HoleEditRules.Redirects` takes "the document open in the window is a part"; when task 105 makes AWB Hole work in a part edited in place, the redirect should follow it.
