# To do

Everything that needs doing and is not already a task in `docs/PLAN.md`. Kept current by the parent session: items are added when they come up and removed when they are done or turned into a task. Where the project stands is in `STATUS.md`; why things are the way they are is in `DECISIONS.md`.

Last updated: 2026-10-10

## Next up

1. Run M2 cloud run 1 (tasks 033 to 042): "Run milestone M2 following docs/WORKFLOW.md." It skips 032 and stops before 043, which wait on the spike.
2. Aidan builds run 1 on Windows, works through its checks, and runs spike 032 (`docs/tasks/032-spike-create-rename-print.md`) in a local session. Commit its Results.
3. Run M2 cloud run 2 (tasks 043 to 048).
4. Aidan finishes the Part Properties checks listed below; some will be easier once M2's new parts exist.

## Waiting on Aidan: checks in Inventor

From the Part Properties checklist (task 019). Confirmed so far: the window opens on an assembly, pre-fills Designer, Detailer and part type A, writes Description, Designer and Cost correctly, one Undo reverts an Apply, and the blank Part type entry can be clicked.

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
- Questions 13 to 19 in spec 02: a well-formed number not in the register, case in file names, what to do with duplicates, Save As and Replace on every occurrence, opening Part Properties after creating a file, a comma in the name of a file with no number, and a register row that cannot be read. Each has a default the tasks build

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
- Task 030: embed only `UI/Icons/*.png`. The `.svg` sources sit in the same folder and must stay out of the build. (task 024)

**Window style**
- The remark in `UiTheme.cs` still says the dark-theme rule waits for spike 020; name the confirmed `LightTheme` and `DarkTheme`. (task 026)
- Settings: the gap between a control and its note is the full row gap (9). If it looks loose, add a shared `HelpText` style. (task 028)
- Settings: the restart note lines up with the checkbox's box, not its label text. (task 028)
- A cloud check project that compiles the add-in's XAML and WPF code against stub Inventor types would catch XAML errors before Aidan builds. Proposed, not agreed. (task 026)

- Developer commands get no icon while developer tools are off. (task 030)

- XAML rules test: C# in `UI/Controls/` is not checked, `DynamicResource` keys are not checked to exist, and a size binding with a typed `FallbackValue` passes. (task 031)

**Settings window**
- "Units for new files" may be cut off by the 100-wide label column. Widen the shared `FieldRow` label column or shorten the label; check in Inventor. (task 037)
- A shared style for a button beside a text box (for example `BrowseButton`), so Browse can sit on the register folder row. New Part's folder field will want it. (task 037)
- The Browse handler calls `Directory.Exists` on the UI thread, which can stall on a disconnected NAS drive. Drop the check or move it off the UI thread if it shows. (task 037)

**Part properties**
- One Apply writes one log line plus one per property. Keep the summary line and log the per-property lines only on failure. (tasks 018, 019)
- Weight is read through code that also computes volume, area and centre of mass. On a large assembly the window may open slowly; read only the mass, or read it after the window opens. (task 018)
- `PartPropertiesViewModel.Ok()` has no guard for invalid values when called from code. The window cannot reach it today. (task 016)
- Correcting a part type's case shows the "pre-filled values" wording, which is not quite accurate for it. (task 016)
- Number parsing accepts oddly placed group separators, so `1,2,3` reads as 123. Tighten only if it confuses anyone. (task 013)
- The Part Properties window now sizes to its content, so a long status message widens it, up to 560, until the message clears. If that is distracting, fix the width in `UI/Theme/`. (task 029)
- The window's local `ValueOrPlaceholder` and `IdentifierOrPlaceholder` styles could become shared pieces. (task 029)

**Numbering**
- Until task 039 lands, Part Properties shows Inventor's file-name fallback as an assigned number for files whose name has an underscore or a comma, because `PartName.FromFileName` now converts them. Do not build a commit between 034 and 039 for testing. Close this when 039 is committed. (task 034)

**Developer tools**
- JSON exports include `BoundingBox` lengths, which are derived values. Mark them ignored if exports should not carry them. (task 001)
