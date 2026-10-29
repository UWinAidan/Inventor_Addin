# To do

Everything that needs doing and is not already a task in `docs/PLAN.md`. Kept current by the parent session: items are added when they come up and removed when they are done or turned into a task. Where the project stands is in `STATUS.md`; why things are the way they are is in `DECISIONS.md`.

Last updated: 2026-10-28

## Next up

1. M4 part 1: the round is planned: tasks 103 to 134 (2026-10-25). Spike 103 is done (rounds 1 and 2 with Aidan, 2026-10-25); it gave tasks 105 and 109 in full and two new tasks, 133 (the mini toolbar's Hole button) and 134 (Save As and Replace renames the occurrence; Aidan's Rename report). 105, 109, 133 and 134 are done (not yet run in Inventor). Spikes 107 and 108 are done (with Aidan, 2026-10-28); task 110 is confirmed from 107's final result (round 4: drill point, seats off the edge and end caps all pass), tasks 114, 115 and 127 are completed from 108. 135, 120 and 121 are done. **Next:** 128, 129 (ribbon fixes); the full order is in `docs/PLAN.md` ("Round after PR #29's first look").
2. Ribbon icon colours: Aidan picks option A, B or C from `docs/ui-mockups/ribbon-icons-colour-options.png` on branch `claude/ribbon-icon-colour`; then build it. **Later, not this round** (Aidan, 2026-10-25): every AWB icon gets a more polished look, done together with the colour option.
3. Aidan finishes the remaining Part Properties checks below.

## Waiting on Aidan: checks in Inventor

**M2 run 2** was checked on 2026-10-11. Six bugs and six changes became tasks 052 to 061; every other run 2 check passed.

**M2 fix run** (053 to 061) was checked on 2026-10-12: everything passed except occurrence names in a closed assembly, now task 065. Aidan also asked for four items, now fix run 2 (062 to 066).

**M2 fix run 2** (062 to 066) was checked on 2026-10-12: every check passed.

**M3** (068 to 076) and its fixes (077 to 080) were checked on 2026-10-17: every check passed. Merged into main from `claude/m3-material-finish` on 2026-10-17.

**M2 run 1** (037, 038, 039, 042): Aidan reported on 2026-10-17 that the remaining checks passed.

**M4 part 1, 090** was checked on 2026-10-18: steps 1 to 4a, 6, 7, 10, 12 to 21 and 25 passed (step 5 skipped). Steps 8 and 11 and the tapped thread display failed, now tasks 093 to 095; step 4b and Aidan's other requests are tasks 096 to 105. Steps 22 to 24 (editing) wait for 092.

**M4 part 1, 091 to 106** (not 103, 105) were checked on 2026-10-25. Failed, now fix tasks: 091-3 (120), 091-4 (121), 096-3 (122), 099-3 (123), 100-7 (124), 106-5 (125). Every other step passed or was accepted as it is (092-4, 098-2, 099-5), except:
- [ ] Not tested yet: 106 steps 6, 7 and 10 (fold into task 125's check)
- Skipped, not needed: 091-6, 095-4, 099-2 (covered by 106), 106-8, 106-9
- 095-5 answered: Inventor's own Hole dialog, editing an AWB tapped hole with no changes, keeps the bore at the tap drill, not the minor diameter.

From Part Properties (task 019):

Confirmed so far: the window opens on an assembly, pre-fills Designer, Detailer and part type A, writes Description, Designer and Cost correctly, one Undo reverts an Apply, and the blank Part type entry can be clicked.

- [ ] Clear Detailer and Part type, apply: the custom properties are deleted from the Custom tab, not left empty
- [ ] Edit a part in place inside an assembly, change Designer, apply, return to the assembly, Undo once: does it revert? (open since task 006; now on task 109's checklist, step 5)
- [ ] A read-only file opens with every field disabled and a line saying why
- [ ] A part with `Part Type` set to `m` in Inventor's own dialog shows `M`
- [ ] A new, never-saved part: Apply still writes Designer and Detailer (the header and part number rows were confirmed on 2026-10-07: New part, Not saved yet, Not assigned)
- [ ] With "Show developer tools" switched off and Inventor restarted: is the Developer panel gone, or does Inventor keep showing it? (open since task 007)


## Waiting on Aidan: decisions

Each spec lists its own open questions in full. These are the ones that block planning.

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
- The Defaults dated 2026-10-24 in `DECISIONS.md`: the Spotface and Taper Tapped tab layouts, invented NPT and BSPT starter rows, Simple's Diameter box, the tolerance list for clearance holes

**Hole table (spec 04), blocks the wizard half of M4**
- How groups are ordered in the clearance table
- Where the origin comes from

**Drawing tools (spec 05), blocks M5**
- Which of the listed commands are wanted, and what the unclear ones should do
- The custom print templates: sheet sizes, sheet colours, and the title block, designed with Aidan (noted 2026-10-11)

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

- **Reviewer styling checklist and a Windows build script.** Both are written into spec 08 as proposed additions to M1b. Aidan has not said yes or no.
- **Part name editable in the properties window.** Aidan asked on 2026-10-06 whether the name can be edited. It stays display-only, linked to the file name; the Rename button beside it is now planned (task 047). Waiting on Aidan only if he wants the name to differ from the file name.

## Follow-ups from finished tasks

Small things noticed while building. None is urgent. The parent turns these into tasks when planning a milestone that touches the same code.

- AWB Hole in place (task 105): check the switch rule with a new, unsaved assembly or part (names are empty; only the object comparison is left). In place, picked sketch points and edited holes are not checked to belong to the part being edited; if Inventor ever offers another component's, guard on the document (reviewer: the double-click would open the window on a hole of another part).
- Commands in place (task 109): Part Properties' property writes and Generate start their transaction on the edited part, not `ActiveDocument` (only Change beside Material does); if 109 checklist step 5 fails (one Undo in the assembly should revert Designer), start them on `ActiveDocument` as Material and Finish does (spike 067). `MaterialFinishCommand`, `AwbHoleCommand` and `HoleEditRedirect` still read their documents directly instead of through `AddinCommand.CommandTargetDocument()`; move them when next touched.
- Save As and Replace occurrence name (task 134): if the name set fails and the sub-assembly save then fails too, the thrown save message hides the name note (only the log has it); append it. `NewPartCreatorTests` has no direct test of `Notice` with `Note`. `OccurrenceNameKeptMessage` could name the copy's file to rename to. Untested in Inventor: `proxy.NativeObject` after `Replace` accepting a `Name` set, one Undo reverting both, a throwing `Name` set leaving the transaction able to end.
- Rename of AWB-format occurrence names (task 135): `PartNumber.TryParse` trims white space, so `NAME, AWB-10017:1` also counts as an AWB name and is renamed (as file names are read); a stricter match is one check in `RenamePlan.AwbNameDigits` if Aidan wants it. Renaming a file with no number renames a stale AWB-named occurrence of it to the plain new name (`WIDGET_2:1`). 
- Flaky tests (tasks 135 and 120): `NewPartCreatorTests.FailureAfterAssigning_ACloseThatFailsIsLogged_AndTheOriginalMessageIsKept` (the message named an `AppData\Local` folder) and `SaveAsReplacerTests.Replace_CopyFails_IsSaveAsCopysFailure_AndNothingIsReplaced` each failed once and passed on re-run; probably the shared real temp folder.
- Mini toolbar Hole (task 133): task 129 must call `HoleEditRedirect.Active?.InventorHoleAsked()` just before running Inventor's Hole (and add its command to `InventorHoleRunRequest.StartIsAskedFor` if it is not `PartHoleCmd`/`PartDMHoleCmd`). `LogSheetMetalToolbar` loops over `selected` with no null guard; a null would skip the swap once (caught, logged). `CurrentPart()` does not tell a flat pattern apart, so its toolbar may be swapped too (133 checklist 5a). If the inserted entry shows Inventor's icon or runs Inventor's Hole, always insert our `ButtonDefinition`. `HoleEditRules` remarks still say editing does not work in place (stale since task 105).

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
