# Status

Where each big item stands. One line per item, kept current by the parent session. For the order of work and the task lists see `docs/PLAN.md`; for what needs doing next see `TODO.md`.

Last updated: 2026-11-22

## Session handoff (ContextKeeper)

- Updated: 2026-11-22 11:18. Branch `claude/m5-drawing-tools`, latest commit `14b9a9a` (task 211, pushed).
- Goal: M5 tasks in PLAN order, in chunks of 4 to 5. For each task: implementer, then reviewer, one commit, push. Stop after each chunk with a summary, the Defaults logged and an Inventor checklist; Aidan says "next chunk". Nothing from work prints goes in the repo.
- Done this session (pushed):
  - Spike 183 (`4e1a24a`) and spike 184 (`b2b09c2`) recorded; the dependent tasks have the confirmed calls.
  - Aidan's decisions and task 210 (`53579d2`).
  - Tasks 211 and 212 written (`7d8ad8e`).
  - Task 211, Finish Spec property, is done (`14b9a9a`; 4972/4972 Core tests in the reviewer's run).
- In the working tree, uncommitted:
  - Task 185 (print template layout):
    - New files `src/InventorAddin.Core/Drawing/PrintTemplateLayout.cs` and `tests/.../Drawing/PrintTemplateLayoutTests.cs`; `Branding.cs` gains `WebsiteUrl` and `PrintLogoFileName`.
    - The first review returned CHANGES REQUIRED; both items are fixed. The small part number now sits at sheet width − 3 mm, and a glyph-band test uses 1.8 mm. `FixedText` is documented as never drawn.
    - Re-review is running (result unverified). The implementer reports 5018/5018 tests.
  - Parent edits that go in task 185's commit:
    - spec 05 open questions 24 to 26 (the triad over the block on portrait sheets, the ANSI margins, the ANSI zone counts)
    - two Default rows in DECISIONS.md
    - task 187 notes (triad left off portrait sheets; draw `Texts`, `Logo` and `Symbol`, not `FixedText`)
  - This STATUS.md handoff.
- Open:
  - Mass in lb is not settled (task 187 tries an imperial template).
  - Spec 05 question 23: mm or µm for Finish Thickness.
  - Numbering tests sometimes fail, a different one each run (`RenamePlanTests` timeout, `SaveAsReplacerTests`); listed in TODO.md.
- Next:
  1. On 185's re-review PASS: set the task to done, tick it in PLAN, copy its follow-ups to TODO.md, commit `task 185: Print template layout (Core)` together with the spec, DECISIONS and 187 edits, then push. On CHANGES REQUIRED: one more fix round, then mark it blocked.
  2. Tasks 186, 189 and 190, one at a time (parallel builds collide on `obj/`).
  3. Chunk 1 summary for Aidan: 211, 185, 186, 189, 190; Defaults; questions 24 to 26; any checklist.
  4. Chunk 2: 187 (drives Inventor with Aidan), 188, 191, 192, 193. 210 and 212 fit in later.
- Gotchas:
  - PowerShell COM spikes:
    - Variable names ignore case, and `$pid` is read-only.
    - Log with `[Console]::WriteLine`.
    - Don't nest double-quoted strings inside `$()`.
    - Walk COM enumerators with `Count` and `Item(i)`.
  - The mass field shows N/A until `MassProperties.Mass` is read and the drawing updated.
  - A view sketch is in model units.
  - Spike files are in `spike-local/18x/` (gitignored) and `C:\AwbSpike\`. The logs hold the material name from Aidan's library; keep it out of the repo.
- Read first: `docs/WORKFLOW.md` (run mode), `docs/PLAN.md` M5, task files 185 to 190, DECISIONS.md rows dated 2026-11-22.

## Big items

| Item | Spec | State | Notes |
|---|---|---|---|
| Foundations (M0) | 01 | **Done, tested in Inventor** | Settings, log, ribbon, error reporting, Settings and About windows, editing-command base, "AWB Addin" branding |
| Part properties window (M1) | 07 | **Built and merged, partly tested in Inventor** | Opens on parts and assemblies, pre-fills, writes and undoes correctly. A never-saved part shows New part, Not saved yet and Not assigned; the blank Part type entry can be clicked. Some checklist steps not yet reported; see `TODO.md` |
| Window style and icons (M1b) | 08 | **Done, tested in Inventor** | Tasks 020 to 031, then 151 (new window icons, Outline chosen state, ribbon option C, 7j AWB Hole; checked by Aidan 2026-11-12, merged in PR #31). Shared look in light and dark, all three windows restyled, icons on ribbon and headers, XAML rules test. Every check passed on Windows. Two follow-ups on the build's deploy line are in `TODO.md` |
| Part creation and numbering (M2) | 02 | **Done, tested in Inventor** | Every check passed; the last run 1 checks were reported on 2026-10-17. Run 2 checked on Windows 2026-10-11; fix run 053 to 061 and fix run 2 (spike 062, tasks 063 to 066) checked 2026-10-12 and passed. Merged into main from `claude/m2-fix-run` |
| Material and finish (M3) | 03 | **Done, tested in Inventor** | Tasks 067 to 080, merged into main from `claude/m3-material-finish` on 2026-10-17: starter library and finish list, Material and Finish window and button, Part Properties Change button and warning, Settings and About on every ribbon, windows that fit the screen, Settings as one resizable page of collapsible sections, dialogs with only a close button, and a themed scroll bar. Every check passed |
| AWB Hole (M4 part 1) | 09 | **Done, tested in Inventor** | Decided by Aidan 2026-10-17; spike 081 done with Aidan. Tasks 082 to 090 checked by Aidan 2026-10-18 (making, picking, previewing, tapped and dowel holes, OK, Cancel and Undo work). Then built on branch `claude/m4-hole-generator`, compiled on Windows, not yet run in Inventor: fixes and requests 093 to 101, mockup 102 (approved with changes 2026-10-23), dowel seats 106, window redesign 104, takeover of Hole and H 091, Edit Feature and double-click 092. Aidan's first look on 2026-10-24 gave six changes, planned as tasks 103 (in place for every command, top priority), 105 and 107 to 119: client graphics preview, Simple and Taper Tapped types, Spotface, toleranced clearance holes, depths below the seat, centres per point, a window that fits at 150 percent. Aidan checked 091 to 106 on 2026-10-25: six failures and seven requests, planned as tasks 120 to 132 in the same round. That round (105 to 138) was built on Windows (2026-11-07) and checked by Aidan from 2026-11-07 to 2026-11-09, with a temporary self-test (spike 139, local only) for the measurable steps: everything passed except hand colours on a remade hole (122-6, 122-8), the sketch point mini toolbar (133-1, 133-2, 133-5) and the highlight in place (105-1). Those fixes, the marking menu's Hole, and Aidan's requests and his first-look answers became tasks 140 to 150, built on Windows on 2026-11-09 (not yet run in Inventor; checklist `docs/checklists/m4-fixes-140-150.md`). Aidan checked them on 2026-11-10: all passed but 140-2, fixed with two decisions as tasks 152 to 154, which passed the same day. PR #29 merged into main on 2026-11-10 |
| Hole table wizard (M4 part 2) | 04 | **Done, tested in Inventor** | Spike 160 done with Aidan 2026-11-17: tables drawn as sketched symbols, one `AWB Centre Mark` style on every hole. Core tasks 155 to 159, 161 and 162 and add-in tasks 163 to 167 built and reviewed on branch `claude/m4-hole-table` (add-in compiled on Windows 2026-11-17). Aidan's check: `docs/checklists/m4-hole-table-163-167.md` (167 passed 2026-11-18). Fixes and requests 168 to 173, built 2026-11-18; 163 to 173 passed Aidan's check 2026-11-20. His fix and request from it, 174 to 176 (selected point picks free centres; automatic out-of-date check with Update now or Later), built and reviewed on Windows 2026-11-20; Aidan's check of them passed but for four failures, fixed as 177 to 180; 177 to 179 passed Aidan's check 2026-11-20, 180 reversed by him and reverted as 181, which passed the same day. PR #32 merged into main on 2026-11-20 |
| Drawing tools (M5) | 05 | Planned | Tasks 183 to 209 (2026-11-21): print template first, then views, Open Drawing, Check Out of Bounds, and six tools (notes, part marking, Batch Print, REF dimensions, Stock Size, Make/Buy). Spikes 183 and 184 need Aidan in Inventor |
| iLogic injection (M6) | 06 | Not started | Waiting on which rules |
| Distribution (installer, download page) | none yet | Parked | Was to be decided when M2 was planned; still waiting on Aidan |

States used: Not started, Planned, In progress, Built (merged, not tested in Inventor), Partly tested, Done (tested in Inventor), Parked, Proposed.

## What works in Inventor today

- An **AWB Addin** tab on the part, assembly and drawing ribbons, and with no document open; Settings and About are on it everywhere.
- **Part Properties** (parts and assemblies): shows part number, part name, part type, designer, detailer, cost, material, finish and weight. Part type, designer, detailer and cost can be edited. One Undo reverts an Apply.
- **One shared window look** in Inventor's light and dark themes, with keyboard navigation, and icons on the ribbon buttons and window headers.
- **Settings**: show developer tools, default designer. One scrolling page of collapsible sections, resizable by its edges and remembered (tasks 079, 080).
- **About**: version, build time, Inventor version, log folder.
- **Developer panel** (off by default): export the active document or the asset libraries to JSON.
- **Build deploy line**: a Windows build copies the add-in into Inventor and says whether it did. The `Deployed` line shows only with `-tl:off`; see `TODO.md`.

## Numbers

- Tasks finished: 90 (001 to 090), plus 091 to 102, 104 and 106 on branch `claude/m4-hole-generator`
- Core tests: 2650 after task 080; they run in the cloud and on Windows with `dotnet test tests/InventorAddin.Core.Tests`
