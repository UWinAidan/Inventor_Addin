# To do

Everything that needs doing and is not already a task in `docs/PLAN.md`. Kept current by the parent session: items are added when they come up and removed when they are done or turned into a task. Where the project stands is in `STATUS.md`; why things are the way they are is in `DECISIONS.md`.

Last updated: 2026-10-10

## Next up

1. M2 cloud run 2: tasks 049 to 051, then 043 to 048 (running now).
2. Aidan builds run 2 on Windows and works through its checks.
3. Aidan finishes the Part Properties checks listed below; some will be easier once M2's new parts exist.

## Waiting on Aidan: checks in Inventor

**M2 run 1** (full steps in each task's Verification section). Aidan's run 1 checks found three things, now tasks 049 to 051; tick the rest here when confirmed
- [ ] Task 037, Settings: units dropdown and register folder, Browse, validation, access keys, an unknown units value in `settings.json`; is "Units for new files" cut off?
- [ ] Task 038, Part Properties: a Designer or Detailer equal to the Windows login name is replaced by the default designer, and left alone when the default is blank
- [ ] Task 039, Generate part number: online, offline, Undo, lock busy, duplicates, a typed number not in the register, a never-saved part, a new-format file name, and a register folder changed while Inventor runs (11 steps)
- [ ] Task 042: ribbon unchanged with no new log warnings; do the five new icons in `docs/ui-mockups/ribbon-icons.png` read? Optionally, force one command to fail and check the rest of the ribbon still builds

From Part Properties (task 019):

Confirmed so far: the window opens on an assembly, pre-fills Designer, Detailer and part type A, writes Description, Designer and Cost correctly, one Undo reverts an Apply, and the blank Part type entry can be clicked.

- [ ] Clear Detailer and Part type, apply: the custom properties are deleted from the Custom tab, not left empty
- [ ] Edit a part in place inside an assembly, change Designer, apply, return to the assembly, Undo once: does it revert? (open since task 006)
- [ ] A read-only file opens with every field disabled and a line saying why
- [ ] A part with `Part Type` set to `m` in Inventor's own dialog shows `M`
- [ ] A new, never-saved part: Apply still writes Designer and Detailer (the header and part number rows were confirmed on 2026-10-07: New part, Not saved yet, Not assigned)
- [ ] With "Show developer tools" switched off and Inventor restarted: is the Developer panel gone, or does Inventor keep showing it? (open since task 007)


## Waiting on Aidan: decisions

Each spec lists its own open questions in full. These are the ones that block planning.

**Material and finish (spec 03), blocks M3**
- The finish list: names, codes, which materials each applies to
- Hardening: a finish, or separate hardened materials as the library has now
- How a finish is stored on the part, and whether it changes the appearance

**Hole table (spec 04), blocks M4**
- What makes a hole "precision"
- How groups are ordered in the clearance table
- Where the origin comes from

**Drawing tools (spec 05), blocks M5**
- Which of the listed commands are wanted, and what the unclear ones should do

**iLogic (spec 06), blocks M6**
- Which rules, and whether they are internal or external

**Window style (spec 08), not blocking**
- Questions 1 to 8 in spec 08: accent colour, dark title bar, icon style, and five defaults added while planning (never-saved header and part number, theme names, Inventor version row, the XAML rules test)

**Numbering (spec 02), not blocking**
- Questions 13 to 22 in spec 02: a well-formed number not in the register, case in file names, what to do with duplicates, Save As and Replace on every occurrence, opening Part Properties after creating a file, a comma in the name of a file with no number, a register row that cannot be read, Mass Update's Detailer, a registered number on two files, and a file named only `AWB-10001.ipt`. Each has a default the tasks build

**Distribution, not blocking**
- Whether and when to add an installer and a download page. It was to be decided when M2 was planned and is still parked

**Smaller, not blocking**
- Should Settings and About appear with no document open? (spec 01, question 4)
- Should About check for a newer version? (spec 01, question 2)
- More fields for the properties window: revision, vendor, vendor part number, project, notes? (spec 07, question 1)

## Proposed work, not yet agreed

- **Reviewer styling checklist and a Windows build script.** Both are written into spec 08 as proposed additions to M1b. Aidan has not said yes or no.
- **Part name editable in the properties window.** Aidan asked on 2026-10-06 whether the name can be edited. It stays display-only, linked to the file name; the Rename button beside it is now planned (task 047). Waiting on Aidan only if he wants the name to differ from the file name.

## Follow-ups from finished tasks

Small things noticed while building. None is urgent. The parent turns these into tasks when planning a milestone that touches the same code.

**Build and workflow**
- There is no `global.json`, so the cloud and Windows may build with different .NET SDKs. Pin it if builds start to differ. (task 001)
- **Make the deploy line visible in a plain `dotnet build`.** On Aidan's PC the `Deployed add-in to ...` line only appears with `-tl:off`: the terminal logger hides high-importance messages. Make it show in a plain `dotnet build InventorAddin.slnx` (for example a `Directory.Build.rsp` with `-tl:off`, or another way the terminal logger prints), then update step 3 of "Aidan's part" in `docs/WORKFLOW.md` to match. (task 025, Windows check 2026-10-07)
- **Cut the deploy copy retries.** With Inventor open the build succeeds, but the `NOT deployed` case prints about 10 warnings, one per `Copy` retry. Set `Retries` low (0 or 1) on both copies so it is a line or two. Any copy failure is still reported as a lock; the MSBuild warning above it shows the real cause. One attempt with Inventor open crashed with `MSB4166` (child node exited); the rerun was fine. (task 025, Windows check 2026-10-07)

**Ribbon and shell**
- The agent definitions say "the AWB Addin add-in", which repeats "add-in". (task 011)

**Ribbon icons**
- Spec 08's "Ribbon icons" list names only the first five buttons. Tasks 043 to 048 should each add their command's icon there. (task 042)
- When Inventor refuses the icons but has already created the definition, the log says "the button shows text only" and then that the definition was reused; the first line may be untrue. (task 042)
- Task 030: embed only `UI/Icons/*.png`. The `.svg` sources sit in the same folder and must stay out of the build. (task 024)

**Window style**
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
- One Apply writes one log line plus one per property. Keep the summary line and log the per-property lines only on failure. (tasks 018, 019)
- Weight is read through code that also computes volume, area and centre of mass. On a large assembly the window may open slowly; read only the mass, or read it after the window opens. (task 018)
- `PartPropertiesViewModel.Ok()` has no guard for invalid values when called from code. The window cannot reach it today. (task 016)
- Correcting a part type's case shows the "pre-filled values" wording, which is not quite accurate for it. (task 016)
- Number parsing accepts oddly placed group separators, so `1,2,3` reads as 123. Tighten only if it confuses anyone. (task 013)
- The Part Properties window now sizes to its content, so a long status message widens it, up to 560, until the message clears. If that is distracting, fix the width in `UI/Theme/`. (task 029)
- The window's local `ValueOrPlaceholder` and `IdentifierOrPlaceholder` styles could become shared pieces. (task 029)

**Numbering**
- Save As Copy: rename `NewPartException` and `NewPartResult` to neutral names now that copy commands share them; `NewPartCommand.cs` probably has an unused `using System;`. Spec 08 should list the Save As Copy icon and dialog. (task 045)
- Create Print uses the active document, so while editing a part in place inside an assembly it prints the assembly. Its messages say "part" for assemblies too. An existing print is offered before the template is looked up. "metric"/"imperial" wording is in both `NewPartCreator` and `PrintPlan`; share it. Spec 08's icon list should name Create Print. (task 044)
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
