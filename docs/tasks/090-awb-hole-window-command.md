# 090: AWB Hole window and command

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command", "The window"); `docs/features/08-window-style.md`
- **Status:** done
- **Depends on:** 085, 087, 089
- **Needs:** windows build
- **Parallel-safe with:** 086

## Goal

The AWB Hole command and its modeless window in the add-in's shared look, bound to the view-model and driving the session, on the AWB Addin tab for part files.

## Scope

- `src/InventorAddin/UI/AwbHoleWindow.xaml` and `.xaml.cs`, `src/InventorAddin/Commands/AwbHoleCommand.cs`
- `UI/WindowHost.cs`: a modeless show (owner Inventor, the shared theme, header icon, fit to screen, modeless keyboard interop through `ElementHost.EnableModelessKeyboardInterop`), opened through `Dispatcher.BeginInvoke` after Inventor's event or command has returned (spike 081 round 3)
- `RibbonLayout` and its tests: AWB Hole (large, `IconNames.AwbHole`) on the Part ribbon's AWB Addin tab, Create panel (Default)
- `RibbonSetup`/`CommandNames` as needed; spec 01 and spec 08 rows

## Acceptance criteria

- [x] The window follows spec 09's field order in the shared look (sections, field rows, footer with status, OK and Cancel), passes the XAML rules test, and is never taller than the screen.
- [x] Opening the command in a part opens the window with the default centres picked and the preview showing; in other documents the button is not shown (part ribbon only).
- [x] Only one AWB Hole window at a time; pressing the command again brings it to the front.
- [x] OK and Cancel as the session (089); Esc is Cancel; Enter is OK only when OK is enabled.
- [x] An API to open the window on an existing hole in edit mode, for 092.
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx` builds

## Notes for the implementer

- `MaterialFinishWindow` is the closest existing window for layout and bindings.

## Verification

Filled in by the implementer.

- **Ran:** on Aidan's PC (Windows, Inventor 2026 installed, not deployed): `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off`: Build succeeded, 0 errors, 4 warnings (the existing CS8600 ones in `CreatePrintCommand`, `NewPartCommand`, `SaveAsCopyCommand` and `SaveAsReplaceCommand`; MSBuild lists each warning line twice; none in the new or changed files). Run again after the review fixes: same results. `dotnet test tests/InventorAddin.Core.Tests`: 3215 passed, 0 failed (3158 before this task's tests; the new `AwbHoleWindowViewModelTests` and `HolePickingWatchTests` add 57). The XAML rules tests include `UI/AwbHoleWindow.xaml` and pass. Nothing was run inside Inventor.
- **What was added or changed:**
  - Core, `AwbHoleViewModel` (085): `TerminationOptions` is now per instance: Distance and Through All, with To only for an edited hole that already ends at a face, plus `TerminationNote` saying the end face cannot be picked yet. Depth boxes bind `ThreadDepthText`, `DrillDepthText`, `DepthText` (the user's number format or a point; text that is not a number stays with "Enter a number." and disables OK; `TryParseNumber`, `FormatNumber`), with `LengthUnitText` (mm, in). Centres row: `CentresText`, `CanPickCentres` (set by the session), `CentresHelp`. `ShowNotes(notes)` puts the writer's notes for a hole that was made (tap drill fallback, "made again at the end of the browser") in the status line, not as an error, with the details listing them first. `ForReadBack(workbook, partName, HoleReadBackResult, problems)` picks `ForEdit` or `ForInventorHole` (for 092). `WorkbookStopMessage(read)`: a workbook with no Clearance, Tapped or Dowel rows stops the window with the file's problems. `WrongDocumentText`.
  - Core, `HoleSessionRules.cs`: `HolePickingWatch` and `HolePickingStop` (below). `CommandNames.AwbHole`. `RibbonLayout`: AWB Hole, large, Create panel, Part ribbon only, after the small buttons.
  - Add-in: `Commands/AwbHoleCommand.cs` (`AwbHoleCommand`, an `AddinCommand` with `kShapeEditCmdType` that does its own part check, so every press reaches the launcher first; `AwbHoleWindowLauncher` with `ShowNew(PartDocument)`, `ShowForEdit(PartDocument, HoleFeature)` (false when a window is open or about to open, so 092 can say so), `CommandPressed()`, `IsOpen`, `Shutdown()`); `UI/AwbHoleWindow.xaml(.cs)`; `UI/Controls/ColourSwatch.cs` (the dowel's fit colour, the one data colour a window draws, styled in `Styles.xaml`) and `UI/Controls/VisibleWhenTextConverter.cs`; `WindowHost.ShowModeless` (the dialog's preparation moved into `Prepare`, shared by both); `RibbonSetup` registers the command; `StandardAddInServer.Deactivate` calls `AwbHoleWindowLauncher.Shutdown()` so an open session is cancelled.
  - `HoleSession` (089): calls `ShowNotes(result.Notes)` after each change Inventor took (it clears a refusal too); sets `CanPickCentres` false for an edited hole with no sketch; listens to `UserInputEvents.OnActivateCommand`; `ResumePicking()`; the interaction events can be made again (`StartInteraction`, `DropInteraction`).
  - Specs: spec 01 "Panels today" Create row; spec 08 ribbon icon list and a new "AWB Hole" window section.
- **Pressing AWB Hole while the window is open** (089 follow-up). Starting any command may make Inventor stop the picking (`InteractionEvents.OnTerminate`), which cancels the session. The session now records each command Inventor starts (`OnActivateCommand`) in Core's `HolePickingWatch`; when the picking stops and the last command started is `Awb_AwbHole`, it drops the terminated interaction events and starts new ones through `Dispatcher.BeginInvoke`, instead of cancelling. The command's own deferred handler brings the window to the front and calls `ResumePicking()` (which also starts the picking if it was stopped, and forgets the recorded command so a later Esc still cancels). **A refused press clears the watch too** (review): every press of AWB Hole first calls `AwbHoleWindowLauncher.CommandPressed()`, before the document check, which queues `ResumePicking()` on the open session through `Dispatcher.BeginInvoke`. So a press refused because no part is open, or because a part is edited in place in an assembly, cannot leave "AWB Hole started" recorded and turn a later Esc or Extrude into a restart that would run inside the session's transaction. `CommandManager.ActiveCommand` is deliberately not used: while our picking runs it might name AWB Hole, which would turn Esc and other commands into a restart and let another command run inside the session's transaction. **This relies on Inventor raising `OnActivateCommand` before `OnTerminate`, which is not confirmed** (checklist step 4). If the order is the other way, the session is cancelled (the part is left as it was) and a fresh window opens: nothing is lost but the window's settings.
- **Not compiled (changed under `src/InventorAddin`):** none: every changed and new file compiled against the Inventor 2026 interop. None of it has run in Inventor.
- **Inventor API members not confirmed** (each compiles; none has run from this code):
  - `CommandManager.UserInputEvents` and `UserInputEvents.OnActivateCommand` (the spike hooked them, round 2): **the order of `OnActivateCommand` and `InteractionEvents.OnTerminate`** when a ribbon button is pressed while our picking runs, and **whether `OnActivateCommand` reports an add-in button by its `ButtonDefinition` internal name** (`Awb_AwbHole`). The spike saw it report Inventor's own names (`PartNGxHoleEditCtxCmd`).
  - Whether **pressing our own button stops the picking at all** (`OnTerminate`), and whether new `InteractionEvents` made and started after Inventor terminated the first ones work as the first did (picking, `OnSelect`, `OnTerminate`).
  - `ElementHost.EnableModelessKeyboardInterop` and an owner from `MainFrameHWND` for a modeless window: the spike's window used both and ran, but they are not in its confirmed list. Whether **Esc, Enter and typed letters reach the window and not Inventor** (Inventor runs the message loop, and letters are its shortcuts; checklist 17a to 17c), and whether Esc pressed in the model still stops the picking.
  - `Dispatcher.CurrentDispatcher.BeginInvoke` from a button's `OnExecute` to open the window, as the spike did from its own command (confirmed working there, round 3).
  - `PartDocument.FullFileName` and `PartDocument.DisplayName` for the header subline (existing code reads them from `Document`).
- **Manual checklist for Inventor** (this task's steps, with the steps 088 and 089 left for the window). Use a scratch block with a sketch of three hole-centre points; build with Inventor closed so the add-in deploys.
  - **Opening**
    1. Part ribbon, AWB Addin tab, Create panel: AWB Hole is a large button with its icon, after Save As Copy. It is not on the Assembly, Drawing or no-document ribbons.
    2. AWB Hole with nothing selected: the window opens (header "AWB Hole", the part's file name), the newest visible sketch with hole centres is used, all three points are blue and a hole is at each; Centres says "3 picked". Select another sketch (or one of its points) first: that sketch is used instead. (089 step 1)
    3. The window takes keyboard input at once (Units has focus, Tab moves through the fields, access keys work), and it is never taller than the screen (on a small screen the body scrolls and OK and Cancel stay visible).
    4. **Press AWB Hole again while the window is open:** the window comes to the front, nothing is cancelled (the holes stay, the values stay), and clicking a point still toggles it. Then press Esc in the model: the session is cancelled. **If instead the window closes and a fresh one opens, report it** (Inventor stops the picking before it reports the command; see above). Check `awbaddin.log` for "AWB Hole was started while its window is open".
    4a. Straight after step 4 (AWB Hole pressed again, window to the front), start Extrude from the ribbon: the session must be cancelled and the window close, with the part as it was. Extrude must not run while the window is open.
    4b. Part edited in place in an assembly (double-click a part in an assembly), press AWB Hole: the message "AWB Hole works in part files." and nothing changes. Do it twice: with no AWB Hole window open, and with one open on another part (open the part, start AWB Hole, switch to the assembly window, edit a part in place, press AWB Hole). In the second case, go back to the part and press Esc in the model, then start Extrude: each must cancel the session (a refused press must not make a later Esc or command restart the picking).
    5. A workbook with no sizes (a Settings path to a missing file): a message names the file and no window opens.
  - **Picking** (089 steps 2 to 4)
    6. Click a point: it goes from the hole and loses its blue; again: it comes back. No Ctrl needed. Click every point off: no hole, OK disabled, Centres says "None picked" with "Pick at least one hole centre."; the sketch is still there. Click one back on: the hole comes back.
    7. On a new hole, click a point of a different sketch: the picks start again in that sketch with just that point; one hole in the browser, in the new sketch.
    8. The sketch is visible while the window is open. OK: the sketch ends hidden. Cancel: the sketch is as it was before the command.
  - **Values and the preview** (this task's steps 2 to 4; 088 steps 1 to 6, 8 to 11)
    9. Termination lists Distance and Through All only (no To). Clearance, no seat, Through All, each of Close, Normal, Loose: the hole is made at every centre, into the material. Flip: the hole cuts nothing and the status line says "Inventor could not make this hole..." with the reason in its tooltip; the last good hole stays; the next valid change works.
    10. Distance: Depth appears with "mm" (or "in") beside it and OK is disabled until a depth is entered. Type a depth, press Tab: the hole follows. Type "abc": "Enter a number." under the box, OK disabled, the hole unchanged; fix it: the message goes. 118 deg, 90 deg and Flat drill points: the bottom changes; measure the cone angle.
    11. Counterbore and countersink: Fastener type appears; the seat sizes match the workbook row; change the size up and down several steps: no refusal. An invalid value (a hole larger than its counterbore, by editing the workbook if needed) is refused with a message and the last good hole stays.
    12. Tapped M6 (or any metric row): Thread depth and Drill depth appear for Distance; measure the modelled diameter: it is the workbook's tap drill, not Inventor's minor diameter. An imperial row: if the status line (or the log) says the thread is modelled at Inventor's minor diameter, report it (the note now shows in the window's status line).
    13. Blind tapped M6 with 9 mm thread and 12 mm drill: the thread depth in the model is 9 mm, not 90 or 0.9. The same in imperial (0.4 in thread, 0.5 in drill): 0.4 in. A shallower drill depth with a thinner thread depth, then deeper again: no refusal.
    14. Change a clearance hole to tapped and back while previewing: the hole stays in the same place in the browser and keeps its name. If the status line says the hole was made again at the end of the browser, note it.
    15. Dowel (Standard AWB Dowel) with an ISO row (H7) and a Limits row: Fit shows Slip, Normal, Press with the fit's colour square and name; Tolerance lists the rows for the fit and preselects the Default one. After OK: the fx dialog shows H7 or the limits, the model is at nominal, and every face, the bottom too, is red, blue or green by fit. Only one "AWB Press fit" (etc.) appearance in the part after several holes; change the RGB in the workbook, make another hole: the colour changes on all holes of that fit. A Limits row of +0 / -0.02 mm: the lower deviation shows as minus.
  - **OK, Cancel, Undo** (this task's step 5; 088 step 7; 089 step 6)
    16. OK, then one Ctrl+Z: the whole hole goes (feature, colour, tolerance, data, sketch visibility). Cancel, Esc in the window, and the window's close button: the part is exactly as before.
    17. Enter in a depth box commits the value and does OK when OK is enabled; with OK disabled, Enter does nothing. Enter with a dropdown open picks the entry and does not do OK.
    17a. Click in a depth box and type letters that are Inventor shortcuts (e, h, d), then "abc": the letters go in the box, no Inventor command starts (no Extrude, no Hole, no Dimension), and the session is not cancelled; the box shows "Enter a number." until it is fixed.
    17b. Esc in the window is Cancel: the window closes, the part is as it was, and Esc is not passed to Inventor (no other command is stopped or started).
    17c. Open a dropdown (Size) and press Esc: only the dropdown closes; the window and the session stay. Esc again: Cancel.
  - **Other ways out** (089 steps 8 to 11)
    18. While the window is open, close the part (or Inventor): no error; the window closes. At the save prompt answer Yes, reopen: no preview hole.
    19. Start another command from the ribbon (Extrude): the session is cancelled and the window closes. Orbit, pan, zoom and the ViewCube do not cancel it.
    20. After OK, Cancel, closing and an error: no blue highlight left, and clicking in the model selects normally.
    21. On a large part, a change over 500 ms appears in `awbaddin.log` as "preview ... took N ms".
  - **Editing** (088 step 12; 089 step 7; needs 092's Edit Feature or double-click, which call `AwbHoleWindowLauncher.ShowForEdit`)
    22. Edit an AWB hole: header "Edit AWB Hole"; every setting loads from its stored data; its own centres are blue; points of its sketch can be added and removed, points of another sketch do nothing. Cancel leaves the hole as it was. Change a dowel into a clearance hole, OK: no tolerance, no colour, and the `AWB_Hole` data holds the clearance hole; one Undo brings the dowel back.
    23. Edit a hole made by Inventor's Hole: the note says OK makes it an AWB hole, with the guesses under it. A hole ending at a face lists To, with the note that the face cannot be picked yet; OK is refused until Distance or Through All is chosen.
    24. A hole placed on something other than sketch points (if one can be made): Centres says its centres are not sketch points; clicks do nothing.
  - **Look**
    25. Light and dark themes: every row, the fit colour square (bordered, readable on both), error lines and the status line; the title bar is dark in the dark theme.

## Follow-ups

- **Defaults for the parent to log** (task 090 had to pick; Aidan may overturn): (a) AWB Hole is on the Part ribbon only (sheet metal parts included, since they share the Part ribbon), large, last in the Create panel after the small buttons; (b) the command needs the part to be the document open in the window: a part edited in place inside an assembly gets "AWB Hole works in part files" (the session's transaction, picking and highlight are on the part; not tried); (c) Termination To is not listed for a new hole until the end face can be picked; an edited hole that ends at a face lists it, with a note; (d) depth boxes update when they lose focus or on Enter, not on every key; text that is not a number disables OK; (e) a workbook with no Clearance, Tapped or Dowel rows stops the window with a message; rows with problems only count in the status line; (f) the window does not remember its position (no state key); (g) pressing AWB Hole (or 092's edit) while the window is open on any hole brings it to the front; `ShowForEdit` then returns false (also while a request is queued and the window is about to open) so 092 can say so; (h) a part edited in place in an assembly is refused even when a window is open on another part.
- The log prefix "AWB Hole: " in the 088 and 089 `Holes/` files (`HoleFeatureWriter`, `HoleFitWriter`, `HoleWorkbookLoader`, `HoleSession`) is now `AwbHoleViewModel.CommandName + ": "` (review), so the name is not repeated outside Core.
- **Termination To needs a face pick** (088 and 089 follow-ups): still not built. Spec 09 lists To; it is left out of the window for new holes. A task should add the face pick (a second selection mode in the session for a face, passed as `endFace` to the writer) and put To back.
- **Pressing AWB Hole while the window is open** depends on Inventor's event order (see Verification). If checklist step 4 shows the window closing and reopening, the alternative is to defer the cancel in `OnTerminate` until after the command has run, which risks another command running inside the session's transaction; ask Aidan before doing that.
- **Switching to another document while the window is open** is still not handled (089 follow-up): the outer transaction stays open on the part.
- **Spec 09 "The window"** does not list the Centres row or Depth (for a blind clearance or dowel hole), which the window shows; the parent may want to add them to the spec's field table.