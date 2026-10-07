# To do

Everything that needs doing and is not already a task in `docs/PLAN.md`. Kept current by the parent session: items are added when they come up and removed when they are done or turned into a task. Where the project stands is in `STATUS.md`; why things are the way they are is in `DECISIONS.md`.

Last updated: 2026-10-07

## Next up

1. Aidan finishes the remaining window style checks and the Part Properties checks listed below.
3. Aidan decides the numbering configuration (spec 02), then: plan M2.

## Waiting on Aidan: checks in Inventor

From the Part Properties checklist (task 019). Confirmed so far: the window opens on an assembly, pre-fills Designer, Detailer and part type A, writes Description, Designer and Cost correctly, and one Undo reverts an Apply.

- [ ] Clear Detailer and Part type, apply: the custom properties are deleted from the Custom tab, not left empty
- [ ] Edit a part in place inside an assembly, change Designer, apply, return to the assembly, Undo once: does it revert? (open since task 006)
- [ ] A read-only file opens with every field disabled and a line saying why
- [ ] A part with `Part Type` set to `m` in Inventor's own dialog shows `M`
- [ ] A new, never-saved part: header says "Not saved yet", part name blank, Apply still writes Designer and Detailer
- [ ] Part type dropdown: the blank entry at the top can be clicked
- [ ] With "Show developer tools" switched off and Inventor restarted: is the Developer panel gone, or does Inventor keep showing it? (open since task 007)

From the build deploy line (task 025):

- [ ] With Inventor closed, `dotnet build InventorAddin.slnx`: the last lines include `Deployed add-in to ...`, and About shows this build's time
- [ ] With Inventor open, build again: the build succeeds, prints the `NOT deployed` warning and no `Deployed` line
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: neither line is printed

From the window style and icons (tasks 026 to 031; full steps in each task file and in PR #14). Confirmed on 2026-10-07: the add-in builds and deploys; About, Settings and Part Properties look right in light and dark with the dark title bar; Not assigned and Not set show correctly; ribbon icons are right in both themes after a restart. A clipping bug at 150% display scale was found and fixed on `main`.

- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes on Windows (the XAML rules test from 031 finds files by path)
- [ ] Keyboard: Tab order, access keys, Enter and Esc, and the accent focus outline in each window (026, 028, 029)
- [ ] Part Properties: the blank Part type entry can be clicked (029)
- [ ] A new, never-saved part: record what the Part number row shows (spec 08, question 5) (029)
- [ ] The log has one `INFO` line naming the ribbon icon set, and no icon warnings (030)
- [ ] Section headings: does the hair-space letter spacing read as wide spacing? (026)


## Waiting on Aidan: decisions

Each spec lists its own open questions in full. These are the ones that block planning.

**Numbering (spec 02), blocks M2**
- What a part number looks like, and whether assemblies and parts are numbered differently
- Whether the register design is accepted: one register file on the NAS with a local copy on each PC
- How a file name combines the part number and the name
- Where templates live
- What the Rename command searches when it updates references

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

**Smaller, not blocking**
- Should Settings and About appear with no document open? (spec 01, question 4)
- Should About check for a newer version? (spec 01, question 2)
- What should "Mass Update" do, if it is wanted at all? (spec 01, question 1)
- More fields for the properties window: revision, vendor, vendor part number, project, notes? (spec 07, question 1)

## Proposed work, not yet agreed

- **Reviewer styling checklist and a Windows build script.** Both are written into spec 08 as proposed additions to M1b. Aidan has not said yes or no.
- **Part name editable in the properties window.** Aidan asked on 2026-10-06 whether the name can be edited. Today it is display-only because it is linked to the file name, and changing it means renaming the file (the Rename command, planned with M2). Waiting on Aidan: keep it that way, or make the name editable now and let it differ from the file name.

## Follow-ups from finished tasks

Small things noticed while building. None is urgent. The parent turns these into tasks when planning a milestone that touches the same code.

**Build and workflow**
- There is no `global.json`, so the cloud and Windows may build with different .NET SDKs. Pin it if builds start to differ. (task 001)
- With Inventor open, the deploy `Copy` may retry for about 10 s before the `NOT deployed` line appears. If that is annoying, set `Retries` low on both copies. Any copy failure is reported as a lock; the MSBuild warning above it shows the real cause. (task 025)

**Ribbon and shell**
- The agent definitions say "the AWB Addin add-in", which repeats "add-in". (task 011)

**Ribbon icons**
- Task 030: embed only `UI/Icons/*.png`. The `.svg` sources sit in the same folder and must stay out of the build. (task 024)

**Window style**
- The remark in `UiTheme.cs` still says the dark-theme rule waits for spike 020; name the confirmed `LightTheme` and `DarkTheme`. (task 026)
- Section headings use a hair space (U+200A) between letters. If it does not read as wide spacing in Inventor, try a thin space (U+2009). (task 026)
- Settings: the gap between a control and its note is the full row gap (9). If it looks loose, add a shared `HelpText` style. (task 028)
- Settings: the restart note lines up with the checkbox's box, not its label text. (task 028)
- Settings: "Show developer tools" has no access key; D is taken by Default designer. Alt+S is free. (task 028)
- A cloud check project that compiles the add-in's XAML and WPF code against stub Inventor types would catch XAML errors before Aidan builds. Proposed, not agreed. (task 026)

- Ribbon: if `AddButtonDefinition` with icons throws after Inventor created the definition, the text-only retry fails on the duplicate name. Re-check `defs[InternalName]` before retrying. (task 030 review)
- Ribbon: one command failing to register still stops the whole ribbon build; only per-ribbon failures are now caught. (task 030)
- Developer commands get no icon while developer tools are off. (task 030)

- XAML rules test: make `UseLayoutRounding` in window XAML or `UI/Theme/` fail the test, so the 150% scale clipping cannot come back. (Aidan's fix, 2026-10-07)
- XAML rules test: C# in `UI/Controls/` is not checked, `DynamicResource` keys are not checked to exist, and a size binding with a typed `FallbackValue` passes. (task 031)

**Part properties**
- One Apply writes one log line plus one per property. Keep the summary line and log the per-property lines only on failure. (tasks 018, 019)
- Weight is read through code that also computes volume, area and centre of mass. On a large assembly the window may open slowly; read only the mass, or read it after the window opens. (task 018)
- `PropertyReader.Set` has no callers now that `PropertyWriter` exists. Delete it. (task 018)
- `PartPropertiesViewModel.Ok()` has no guard for invalid values when called from code. The window cannot reach it today. (task 016)
- Correcting a part type's case shows the "pre-filled values" wording, which is not quite accurate for it. (task 016)
- Number parsing accepts oddly placed group separators, so `1,2,3` reads as 123. Tighten only if it confuses anyone. (task 013)
- `PartNumber`, `Material` and `Finish` on `PartPropertiesViewModel` are no longer bound by the window. Remove them, or keep them for a later field. (task 029)
- The Part Properties window now sizes to its content, so a long status message widens it, up to 560, until the message clears. If that is distracting, fix the width in `UI/Theme/`. (task 029)
- The window's local `ValueOrPlaceholder` and `IdentifierOrPlaceholder` styles could become shared pieces. (task 029)
- If the blank Part type entry is too short to click, give the shared `ComboBoxItem` style a minimum height. (task 029)

**Developer tools**
- JSON exports include `BoundingBox` lengths, which are derived values. Mark them ignored if exports should not carry them. (task 001)
