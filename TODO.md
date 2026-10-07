# To do

Everything that needs doing and is not already a task in `docs/PLAN.md`. Kept current by the parent session: items are added when they come up and removed when they are done or turned into a task. Where the project stands is in `STATUS.md`; why things are the way they are is in `DECISIONS.md`.

Last updated: 2026-10-06

## Next up

1. Plan and run M1b, window style and icons (spec 08). It starts with a spike Aidan runs on Windows.
2. Aidan finishes the Part Properties checks listed below.
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
- The accent colour (default blue), whether to darken the title bar, and the icon style

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
- The build skips copying the add-in into Inventor's folder when Inventor is running, and says so only in a warning that is easy to miss. Make it print one plain line: deployed, or not deployed because Inventor is open. (Aidan hit this on 2026-10-06.)
- There is no `global.json`, so the cloud and Windows may build with different .NET SDKs. Pin it if builds start to differ. (task 001)

**Ribbon and shell**
- `RibbonSetup.Create` does not catch a failure for one ribbon, so one failure stops the rest and is not logged. Wrap each ribbon and log the error. (tasks 005, 007)
- In the Settings window an error message stays visible after a failed save until the next save. (task 008)
- The agent definitions say "the AWB Addin add-in", which repeats "add-in". (task 011)

**Part properties**
- The window has a large empty gap between Weight and the buttons, where the status line sits. (seen in Aidan's test)
- One Apply writes one log line plus one per property. Keep the summary line and log the per-property lines only on failure. (tasks 018, 019)
- Weight is read through code that also computes volume, area and centre of mass. On a large assembly the window may open slowly; read only the mass, or read it after the window opens. (task 018)
- `PropertyReader.Set` has no callers now that `PropertyWriter` exists. Delete it. (task 018)
- `PartPropertiesViewModel.Ok()` has no guard for invalid values when called from code. The window cannot reach it today. (task 016)
- Correcting a part type's case shows the "pre-filled values" wording, which is not quite accurate for it. (task 016)
- Number parsing accepts oddly placed group separators, so `1,2,3` reads as 123. Tighten only if it confuses anyone. (task 013)

**Developer tools**
- JSON exports include `BoundingBox` lengths, which are derived values. Mark them ignored if exports should not carry them. (task 001)
