# To do

Everything that needs doing and is not already a task in `docs/PLAN.md`. Kept current by the parent session: items are added when they come up and removed when they are done or turned into a task. Where the project stands is in `STATUS.md`; why things are the way they are is in `DECISIONS.md`.

Last updated: 2026-11-20

## Next up

1. M5, drawing tools (spec 05): planned 2026-11-21 as tasks 183 to 209 (`docs/PLAN.md`). Aidan takes a last look at `docs/ui-mockups/title-block.png` (blue numbers 18 to 25); then spikes 183 and 184 in a local session with him, while the Core tasks can run. M4 is done: part 1 PR #29 (2026-11-10), part 2 PR #32 (2026-11-20).
2. **Later, not this round** (Aidan, 2026-10-25): every AWB icon gets a more polished look, done together with the colour option.
3. Aidan finishes the remaining Part Properties checks below.

## Waiting on Aidan: checks in Inventor

**M4 part 2** (163 to 181) was checked from 2026-11-18 to 2026-11-20: every step passed (180 reversed by Aidan and reverted as 181, which passed). Merged in PR #32 on 2026-11-20.

**Spike 160, left open (not blocking):** which command made VIEW2's ordinates, "Ordinate Dimension Set" or single "Ordinate"? The API listed no set.

**M2 run 2** was checked on 2026-10-11. Six bugs and six changes became tasks 052 to 061; every other run 2 check passed.

**M2 fix run** (053 to 061) was checked on 2026-10-12: everything passed except occurrence names in a closed assembly, now task 065. Aidan also asked for four items, now fix run 2 (062 to 066).

**M2 fix run 2** (062 to 066) was checked on 2026-10-12: every check passed.

**M3** (068 to 076) and its fixes (077 to 080) were checked on 2026-10-17: every check passed. Merged into main from `claude/m3-material-finish` on 2026-10-17.

**M2 run 1** (037, 038, 039, 042): Aidan reported on 2026-10-17 that the remaining checks passed.

**M4 part 1, 090** was checked on 2026-10-18: steps 1 to 4a, 6, 7, 10, 12 to 21 and 25 passed (step 5 skipped). Steps 8 and 11 and the tapped thread display failed, now tasks 093 to 095; step 4b and Aidan's other requests are tasks 096 to 105. Steps 22 to 24 (editing) wait for 092.

**M4 part 1, 091 to 106** (not 103, 105) were checked on 2026-10-25. Failed, now fix tasks: 091-3 (120), 091-4 (121), 096-3 (122), 099-3 (123), 100-7 (124), 106-5 (125). Every other step passed or was accepted as it is (092-4, 098-2, 099-5), except:
- Skipped, not needed: 091-6, 095-4, 099-2 (covered by 106), 106-8, 106-9
- 095-5 answered: Inventor's own Hole dialog, editing an AWB tapped hole with no changes, keeps the bore at the tap drill, not the minor diameter.

**M4 part 1 round after PR #29's first look** (105 to 138; 103, 107 and 108 were spikes with Aidan) was checked from 2026-11-07 to 2026-11-09. Aidan did section 1 in Excel and the steps marked [MANUAL] in `docs/checklists/m4-round-after-pr29.md`. A temporary self-test covered the measurable steps (spike 139, local branch `spike/139-selftest`, never merged; run 2: 128 PASS, 2 FAIL, 13 SKIP, each skip done by hand). Every step passed except the failures below, now tasks:
- 122-6 and 122-8 (hand colours on a remade hole): task 140.
- 133-1, 133-2 and 133-5 (the sketch point mini toolbar loses Hole, Edit Sketch and Create Sketch; AWB Hole is not added): task 141. 133-3, 133-4 and 133-5a passed.
- 105-1 (picked centres not highlighted in place): task 143.
- Found during the check: the marking menu's Hole opens Inventor's Hole (task 142).
- Aidan's requests: type order (144), a browser sketch picks its centres (145), countersink angle Custom... (146), arrow keys between fields (147), icon redesign and light-mode colours, mockup first (148, from 118-10).
- Noted passes: 114-11 (blind tapped M6 with a 1 mm spotface, thread 7.5, drill 10: the thread starts at the spotface floor and ends 8.5 from the top face, plain bore and point below), 110-4, 118-7, 138-11.

**M4 part 1 fixes 140 to 150** were checked on 2026-11-10 (`docs/checklists/m4-fixes-140-150.md`): everything passed except 140-2, now task 152; Flat Pattern and 145-3 became decisions, now tasks 153 and 154. **Fixes 152 to 154** were checked on 2026-11-10 (`docs/checklists/m4-fixes-152-154.md`): every step passed. PR #29 merged into main on 2026-11-10.

Aidan's first look (2026-11-07), answered 2026-11-09: cut-off text is task 149, a typed ISO code in Custom tolerance is task 150, Flat Pattern keeps Inventor's Hole (`DECISIONS.md`), and dimensions on the preview are parked under "Proposed work".

From Part Properties (task 019):

Confirmed so far: the window opens on an assembly, pre-fills Designer, Detailer and part type A, writes Description, Designer and Cost correctly, one Undo reverts an Apply, and the blank Part type entry can be clicked.

- [ ] Clear Detailer and Part type, apply: the custom properties are deleted from the Custom tab, not left empty
- [ ] A read-only file opens with every field disabled and a line saying why
- [ ] A part with `Part Type` set to `m` in Inventor's own dialog shows `M`
- [ ] A new, never-saved part: Apply still writes Designer and Detailer (the header and part number rows were confirmed on 2026-10-07: New part, Not saved yet, Not assigned)
- [ ] With "Show developer tools" switched off and Inventor restarted: is the Developer panel gone, or does Inventor keep showing it? (open since task 007)


**After 227 to 236:** recheck that an imperial print shows a period (the check of 226 could only make metric prints of an inch part).

## Waiting on Aidan: decisions

Each spec lists its own open questions in full. These are the ones that block planning.

**AWB Hole (spec 09), not blocking**
- Question 29: should switching units in the window load that units' last hole? Default: no, the last hole applies only when the window opens (task 173)


**Material and finish (spec 03), not blocking**
- Questions 10 to 17 in spec 03: ambiguous appearance names, where preferred materials are recorded, Show paints, the Finish Thickness text, the starter library's materials, sub-assemblies in a selection, Passivate's AppliesTo, the ribbon button. Each has a default
- The finish appearances are spike 067's suggestions for now; Aidan edits `libraries/finishes.csv` if he wants others

**AWB Hole (spec 09), not blocking**
- Question 16 in spec 09: a seated blind clearance, dowel or Simple hole's Depth is still measured from the top face (Default); or from the bottom of the seat, like the thread depths?
- Question 17 in spec 09: a toleranced clearance or Simple hole takes the colour of its Precision Tol row's fit, Custom the normal colour (Default); or one new precision colour?
- Question 18 in spec 09: Custom tolerance is in the dowel list too (Default); or dowels keep only the workbook rows?
- Question 19 in spec 09: a Simple hole's diameter and seats, and a Taper Tapped seat, start from the last values used, else empty (Default); or from workbook defaults?
- Question 21 in spec 09: Inventor models a taper tapped hole at its own tap drill; the Taper Tapped tab's Tap drill column feeds notes and tables only (Default); or drop the column?
- Question 22 in spec 09: no Thread depth box on a taper tapped hole, since Inventor threads the table's effective length (Default); or a spike round to try setting it?
- Question 27 in spec 09: a workbook Counterbore or Dowel seat row still needs a counterbore depth above 0 (Default), though the window allows 0; or may a row hold 0?
- The Defaults dated 2026-10-24 in `DECISIONS.md`: the Spotface and Taper Tapped tab layouts, invented NPT and BSPT starter rows, Simple's Diameter box, the tolerance list for clearance holes

**Hole table (spec 04), not blocking**
- Questions 9 to 17 and 19 in spec 04: seated sub-order, a seated threaded hole, the sort diameter, where a split goes, letters, inch decimals, description wording, holes not seen end on, several origins. Each has a default the Core tasks build
- Question 20 in spec 04: Renumber on a re-opened table waits for Update to redraw (Default); or should it redraw at once?

**Drawing tools (spec 05), blocks M5 tasks**
- Spec 05 question 32: the smallest text height for a long title block value (Default 1.8 mm). Question 33: an off-units print's FINISH and WEIGHT are drawing properties written by Create Print (Default), or the part carries both units. Question 34: a hand-typed sheet metal Material Size is replaced (Default), and whether it follows the print's units. Question 35: assembly dashes written into the model (Default) or on the drawing only. Question 36: the suggested sheet is the first giving 1:2 or larger, else the largest (Default)
- Spec 04 question 26: the hole tag leader distance (Default 5 mm from the hole's edge, set on Update)
- Last look at the title block mockup with the 2026-11-21 additions (`docs/ui-mockups/title-block.png`, blue numbers 18 to 25; the FINISH line is gone and 18 is now the finish notes on the sheet) before task 185 is built
- Spec 05 questions 13 to 23: the M5 defaults (zones, Project property, notes rules, marking sizes and colour, Batch Print, REF dimensions, stock shapes, Make/Buy blanks and manufacturer properties, Create Print's sheet, template built from code, the Finish field's text and the finish notes' order). Each has a default the tasks build
- M5 planning questions 14 to 17 (from an earlier session; their wording is not in the repo): ask again when those commands come up (Aidan, 2026-11-20). Question 18 is answered by the Make/Buy column (2026-11-21)

**iLogic (spec 06), blocks M6**
- Which rules, and whether they are internal or external

**Window style (spec 08), not blocking**
- Questions 1 to 8 in spec 08: accent colour, dark title bar, icon style, and five defaults added while planning (never-saved header and part number, theme names, Inventor version row, the XAML rules test)

**Numbering (spec 02), not blocking**
- Questions 13, 15 to 17, 19 to 22 and 26 in spec 02: a well-formed number not in the register, what to do with duplicates, Save As and Replace on every occurrence, opening Part Properties after creating a file, a register row that cannot be read, Mass Update's Detailer, a registered number on two files, a file named only `AWB-10001.ipt`, and Mass Update assigning a number an unticked file carries. Each has a default the tasks build. Question 14 was answered and 18 settled on 2026-10-11; 27 was answered on 2026-10-28 (task 135).

**Distribution, not blocking**
- Whether and when to add an installer and a download page. It was to be decided when M2 was planned and is still parked

**Smaller, not blocking**
- Should About check for a newer version? (spec 01, question 2)
- More fields for the properties window: revision, vendor, vendor part number, project, notes? (spec 07, question 1)

## Proposed work, not yet agreed

- **Lightweight PDM on the home server** (Aidan, 2026-11-20; long term, not M5), building on the numbering register on the NAS: revision and status set by a release action in the add-in, check-out and check-in locking, release history, released PDFs stored on the NAS. First check whether Aidan's Inventor licence includes Autodesk Vault Basic: Vault is not installed on this PC (only Inventor Professional 2024 and 2026); check the entitlements at manage.autodesk.com.
- **Editing AWB Hole's dimensions on the preview in the model** (drag handles and typed value boxes, as Inventor's own Hole does). Parked by Aidan on 2026-11-09 as a later idea, not this round.
- **Reviewer styling checklist and a Windows build script.** Both are written into spec 08 as proposed additions to M1b. Aidan has not said yes or no.
- **Part name editable in the properties window.** Aidan asked on 2026-10-06 whether the name can be edited. It stays display-only, linked to the file name; the Rename button beside it is now planned (task 047). Waiting on Aidan only if he wants the name to differ from the file name.

## Follow-ups from finished tasks

- Task 222, Mass property (Aidan's check passed 2026-11-23):
  - Later: remove the lb mass unit step from the template builder; an imperial sample value for the fit check; the backfill warnings in Create Print and Material and Finish do not mention Mass.

- Task 234, live preview:
  - Views are labelled boxes, not the model's outline; the title block is a plain band. A label that does not fit its box is skipped. Check light and dark themes and 150% scale.

- Task 233, front view choice:
  - Back, Bottom and Left cameras are worked out, not yet seen in Inventor.
  - The iso stays the standard top-right iso whatever the front choice; its fallback size estimate now uses the chosen front's box. Ask Aidan whether the iso should follow the chosen front.
  - The Top and Right tick boxes mean "above" and "beside" the chosen front; their labels mislead after choosing, say, Left.
  - Auto uses the axis-aligned range box, not `OrientedMinimumRangeBox` (a part lying at an angle gets a looser view). Auto equal to a named view is measured twice (cache key); `SelectedFront` reads `_sheet.Units` rather than the selected units.

- Task 232, units and sheet size:
  - Unconfirmed in Inventor: writing FormattedText and Edit/ExitEdit on a drawing's own title block definition; `Document='drawing'` resolving custom properties; custom property writes to a drawing; scratch measuring run from inside the open modal window. All best effort, with a notice.
  - An off-units print's Finish Spec and Mass refresh on Create Print only (question 33); Mass Update could refresh drawings if Aidan wants it.
  - Material Size stays in the part's units on an off-units print (question 34).

- Task 231, assemblies on the print:
  - Writing text to an assembly's Design Tracking Material is unconfirmed; it is best effort (logged, never fails the other writes). If Inventor refuses, point MATERIAL at a custom property on assemblies.
  - After 232, the dashes could move to the drawing instead of the model (spec 05 question 35).

- Task 230, sheet metal Material Size:
  - When the Stock Size command is built (205, 206), make its number format agree with this one (spec 05 shows "PL 12 x 65 x 130", no suffix) and decide whether it skips sheet metal parts.
  - A flat pattern out of date after a thickness edit may give the old flat size; watch on Aidan's check.

- Task 229, sheet metal front view and bend notes:
  - Whether `BendNote.UsePartUnits = false` gives the print's units for the bend radius is unverified; if not, set the units through the bend note style.
  - The iso is still the standard top-right iso, not turned to match the new front view; it may look inconsistent.
  - Bend notes sit where Inventor puts them; overlap with flat pattern geometry is not handled.

- Task 228, black and white and iso everywhere:
  - The sheet metal iso sits above the folded front; check it shows the folded part (no folded-model option is passed) and does not overlap a tall flat pattern. Its position may change after Aidan's check.

- Task 227, fit check (views are measured in a hidden scratch drawing before the window opens):
  - The cause of the iso looking a different scale was not found; each base view's scale is now checked and set to the plan's, with a log warning. Watch the log on Aidan's check.
  - Measuring places the views twice; if slow on a large assembly, measure only front and iso, or cache per model.
  - Re-check the measured iso after 228 changes its style. Dimensions and marks added later can still make a view larger than measured.

- Task 237, values on one baseline:
  - Labels and fixed text with descenders now drop up to about 0.47 mm onto their baselines (intended); the rotated small part number keeps lower justification (upper not tried on rotated text).
  - Hole table text (`HoleTableMarker`) still uses lower justification.
  - The row test in `PrintTemplateFitTests` recomputes the formula; a stronger one would check every field cell in a row gets the same origin.

- Task 226, decimal period (Aidan's check passed 2026-11-23, 0 template warnings):
  - The frame style's alternate marker is not set (needs alternate units); only matters if alternate units are ever shown.
  - Core test runs often fail 1 to 3 timing-sensitive probe tests, a different one each run; worth making them deterministic.

- Spike 225, for the tasks that use it:
  - 233: the base face edge's sign is arbitrary; choose the up vector so the view is not upside down (229 uses `FaceViewRule`).
  - A single ordinate dimension failed ("Value does not fall within the expected range"); `OrdinateDimensionSets.Add` works. Probably needs an origin indicator first (not tried).
  - `HoleThreadNotes.Add` needs `LinearDiameterType = false`; with `Missing` it fails with E_FAIL.

- Task 224, Finish Spec refresh:
  - A spec in task 211's first format ("(long name), 0.013-0.025 mm", written before task 213 the same day) is not recognised and is left as hand-written. Add that form if Aidan has such parts.
  - Mass Update refreshes only files it opens to change (a ticked change, for example a rename), as in task 217.
  - Doc comments slightly stale: `MaterialFinishCommand.BackfillFinishSpecs` ("Fills in a missing Finish Spec") and the first sentence of `FinishSpecBackfill.For`.

- Task 223, title block:
  - Field values are no longer checked for vertical overflow (only labels are); could be restored for the vertical edges.

- Task 193, Open Drawing:
  - When Create Print fails after Open Drawing's Yes, the message and log say "Open Drawing failed".
  - The no-print question and the unsaved message say "part" on an assembly too (as Create Print's messages do).
  - Check in Inventor that `Documents.Open` on an already-open print returns that copy (checklist step 2).

- Task 192, Place Initial Views:
  - `PrintPlan.ChooseScale` and its tests are unused now; remove them unless another command wants a single-view scale.
  - Placed view sizes are not read back, so an iso estimated too small (or an assembly whose range box is much larger than its geometry) can overlap a neighbour.
  - Only the drawing's first referenced model is used; a non-AWB sheet's border and title block are assumed, not read.
  - Check on the first prints: the front view is now hidden-line-removed (was hidden lines shown), and the flat pattern lies Length across, Width up.
  - Spec 02 still says Create Print uses Inventor's `ANSI (mm).idw` / `ANSI (in).idw`; it uses the AWB template since task 188.

- Task 188, Create Print:
  - `Documents.Add` fails while the template is open in Inventor, and the message does not say so; a hint could name the cause.
  - The scale rule still sizes against the whole sheet; move it to the usable area when task 192 wires up the initial views.
  - The sheet swap (`AddUsingSheetFormat`, deleting the template's sheet) runs outside the transaction; try it inside once the view work is checked.
  - Not run in Inventor: `Sheet.Activate` then deleting the template's sheet leaves one sheet ("1 of 1").

- Task 220 (done in round 3):
  - When Create Tables draws only one kind, an already placed table of the other kind that cannot fit is left where it is and not reported.
  - A hand-deleted middle part is not repaired until the next Create or Update.
  - Check in Inventor that `Border.RangeBox` leaves out the zone labels on the AWB templates.

- Task 210, check in Inventor:
  - At 150%, the Status box shows "WORK IN PROGRESS" in full; it sizes to its text, so it may change width (remove `HorizontalAlignment="Left"` to stretch it).
  - Material and Finish's backfill can write Revision and Status while Part Properties is open, without the window re-reading them (a harmless rewrite on Apply).
  - Typing a revision directly is not offered.

- Task 217 (reviewer):
  - Mass Update logs "Finish Spec filled in" before the write, so it is logged even when the transaction aborts. A failed backfill write there also aborts the file's real Mass Update changes.
  - In Material and Finish, one part that cannot be written aborts the whole backfill transaction for the other parts.
  - Create Print skips the backfill when it opens an existing print.
  - `RunInTransaction`'s doc comment still says "on the new drawing".

- Task 215: the mockup caption (`docs/ui-mockups/title-block.html`) and task 185's notes still say "20 mm filing edge" (now centred). In the 7 mm part number row, the Status value keeps the mockup's lower baseline.

- Long term (Aidan, 2026-11-22): a PDM's release action takes over Revision and Status from the add-in.

- Task 213:
  - The inch-or-foot rule lives in the add-in (`MaterialFinishTargets.UnitsOf`), while `HoleTableViewRules.UnitsFor` in Core also counts mil, yard and mile as imperial. Move it to one Core rule.
  - `IMaterialFinishTarget.cs:11` still says the target applies `PropertyWrites`.
  - An unreadable unit silently counts as metric.
  - Parts finished before 213 keep the mm-based Finish Spec until the finish is applied again.
  - Check in Inventor that µ shows in the iProperty and on the print.

- Task 190: `InitialViewsResult.Fits`' doc still says only "the smallest standard scale" (also false when a chosen scale is too big). With no views ticked and a fixed scale, `ScaleText` shows 1:1 (OK is disabled). For task 192: bind the scale ComboBox with `DisplayMemberPath="Label"` (the suggested label changes at run time). `RenamePlanTests.ListFilesToSearch_AFolderThatDoesNotAnswer_IsSkippedAfterTheProbeTimeout` failed again in the reviewer's run.

- Task 189, for tasks 190 and 192: Top and Right are projected from Front, so ticking either forces Front on. Task 192 maps the range box to `InitialViewsBoxCm`, supplies the flat pattern size (or null), and checks that the flat pattern view is Length wide by Width high. The usable area ignores the triad, parts list and NOTES list.

- Task 186: the print template rows sit in Settings' "Material and finish" section, whose heading no longer covers them (rename?). The collapsed summary shows a set template as a bare file name, and nothing when blank. "_Imperial print template" shares Alt+I with "Use _Inventor's Hole command". The starter template files only exist after task 187.

- Task 185, for task 187's check in Inventor: on ISO sheets the rotated small part number clears the zone frame by about 0.2 mm (if it touches, raise `SmallPartNumberEdgeInsetMm`, for example to 2.5). Whether `AddFitted` anchors at the baseline start or a box corner decides the margin text's exact place. The title block outline is 0.7 mm per the mockup (cells 0.35). Spec 05 questions 24 to 26 hold the open layout questions.

- Task 211: two add-in comments still say "four finish iProperties" (`MaterialFinishCommand.cs:23`, `MaterialFinishTargets.cs` near line 131). Parts finished before 211 get Finish Spec only when the finish is applied again.
- Core tests: Numbering tests failed intermittently in task 211's runs, a different one each run (`SaveAsReplacerTests.Replace_NameThatCannotBeSet_KeepsTheReplace_LogsIt_AndSaysSo`, `RenamePlanTests.ListFilesToSearch_AFolderThatDoesNotAnswer_IsSkippedAfterTheProbeTimeout`); the reviewer's run passed all 4972. Probably timing-dependent.

- Task 178: the precision table's position is read with its height (`ReadPlacement(...)?.Position`), so if its height cannot be read a table the user moved is treated as new and restacked; if the clearance table's position cannot be read after the redraw, it is treated as deleted. Both unlikely (the height is a stored attribute).
- Task 182: a hole whose workbook Tapped row's tap drill is edited after the hole was made (thread unchanged), with Inventor's diameter unreadable, now prints the row's tap drill rather than the stored one. The once-per-hole warning set is static and never cleared (fine for one session).
- Task 179: the cause of the lost drill line is not proven. The warning `... is tapped but its tap drill could not be read` is now logged once per hole per session (task 182).
- Task 176: unsaved drawings (no file name) are never checked automatically. The timer ticks every 500 ms while a check waits for a drawing that is not active. `WindowHost.IsAnyOpen` does not count a WinForms `MessageBox`. Whether `_ActiveCommand` and `ActiveEditObject` behave as assumed is shown by the log's "Hole table check: waiting" lines (checklist 176-1).
- Task 175: `HoleTableStoredViewCheck` still lives in `HoleTableWizardViewModel.cs` (namespace `ViewModels`); move it into `HoleTable`. The wizard still writes the literal "Update failed. " rather than `HoleTableMessages.UpdateFailedPrefix`.
- Task 172: opening Hole Table still writes view keys for the current sheet on first use (task 163), before the write-free check; a keyed-only listing on open would need a Core change. "Update these views" has no access key. Per-view Update results are logged as one joined line.
- Task 169: `SaveAsReplacerTests.Replace_TopLevel_CopiesAsSaveAsCopy_...` is flaky (temp `cad` folder missing in about 2 of 9 full runs).
- Hole tags and marks (task 165): tags, precision marks and centre marks carry `<Brand>_HoleMark` (`HoleId`, `ViewKey`, `Role`), separate from 166's table-state set. Attribute sets on placed sketched symbols and on centre marks are proven by no spike (checklist step 6: marks not doubled on a second Create). After a swap, regenerating only Precision leaves the old precision mark on the swapped-out hole. A hole that fails halfway keeps its partial marks; a failed attribute stamp doubles the marks on the next redraw. Thread edges drawn as arcs are not read. The tag gap uses the tag text height, not the data text height the spike used. The tag text style comes from a second temporary hole table (the drawer's style set could carry it). A reused `<Brand>_HoleTag` definition keeps its old text style if the drawing's tag style changes. A circle that already has a centre mark gets a second one.
- Hole table drawing (task 164), for task 166: a definition for a table no longer in the plan is left on the sheet; Update should delete it. The height attribute (`<Brand>_HoleTableDrawing`, `HeightCm`, plus `SheetName` and `ViewName`) may fold into 166's stored state. Generate one table uses the whole plan's shared column widths, so it can differ from the other table drawn earlier; Create Tables or Update redraws both. Definition names are about 89 characters (two GUID keys); if Inventor refuses them, shorten the keys. `AlignTables` reads `sheetName` and `viewName` and never uses them (dead code). Untested at runtime: `Edit(out DrawingSketch)` early-bound, attribute writes on a definition, `def.Sketch.SketchLines` outside edit mode.
- Hole table window (task 167): the Hole Table icon is a copy of AWB Hole's (four `UI/Icons/HoleTable.*.png`); replace them when Aidan sends a design. Toggle View Names is in no task. The busy bar is a stopgap driven from code-behind: it will not animate during a command (Loaded fires after the Render pump) and its storyboard repeats after collapse; a real busy or progress flag on the view-model should drive it. The Cancel button's XAML comment wrongly says IsCancel closes the window (it only maps Esc). `CommandDocuments` lists HoleTable as Edited; TopLevel would be more direct. Check focus after Split or a Notes edit rebuilds a list (view-model fix if lost). Tasks 164 to 166 must start and abort one transaction per action, since `HoleTableCommand` holds none while the window is open.
- Hole table source (task 163), for tasks 164 to 167: `HoleTableSource.FindHole` holds what the last `Analyze` of that view read; 164 and 165 call `Analyze` again before `FindHole`, since COM objects go stale after a model change. Only `kCircleCurve` drawing curves are kept; add `kCircularArcCurve` if 164 or 165 needs arcs. The 166 members are placeholders ("Not yet available"). The 163 manual checklist needs 167's window; run it with the combined check. Listing sheets and views writes key attributes on first sight, which marks the drawing modified.
- Task 163 review, later: the tolerance build in `HoleTableSource` (`IsoCode ?? HolePrecision.CustomName`, missing deviations as 0) could be a Core factory beside `HoleTolerance`; `FindSheet`/`FindView` return the first match, so a sheet or view copied after a listing shares a key until the next listing; the log context `"AWB hole data"` (`HoleTableSource.cs`, `HoleFeatureReader.cs:29`) repeats the brand instead of `Branding.ShortName`.
- Hole table Update and Edit Table (task 166): Update redraws in place but does not move tables, so a table that grows may overlap the one stacked above until Align. A mark moved by hand goes back to its default place when that hole's tag changes. A tag edited by hand on the sheet is not noticed (Update works from stored tags). Records of deleted views stay in the sheet's `AWB_HoleTableViews` set. A view whose stored state cannot be read is not listed, so Create could overwrite it. `MarkHoles` analyses the view again on every Update with holes to mark.
- Hole table stored state (task 162), for tasks 166 and 167: 166 implements `ReadState`, `UpdateTables` and the `state` argument on `CreateTables` and `GenerateTable`, stores `HoleTableStateJson.Write(state)` in the attribute set, and logs `HoleTableRefresh.Summary`; check one real hole id (feature name plus reference key) in Inventor, since the 32 KB test assumes about 75 characters. 167's Edit Table tab binds `StoredViews`, `SelectedStoredView`, `OpenStoredViewCommand`, `Groups` (Notes) and `UpdateCommand`.
- Task 162 review: `HoleTableStateJson.Read` accepts any `nl` and `hn` value; a corrupted huge `nl` would overflow `Letter(next++)` in `Refresh`. Treat negative or over 18278 (ZZZ) as unreadable. `HoleTablePlan` now carries `HighWater`, so two plans with the same rows but different marks are not equal; mind it if a later task compares plans.
- Hole table layout (task 161), for task 164: a toleranced group with a seat or thread line is drawn as one stack text plus one text per later line, centred as a block; spike 160 never drew this, so check the stack sits evenly over its two slots and the gap to the next line matches. `IHoleTableTextMeasure` must measure the formatted text as a fitted note renders it (as the spike did), not the raw markup. Check a view name with an apostrophe (`&apos;` in formatted text is untested in Inventor).
- Task 156 (seen in 161 review): a toleranced hole with no drill diameter attaches its tolerance to `Lines[0]`, which would then be the thread line, so the stack lands in the thread text.
- Flaky test (seen during task 159): `RenamePlanTests.ListFilesToSearch_AFolderThatDoesNotAnswer_IsSkippedAfterTheProbeTimeout` (RenamePlanTests.cs:564) failed once and passed on a re-run. It depends on a probe timeout; give it more margin or a fake clock.
- Task 156: a taper thread's description uses Inventor's `ThreadDesignation` as given (for example `1/4`, with the type `NPT` not prefixed). Check on the first Inventor run (task 163 to 166 check) whether the thread line reads right, or needs the type in front.

Small things noticed while building. None is urgent. The parent turns these into tasks when planning a milestone that touches the same code.

- Icons (task 151): the 16 px drawings are the implementer's own pixel fits; Material and Finish and Mass Update at 16 px are the busiest. The ribbon Developer and AWB accents and the dark ribbon colour (`#3A4150`) are mockup stand-ins, not measured from Inventor; change `RIBBON_PANELS` in `tools/icons/render.py` and the SVG sources together. The ribbon SVGs came from a one-off script that is not kept, so palette changes are by hand. `IconChoiceItem` no longer sets `Foreground`; a text-only icon row would need its own colour.
- Hand colours in place (task 152): if a centre moves more than 0.5 mm in the same change that adds a seat, the new seat takes no colour (old faces are classified against the new centres, as in `Recreate`); reading the old hole's own `HoleCenterPoints` would fix it. `ReadHandColours` logs an Info line on every live edit now; consider Debug.
- Flat pattern (task 153): the AWB Addin tab's AWB Hole button also runs Inventor's Hole in a flat pattern; change if Aidan prefers a message. Unconfirmed in Inventor: `PartNGxHoleEditCtxCmd` run from our stale Edit Feature edits a flat pattern hole. `HoleEditRules` remarks still say editing does not work in place (out of date since task 105).
- AWB Hole in place (task 105): check the switch rule with a new, unsaved assembly or part (names are empty; only the object comparison is left). In place, picked sketch points and edited holes are not checked to belong to the part being edited; if Inventor ever offers another component's, guard on the document (reviewer: the double-click would open the window on a hole of another part).
- Commands in place (task 109): Part Properties' property writes and Generate start their transaction on the edited part, not `ActiveDocument` (only Change beside Material does); 109 checklist step 5 passed (one Undo in the assembly reverts Designer), so this stays as it is. `MaterialFinishCommand`, `AwbHoleCommand` and `HoleEditRedirect` still read their documents directly instead of through `AddinCommand.CommandTargetDocument()`; move them when next touched.
- Save As and Replace occurrence name (task 134): if the name set fails and the sub-assembly save then fails too, the thrown save message hides the name note (only the log has it); append it. `NewPartCreatorTests` has no direct test of `Notice` with `Note`. `OccurrenceNameKeptMessage` could name the copy's file to rename to. Untested in Inventor: `proxy.NativeObject` after `Replace` accepting a `Name` set, one Undo reverting both, a throwing `Name` set leaving the transaction able to end.
- Rename of AWB-format occurrence names (task 135): `PartNumber.TryParse` trims white space, so `NAME, AWB-10017:1` also counts as an AWB name and is renamed (as file names are read); a stricter match is one check in `RenamePlan.AwbNameDigits` if Aidan wants it. Renaming a file with no number renames a stale AWB-named occurrence of it to the plain new name (`WIDGET_2:1`). 
- AWB Hole split button (task 129): the Settings note under "Use Inventor's Hole command" (`SettingsViewModel.UseInventorHoleCommandNoteText`) still says only "Takes effect when you save"; it could say the dropdown toggle sets the same switch at once. Reviewer: `InventorHoleRunRequest` keeps its request when `PartHoleCmd` starts, so after Inventor Hole a later mini toolbar Hole may run Inventor's Hole. 129 checklist step 6 passed, but the mini toolbar's Hole was broken by then (task 141), so check this again after 141; if it does, clear the request on a `PartHoleCmd` start or drop the `Ask()`.
- Seat kept (task 123): the Fastener type is not remembered when a seat comes back (it takes the list's earlier entry if still listed, else the first); Aidan may want it remembered like the seat. Spotface and Taper Tapped seats go through the same `RefreshSeats` fallback when they land; task 126 widens "offers it" there.
- Typed values kept (task 124): when the Simple hole Diameter box and the Custom tolerance Upper and Lower boxes land (tasks 113, 132), they use the same typed record (`MarkTyped` in the setter, `SettleBox` in `Refresh`). Reviewer: a typed thread depth can exceed a smaller size's untyped drill depth (M5 thread 10, then M3 drill 6); check the window says so rather than Inventor refusing. A typed seat value shown converted is saved as the last value for the other units' size on OK.
- Dowel seat last values (task 125): the 106-5 diameter loss (9 to 7) did not reproduce in Core, and Aidan's `settings.json` had no `6 mm` entry; 125 checklist step 7 settles it from the new "remembered the seat" log line. Reviewer suggestions: log the lookup (size key, seat found or none) when a dowel window opens, and log "already remembered" when OK keeps the same values.
- Hand colours kept (task 122): colours are carried step to step while the window is open, so a seat removed in one change and added back in the next loses its colour (the window does not remember the colours it opened with). Unconfirmed in Inventor: a hand-coloured face reports `kOverrideAppearance`, the asset still applies after the old hole is deleted, the seat floor's `RangeBox` is centred on the axis (a counterbore cut by an edge would lose its colour), Feature Properties gives `kFeatureAppearance`. Core test gaps (reviewer): a countersunk through hole with no drill point.
- Seat boxes on every type (task 126): `HoleDowelSeat`, `HoleLastDowelSeats` and `AddinSettings.DowelSeatMap` now hold tapped seats too; rename (`HoleSeatSizes`, `HoleLastSeats`). `HoleDowelSeat.Valid` accepts 0 to 180 deg while `HoleRules` asks 1 to 179. Reviewer: a counterbore depth expression set in Inventor's own Hole dialog on a clearance or tapped hole is now replaced by its number on an AWB Hole OK (as dowels since 106); `ShowsFastener` counts the shown Fastener list while `SeatRowStart` includes hidden rows; no on-disk round-trip test for `LastTappedSeatsMm/In`. A hole OK'd by this build is stored as version 2, which older builds cannot read.
- Workbook Spotface and Taper Tapped tabs (task 111): task 112 needs a rule matching a tapped hole's thread (for example `1/4-20 UNC`) to a Spotface Size (`1/4`); task 114 checks the starter taper sizes against Inventor's thread table (spike 108 proved only NPT `1/4` and `Rc 1/4`). Aidan's own workbook copy needs the two tabs copied in from the starter. The starter workbook opens in Excel with no repair prompt (Aidan, 2026-10-30, 111 checklist step 1).
- Simple, Taper Tapped and Spotface in Core (task 112), for task 114: bind the new view-model properties (list in the task file), including `ShowsDrillDepth` (until then a blind taper tapped hole's Drill depth box is hidden); the writer uses the new geometry fields and the reader fills `TaperedThread`, the spotface fields and `DiameterExpression`; map `kSpotFaceHole` to `HoleSeat.Spotface` and retire `HoleFeatureMessages.SpotFaceNotOffered`; build the note when Taper Tapped drops a counterbore. 114's checklist: edit an Inventor hole of an odd diameter, it opens as Simple with its diameter. Later: a Simple diameter expression is compared by its stored number for re-apply (a depth expression by its value); seated Inventor holes with no matching row still open at the closest Counterbore or Countersink row (reviewer: could open as Simple with its own seat). Task 127: a counterbore depth of 0 is still refused (`HoleRules`, `HoleDowelSeat.Valid`).
- Clearance seat with no row (task 137): for task 114, check the window does not jump as Fit appears and disappears with the Size on a seated clearance hole. A size whose only seat rows are hidden counts as having a row (no Fit, empty Fastener type); should it count as no row? Seat rows at a size with no Clearance row can no longer be picked (they could never be built). Reviewer: an Inventor hole matching a Clearance size at a fit no seat row of that size uses opens at the first row and its through hole changes on OK without the note saying so; a stored hole whose fastener's size lost its seat rows now builds at Normal fit silently (and the reverse), so its through hole may change.
- Toleranced holes in Core (task 113; bound in the window by task 115): Reviewer: a Max, Min, Basic or Reference tolerance, opens on the default row and OK applies it (could stay untoleranced with a note instead); a dowel read back as Custom takes the Normal colour whatever its fit. Task 115: a tolerance on a Simple hole's diameter is untried in Inventor (115 checklist step 3); the Toleranced row is the window's only `FieldRow` with no label, so check it lines up.
- Hole icons (task 119): spec 08's sentence on the AWB Hole icons should name Simple, Taper Tapped and Spotface; an optional renderer in `tools/icons/` for the `Styles.xaml` hole geometry would give a review image (the scratchpad one drew from it). In Inventor: Spotface next to Counterbore at 100 percent is the closest pair, and Simple's arrow ends may touch the walls.
- Flaky tests (tasks 135 and 120): `NewPartCreatorTests.FailureAfterAssigning_ACloseThatFailsIsLogged_AndTheOriginalMessageIsKept` (the message named an `AppData\Local` folder) and `SaveAsReplacerTests.Replace_CopyFails_IsSaveAsCopysFailure_AndNothingIsReplaced` each failed once and passed on re-run; probably the shared real temp folder. Also `RenamePlanTests.ListFilesToSearch_AFolderThatDoesNotAnswer_IsSkippedAfterTheProbeTimeout` (timeout-based, task 128). `RenamePlanTests.ListFilesToSearch_ASlowListing_IsSkippedAfterTheListTimeout` failed once on a cold first run (task 110 review). Task 130 saw 1 to 3 file-system `Numbering` tests fail after about 3 s in some full runs, a different set each time, all passing alone (file I/O under parallel load, or OneDrive syncing the repo). Task 116 saw 3 to 5 `Numbering` tests fail per full run (also `SaveAsReplacerTests.Replace_NameThatCannotBeSet_...`), all passing alone; they could be made timing-independent or run in their own collection.
- Hand colours on a remade hole (task 140): when checklist step 1's log shows the source a Feature Properties colour gives the hole, record it (CLAUDE.md's confirmed list or task 122's Defaults) and narrow `IsFeatureHandSource` to a positive list if wanted. If step 2's log says "kept as the hole's own colour" with a coloured drill point, a face override moves the hole's own source; use the positive list. An old face whose shape could not be read counts as absent, so a new face of its role takes the bore's colour.
- Custom tolerance code (task 150): `Tolerance.ShaftTolerance` and `SetToFits` with a shaft class on a hole diameter are untried in Inventor; if Inventor refuses shaft classes (checklist step 8 shows the fallback warning), decide whether the window should refuse lowercase codes instead of applying them as deviations. Codes are now matched case-sensitively; if Inventor reports a code in another case, every edit would show a re-apply offer (checklist steps 5, 5a). Typed limits behind a code are not stored. The closed Tolerance dropdown could show the code instead of only "Custom".
- Arrow keys (task 147): whether the same rule belongs in spec 08 for every window (the Material and Finish window has its own arrow handling). Down on an empty part of the window body now jumps into the first field instead of scrolling.
- Browser sketch picks (task 145): the point-toggle path in `HoleSession.OnSelect` does not check that a point picked in place belongs to the part being edited, and `SameSketch`'s name fallback can mix up a same-named sketch of another document. If a browser click never reaches `OnSelect`, try the browser pane's node events. A refused sketch click shows only in the log.
- Highlight in place (task 143): check that `ActiveOccurrence.Definition.Document` is the part before using it, falling back to the part's own set (guards a part edited in place inside a sub-assembly, unchecked). Spike 103's result 5 (a part highlight set shows in place) was contradicted by 105-1; note it once 143 is checked.
- Marking menu Hole (task 142): sub-menus in the ring and the linear overflow menu below it are not searched for a Hole (the backstop still catches it). Once Aidan's log confirms the ring swap holds, drop the before and after ring lines to Debug. The flat pattern check falls back to an environment name containing "FlatPattern", a guess; checklist step 8 shows whether it matters. After "Inventor Hole", repeating the last command starts `PartHoleCmd` unasked and is stopped.
- Mini toolbar Hole (tasks 133, 141): `HoleEditRules` remarks still say editing does not work in place (stale since task 105). Once task 141's log shows what `PartDMHoleCmd`'s value is and whether the swap holds: drop the before and after Info lines to Debug (or once per session per part kind; they are written on every sketch point click), and consider passing Inventor's value as it is in the Edit Feature swap too (`value ?? _editFeature` in `OnContextualMiniToolbar`), checked the same way.

**Workflow**

- Watch the next few tasks after the implementer moved to Sonnet (2026-11-04): if reviews return CHANGES REQUIRED more often, or Inventor API code gets worse, move it back by removing `model: sonnet` from `.claude/agents/implementer.md`.

**Material and finish**
- Applies made through Part Properties' Change are named "Part Properties" in Inventor's Undo list; an optional transaction name on `DocumentEditCommand.RunInTransaction` would fix it. Part Properties keeps `kFilePropertyEditCmdType` though Change now also changes material and appearance; decide whether to switch to `kNonShapeEditCmdType`. Spec 08's Part Properties section should list the Change button and the warning line; a warning colour token and style would set the warning apart from help text. (task 076)
- Material and Finish window: the view-model's `CancelCommand` is unused (Cancel closes through `IsCancel`); a missing NAS drive gives the "cannot be read" wording rather than "missing" (add `DriveNotFoundException` to the missing filter); the lists scroll by pixel without virtualizing, fine unless a library is very large. (task 075)
- The Material and Finish log line names part file names and the library path, while Part Properties keeps file names out of the log; pick one rule for both. (task 074)
- `MaterialFinishViewModel` tests check that a hidden selection comes back but not that `PropertyChanged` fires for `MaterialListSelection`/`FinishListSelection`; add that if the window ever loses a selection. (task 072 review)
- The starter library (`libraries/StarterMaterials.adsklib`, about 1 MB) changes completely on every regeneration; regenerate and commit it only when `starter-materials.csv` changes. Properties other than density come from the stock template; per-grade strength would need more CSV columns. (task 071)
- The Browse buttons beside Material library and Finish list have no access key (every letter of "Browse" is taken); relabel or leave. (task 070)
- `CsvTable` (task 068) repeats the CSV splitting loop in `RegisterCsv`; `RegisterCsv.Read` could use `CsvTable.Parse`. (task 068)
- A finish list saved by Excel as plain "CSV" (not "CSV UTF-8") is in the Windows code page, so a non-ASCII character in a print note would read as a replacement character. Whoever reads the file (task 075) could fall back to the ANSI code page when the bytes are not valid UTF-8. (task 068)

**AWB Hole**
- Edit routing (task 092): a Re-apply button that previews the stored settings before OK, if the note is not enough. Not compared for re-apply: a tapped hole's thread (`M6` stored, `M6x1` reported), and an expression replaced by a plain number of the same value. An unreadable extent or tolerance can make a re-apply offer. `HoleFeatureReader.ReadSpec` and `ReadAppliedState` are no longer called.
- Sheet Metal takeover (task 128): if the startup log warns that no Sheet Metal panel was found but Inventor's Hole is on that tab once a sheet metal part is open, re-apply the takeover when a sheet metal part is first opened. The tab id `id_TabSheetMetal` and panel `id_PanelP_SheetMetalModify` are unconfirmed; the checklist's step 1 log line shows the real names.
- Hole takeover (task 091): `HoleTakeoverRules.For` is unused by the adapter (task 120); remove it or use it in `GiveBack`. Taking over clears a shortcut the user gave Inventor's Hole, and giving it back restores H, not their key (remember it if wanted); giving back clears any key the user gave AWB Hole. After a crash H stays on AWB Hole until the next start. 
- Window redesign (task 104): the Tolerance closed box still hides the row name with a deliberately failing `ComboBoxItem` ancestor binding (a template selector in `UI/Theme` would be cleaner). A hole ending at a face is drawn as a through hole in the diagram. The diagram frame has no fill (the mockup's panel colour is not a spec 08 token). Clearance and Seat None icons look alike (different rows). Arrow keys in an icon row rebuild the preview on every press; check it is not sluggish on a heavy part
- Dowel seats (task 106): the Dowel tab's autofilter still covers A:D only (widening it changes `xl/workbook.xml`'s `_FilterDatabase` too). A non-number in one of the optional seat columns drops the whole Dowel row; dropping only that value would be kinder. A wrong-type `lastDowelSeatsMm`/`In` value in a hand-edited settings.json makes the whole file fail to load, like other typed settings.
- Redo of an AWB Hole (task 101 spike): `RedoTransaction` after one Undo did not bring the hole back; check Ctrl+Y in Inventor
- Depth expressions (task 100): an expression changed in Inventor's Parameters dialog after OK is not seen by Edit, which opens an AWB hole on its stored expression (task 092 now compares a stored expression by its value today)
- Seats on tapped and dowel holes (task 099 review): the same-size match is within 0.0001, so 0.312 in one tab and 0.3125 in another give no seat (a per-unit tolerance would be kinder); two Clearance sizes with one nominal (`#5` and `1/8`, both 0.125 in) would both be offered; switching Standard from a seated AWB Clearance to AWB Dowel goes to the first dowel size, not the nearest. The window mockup (102) could put Fastener type under Size, since for tapped and dowel holes its list follows the size
- Aidan's own hole workbook has no "Blind depth multiplier" row on its Settings tab (added to the starter in task 097); without it 2 is used. Copy the row across to change it. Aidan may prefer the name "Blind depth (x D)" to match its neighbours (the reader would then accept both)
- The Settings window saves a copy of the settings taken when it opened; it is modal, so today nothing else can change them meanwhile. If it ever becomes modeless, merge `LastBlindDepth*` and `PreferredMaterials` from the current settings on save (task 097 review)
- Tapped holes (task 095): check on a drawing what Inventor's thread notes and hole tables show now that a hole's minor diameter minimum is the workbook's tap drill. `HoleThreadNames.NominalSize` is no longer used; a tap drill change could be made in place instead of remaking the hole
- In the 090 check two AWB Holes in a row started on the same sketch seconds apart; task 094's start log line says how the sketch was chosen. If it says "newest visible unconsumed" right after an OK, the sketch is not ending hidden in the visible UI: a new bug (task 094)
- In `HoleFeatureWriter.Change` the seat is ordered against the hole diameter only; a Distance counterbore made deeper while the hole is made shallower in one step (or the reverse) is not ordered against the depth. Not reached with workbook rows; fix if "Inventor refused the change" shows in the log for it (task 093)
- ISO 286 tables (task 083) were typed from the standard's values and checked from memory by the reviewer, not against a copy of ISO 286-2; worth a spot-check, mainly sizes over 250 mm. K9 to K11 over 3 mm and N9 to N11 up to 1 mm are refused (readings of the standard's footnotes). JS7 to JS11 round an odd IT value down before halving, as ISO 286-2 prints it; if the Precision Tol tab ever holds a JS code, compare one odd-IT size with Inventor's own fit. (task 083)

- The AWB Hole icon (task 087) is drawn in the current single-colour style; recolour it with the others once a ribbon icon colour option is chosen. Its 16 px version has no hatching (too narrow). `tools/icons/render.py` could skip writing a PNG whose pixels already match, since Pillow 12 re-encodes unchanged icons. (task 087)

- AWB Hole: termination To needs a face pick (a second selection mode in the session, passed as the end face to the writer); until then To is left out for new holes. Switching to another document while the AWB Hole window is open is not handled: the outer transaction stays open on the part. Recreating an existing hole that has dependent features has not been tried. (tasks 088 to 090)

**Create Print**
- If `ANSI (mm).idw` is missing beside the `.dwg` template but `standard.idw` exists, Create Print uses `standard.idw` without saying so; its units may not match. Only the log records it. Also, the template lookup runs before the existing-print check, so the log can warn about a missing template when a print exists. (task 063)

**Rename**
- The two-step occurrence set (temporary name, then new name) and putting the original name back are add-in code that the Core fake target does not model; only Aidan's checks cover them. In a closed assembly the stored old name cannot be read, so a failed second set leaves the occurrence under `AWBRENAME-…` (reported). A Core test pinning case-only stems through `RenamedOccurrence` would help. (task 065)

**Startup tab**
- The startup watch cannot tell a tab Aidan clicks from Inventor's own switch back, so a click in the first 5 s can be overridden once. If that is a nuisance, shorten `StartupTabPlan.WatchWindow` to 2 or 3 s (spike 062 measured the switch at 0.5 to 1.5 s). When the tab is not found the log has a warning but no "stopped" line. (task 066)

**Build and workflow**
- Core tests that probe or list a real temporary folder (NewPartCreator, SaveAsCopyCreator, SaveAsReplacer, RegisterStore, RenamePlan) often hit the 3 s probe or list timeout on Aidan's PC: in one review five of six full runs had 1 to 5 such failures. A rerun usually passes. Give these tests a fake probe or a longer timeout so the suite is reliable. (tasks 055, 057 reviews)
- `Forms.Cursor previousCursor = Forms.Cursor.Current;` gives a CS8600 warning in CreatePrint, NewPart, SaveAsCopy and SaveAsReplace; typing it `Forms.Cursor?` clears all four. (task 063)
- There is no `global.json`, so the cloud and Windows may build with different .NET SDKs. Pin it if builds start to differ. (task 001)
- **Make the deploy line visible in a plain `dotnet build`.** On Aidan's PC the `Deployed add-in to ...` line only appears with `-tl:off`: the terminal logger hides high-importance messages. Make it show in a plain `dotnet build InventorAddin.slnx` (for example a `Directory.Build.rsp` with `-tl:off`, or another way the terminal logger prints), then update step 3 of "Aidan's part" in `docs/WORKFLOW.md` to match. (task 025, Windows check 2026-10-07)
- **Cut the deploy copy retries.** With Inventor open the build succeeds, but the `NOT deployed` case prints about 10 warnings, one per `Copy` retry. Set `Retries` low (0 or 1) on both copies so it is a line or two. Any copy failure is still reported as a lock; the MSBuild warning above it shows the real cause. One attempt with Inventor open crashed with `MSB4166` (child node exited); the rerun was fine. (task 025, Windows check 2026-10-07)

**Ribbon and shell**
- The agent definitions say "the AWB Addin add-in", which repeats "add-in". (task 011)

**Ribbon icons**
- Rendering icons on Windows (Pillow 12.3.0) re-encodes 12 existing 32 px PNGs with identical pixels but different bytes; restore them with `git checkout` after a render, or make `render.py` skip writing a PNG whose pixels match the file on disk. At 16 px the second hatch line of `MaterialFinish` touches the ring at (12,8); shorten it if it ever shows. (task 073)
- `RibbonSetup.AddButton` returns early when a control with the command's name is already on the panel, so if Inventor ever keeps the add-in's controls between sessions a size change would not show. Only if a button shows its old size. (task 056; task 121 now deletes our controls on load and unload, so close this once 121's reload check passes)
- When Inventor refuses the icons but has already created the definition, the log says "the button shows text only" and then that the definition was reused; the first line may be untrue. (task 042)
- Task 030: embed only `UI/Icons/*.png`. The `.svg` sources sit in the same folder and must stay out of the build. (task 024)

**Window style**
- The working area is read when a window opens, so dragging it to a smaller screen does not re-cap it. (task 077)
- Settings: an access key for a field in a collapsed section does nothing; it could expand the section. A save error for a setting in a collapsed section does not expand it; the message names the setting. (task 079)
- Only Settings is resizable and remembers its state. Mass Update and Material and Finish, with their long lists, may want the same; any window can opt in with `ResizeMode="CanResize"` and a state key. (task 079)
- The remark in `UiTheme.cs` still says the dark-theme rule waits for spike 020; name the confirmed `LightTheme` and `DarkTheme`. (task 026)
- Settings: the gap between a control and its note is the full row gap (9). If it looks loose, add a shared `HelpText` style. (task 028)
- Settings: the restart note lines up with the checkbox's box, not its label text. (task 028)
- A cloud check project that compiles the add-in's XAML and WPF code against stub Inventor types would catch XAML errors before Aidan builds. Proposed, not agreed. (task 026)

- Developer commands get no icon while developer tools are off. (task 030)

- XAML rules test: C# in `UI/Controls/` is not checked, `DynamicResource` keys are not checked to exist, and a size binding with a typed `FallbackValue` passes. (task 031)

**Settings window**
- `FieldRow` centres its label, so "Other folders" sits halfway down the four-line box. A top-aligned label for tall rows would fix it. (task 049)
- The shared text box template hides scroll bars, so the four-line Other folders box gives no sign it holds more lines. (task 049)
- The register folder's "not a full path" message does not name the register folder; the window now has three folder settings. (task 049)
- A shared style for a button beside a text box (for example `BrowseButton`), so Browse can sit on the register folder row. New Part's folder field will want it. (task 037)

**Part properties**
- Author: when only Author needs fixing, the window shows the general "Pre-filled values will be written when you apply." line, though no field changed; consider its own wording. If the Default designer is also listed under Other names, Part Properties never copies it into Author, so a Windows-name Author stays; skip the Author write only when the Designer means you and is not the Default designer. (task 055)
- One Apply writes one log line plus one per property. Keep the summary line and log the per-property lines only on failure. (tasks 018, 019)
- Weight is read through code that also computes volume, area and centre of mass. On a large assembly the window may open slowly; read only the mass, or read it after the window opens. (task 018)
- `PartPropertiesViewModel.Ok()` has no guard for invalid values when called from code. The window cannot reach it today. (task 016)
- Correcting a part type's case shows the "pre-filled values" wording, which is not quite accurate for it. (task 016)
- Number parsing accepts oddly placed group separators, so `1,2,3` reads as 123. Tighten only if it confuses anyone. (task 013)
- The Part Properties window now sizes to its content, so a long status message widens it, up to 560, until the message clears. If that is distracting, fix the width in `UI/Theme/`. (task 029)
- The window's local `ValueOrPlaceholder` and `IdentifierOrPlaceholder` styles could become shared pieces. (task 029)

**Numbering**
- Case-only Rename: a file left under the temporary name has no number in its name, so running Rename again keeps the number only when the register holds it (an unregistered number cannot be typed back); soften the message or put the number in the temporary name. If step 1 kept the old file, renaming the temporary file to the final name is refused while the old file exists. An occurrence renamed to `~RENAME-…:1` keeps that name if step 2 cannot rename it. Untested in Inventor: `SaveAs` to the final name right after the delete writes the new case; `~` in an occurrence name; `FindOpen` finding a drawing reopened under the temporary name. (task 061)
- Copies: a Part Number that cannot be read back is reported as "blank after saving" and the exception is not logged; log it in `InventorNewDocument.ReadPartNumber`. The selection log uses the COM type name, not `ObjectTypeEnum`. (task 060)
- Rename drawings: a drawing that was open but hidden (loaded by something else) is reopened visibly. If a renamed drawing's old file cannot be deleted, both files stay (both point at the renamed part) and a note asks to delete the old one by hand. Untested in Inventor: whether `Documents.Open(path, true)` activates the reopened drawing, and `Save` followed by `SaveAs` on an invisibly opened drawing. (task 059)
- Rename: when an occurrence's new name is already taken in the assembly, that occurrence keeps its name and a note says so; the add-in does not pick another number. Occurrences in sub-assemblies are renamed only through the sub-assembly's own update. Untested in Inventor: whether a closed assembly's occurrences read the old or new path after `ReplaceReference` (the code accepts either), and setting a name on a suppressed occurrence or with several model states. (task 058)
- Create Print: an old print found beside the model is opened where it is, not moved into `Prints`; the "already exists" question does not say where the print was found. (task 057)
- Names: only the view-models add the `ASSY_` prefix; `NewPartCreator` and `SaveAsCopyCreator` take the request as given, so a new caller must use `PartFileName.ForKind`. A name pre-filled from an old lower-case file shows in lower case in the box until edited (`CharacterCasing` only changes typing). Reserved names are checked on the typed name, so `CON` is refused for an assembly though the file would be `ASSY_CON`. If reading the kind fails, Rename adds no prefix. (task 054)
- Mass Update: a file whose number was already registered but whose names were replaced is reported as an error, not a change; give `FileUpdateResult` a note field. A first-sync `IOException` says "a number may already have been taken" when none was; use that message only part way. Parts left loaded by an assembly closed earlier may show as "Open in Inventor" (try `Documents.CloseAll(true)` if so). The window does not redraw during the sync (about 8 s) or a slow folder listing. (task 048)
- Rename: Inventor freezes while CAD folders are listed (up to 3 s per unreachable folder, 60 s per slow one); move the wait off the UI thread with progress. The Rename window is owned by Inventor, not Part Properties (needs an owner overload in `WindowHost`). The result list uses the four-line text box with no visible scroll bar. Spec 08 should list the Part name row with Rename. (task 047)
- Save As and Replace: `kReferencesChangeCmdType` may suit it better than `kShapeEditCmdType`. Saving the sub-assembly with `Save()` may save its other unsaved files or show a dialog; consider `SilentOperation` around it and say so in the warning. `((AssemblyComponentDefinition)x).Parent` returns the sub-assembly document directly and avoids the unconfirmed `.Document`. Spec 08 should name the button. (task 046 review)
- Save As Copy: rename `NewPartException` and `NewPartResult` to neutral names now that copy commands share them; `NewPartCommand.cs` probably has an unused `using System;`. Spec 08 should list the Save As Copy icon and dialog. (task 045)
- Create Print's messages say "part" for assemblies too. An existing print is offered before the template is looked up. "metric"/"imperial" wording is in both `NewPartCreator` and `PrintPlan`; share it. Spec 08's icon list should name Create Print. (task 044)
- New Part writes its properties in a transaction, so one Undo on the new part clears Part Number and Description in memory; saving then leaves a file without its number while the register keeps the row. Spike 032 found no transaction is needed on a new document: drop it, or keep it if Aidan prefers (043 checklist step 12). (task 043 review)
- New Part checks `File.Exists` on the target while holding the register lock; a NAS folder dropping at that moment could hold the lock 8 to 42 s. Time-box it like the folder probe. (task 043 review)
- New Part's checklist does not exercise a save failure after the number is assigned (try a folder without write permission). The dialog shows "Enter a part name." in the error style as soon as it opens; a muted style until typing may read better. The dialog accepts any folder text until Create. (task 043)
- Spec 08 should record the New Part dialog layout (File, Identity, Location) and add New Part to its "Ribbon icons" list. (task 043)
- The "Part number ... assigned" log lines in `RegisterStore.Assign` name the number and file, not who assigned it. Add `AssignedBy` if the log should say. (task 050)
- Generate part number: offline it probes the register folder twice (sync, then assign), so a hung drive costs about 6 s, not 3; let `Assign` reuse the sync's result. The window freezes meanwhile with no wait cursor or status. (task 039)
- Generate: when the re-check refuses ("already has a registered number"), the sync's duplicates are only logged, and the window does not refresh to show the number. (task 039)
- Generate: each failure is logged twice, by `RegisterPartNumberAssigner` and by the view-model. A failed write after assigning leaves the button enabled, so a retry uses up a second number. A success message is hidden behind a pending validation message. (task 039 review)
- Register store: spike 032 confirmed the 3 s probe timeout is right. An unreachable path blocked `Directory.Exists` for 8 to 42 s, so a timed-out probe's background check can live that long. Make sure abandoned probes cannot pile up against a hung drive, for example by reusing one in flight. (task 036, spike 032)
- Register store hardening: in `TakeLock`, an `IOException` raised as the other PC releases its lock escapes instead of retrying; write the local copy before deleting the pending file; a failed write after `CreateNew` leaves our own lock behind. Lock-file limits to know: clock skew over 2 minutes between PCs can clear a live lock, and release does not check the lock is still ours. (task 036 review)
- An unreadable local copy reads as empty, so offline numbering would restart at AWB-10001 until the next sync reports the duplicates. (task 036 review)
- `ScanFolder` skips hidden and system files and follows directory junctions (the `EnumerationOptions` defaults). Say so in its doc comment or set it deliberately. (task 041)
- The Metric and Imperial option list is built in both `SettingsViewModel` and `NewPartViewModel`. A shared `UnitsOption.All` would keep the text in one place. (task 040)
- New Part's folder is checked only for blank and path length. Task 043 should add a full-path and invalid-character check like the register folder's, or make the folder Browse-only. (task 040)
- `NewPartViewModel.NumberPlaceholder` repeats the `AWB` prefix; build it from `PartNumber.Prefix`. (task 040)
- New Part: picking the blank part type while the kind is Part, then switching to Assembly, sets the type to A. Check this is wanted when 043 is tested. (task 040)

**Developer tools**
- JSON exports include `BoundingBox` lengths, which are derived values. Mark them ignored if exports should not carry them. (task 001)
- Tolerance levels (task 132): spec 09 "Dowel and precision holes" could state the two new workbook rules (a Precision Tol name is used once per units; with two Default = Y rows for a fit and units the first wins and the second is reported). A hole made with the old starter names ("Dowel pin, standard press", "Location fit", "Slip fit, removable") and edited against the new starter keeps its name as an extra entry and blocks OK with a Tolerance problem until a row is picked; a view-model test could pin that down. A Precision Tol row named "Custom" is still silently left out of the list; the reader could report it.
- Centre-drill spot (task 127): `HoleDowelSeat.Valid` keeps a remembered countersink angle between 0 and 180 deg while `HoleRules` asks for 1 to 179 (also noted by task 126). `EditableComboBox` in `Styles.xaml` is the only editable dropdown template; look at it on screen in both themes before another window uses it. Reviewer: an expression for the counterbore depth that comes to 0 being remembered is not on the checklist; confirm Edit offers no re-apply for a depth-0 seat read back from Inventor (`HoleReapply`, `HoleSeatCheck`).
- Live typing (task 130): tasks 114 and 115, and the Spotface boxes when bound, only need `UpdateSourceTrigger=PropertyChanged` on the Diameter, Custom Upper/Lower and Spotface boxes; the view-model already debounces them. Reviewer: clicking Cancel with the mouse moves focus first, so the typed text is committed (one extra preview, maybe an expression check) before Cancel undoes it; `Focusable="False"` on Cancel or skipping the commit when focus goes to Cancel would stop it. A blank draft keeps OK enabled with no centres picked, and clicking it does nothing (130 checklist step 10).
- Preview with client graphics (task 110): `HolePreviewAppearance` (DisplayName, Is) is kept only for `HoleFitWriter.IsHandColour`; delete it, the check and its two tests if Aidan agrees no part still carries the old preview appearance. A solid that will not union is drawn on its own (darker where it overlaps; logged as "could not be joined"). Reviewer: `ClearPreview` skips the clear when nothing was drawn, so graphics left by a session that died inside a running Inventor stay until the next draw; an unconditional `HolePreviewWriter.Clear` at the start of `Begin` would close it. `HolePreviewFaceReader` swallows an enumeration failure without a log line (a missing seat would be hard to diagnose). Untested: a crash with the window open leaves nothing saved in the file.
- Free centres per point (task 117): only hole features count as using a hole-centre point (Aidan accepted this for now, 2026-11-05). Suppressed hole features still count as using their centres. Unconfirmed in Inventor: `HoleFeature.HoleCenterPoints` returns the sketch's own `SketchPoint`s (if not, every centre is picked); for a part edited in an assembly, whether identity or the name-and-position fallback matches (117 checklist step 6).
- Hole types and spotfaces in Inventor (task 114): if Inventor reads a taper designation back long (`1/4 - 18 NPT`), `HoleReadBack`'s Taper Tapped matcher must accept it (114 checklist step 8). A short spike: do `SetCSink` and `SetDrilled` work in place on a taper tapped hole (today every seat change remakes it)? `AddSpotFaceByToFaceExtent2` has never been run. Reviewer: `HoleDepthReference.FirstOfDifference` reads any `(X) - (Y)` as `X` even when `Y` is not the spotface depth; a plain depth with an expression spotface depth reads back from an Inventor-made hole as `12 mm`; `RefreshSeats` gives every excluded seat the counterbore-on-taper tooltip. The Diameter and spotface boxes have no window-level tests.
- Depths below the seat (task 116): confirm where the thread starts on a countersink and a spotface hole (116 checklist steps 4 and 5); if a spotface hole's thread starts at the start face, `HoleDepthReference.Direction` needs its own thread direction. If Inventor rewrites `(d0) + (6 mm)` when it stores it (step 3), `HoleDepthReference.SplitWritten` should accept its form. If spec 09 question 16 goes the other way, it is a change to `Direction` and a stored-data version bump. The collapsed diagram's summary line could say threaded depths are below the seat. Stored hole data is now version 5.
- Full depth on a Through All tapped hole (task 136): confirm a Through All thread under a counterbore or countersink is measured from the start face (136 checklist step 4) and that Inventor keeps a partial thread made with `CreateTapInfo(..., false, depth)` on a Through All hole (step 1). The add-in reads and writes the undocumented `HoleTapInfo.FullTapDepth` (it and the documented `FullThreadDepth` both exist in the 2026 interop); if a hole made in Inventor's dialog with Full Depth off opens ticked (steps 7 and 8), switch the reader to `FullThreadDepth`. A thread depth longer than the part is left to Inventor; see what it says and whether the window should warn. To a face could take the same checkbox if Aidan wants it. Stored data was not version-bumped for `FullDepth` (an older add-in would read an unticked hole as ticked); bumping to 6 is one line.
- Task 118: the seat boxes move up one row when Fastener type gives way to Fit on a seated clearance size with no seat row (task 137); the window keeps its height. Does it matter?
- Task 118: still scrolling with the diagram open: a counterbored blind tapped hole and a blind dowel with a counterbore (759), a toleranced blind clearance hole (713). Options: Fit and Toleranced on one row, a smaller diagram, or no "Hole" heading.
- Task 118: a screenshot of the built window could replace the AWB Hole mockup image in `docs/ui-mockups/`, which no longer matches.
- Task 118: the 690 height limit assumes a 37.3 title bar (measured at 150 percent on Aidan's PC); a blind clearance hole has 11 to spare. Also check Alt+U inside Inventor (modeless window) and that "Edit AWB Hole" with a long part name trims before the Units toggle.
- Task 138 (and 118): keep a render harness, or a test that builds the AWB Hole window, so window tasks can measure heights instead of reasoning about them.
