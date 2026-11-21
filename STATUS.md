# Status

Where each big item stands. One line per item, kept current by the parent session. For the order of work and the task lists see `docs/PLAN.md`; for what needs doing next see `TODO.md`.

Last updated: 2026-11-20

## Session handoff (ContextKeeper)

- Updated: 2026-11-20. Branch `claude/m4-hole-table`, pushed; PR #32 to `main` describes 155 to 181.
- Goal: Aidan's check of 177 to 180 passed except 180, which he reversed; 181 reverts it.
- Done (each reviewed PASS; add-in compiled on Windows with 0 errors; Core tests pass apart from flaky Numbering ones; none run in Inventor):
  - 177 Later remembered by a fingerprint of the hole data (`HoleTableStaleness`, `HoleTableAutoCheck`).
  - 178 Update keeps a stacked precision table on the clearance table (`HoleTableAlign.PrecisionAfterRedraw`, `HoleTableDrawer`, `HoleTableSource.UpdateCore`).
  - 179 tapped drill: AWB stored ?? Inventor ?? workbook Tapped row, warning when none (cause of the lost line not proven).
  - 180 description extra decimals: reverted by 181 (descriptions use exactly the table's decimals; Ø2.25 prints Ø2.3 in mm clearance; Swap to Precision for x.xx).
- Next:
  1. Aidan's one-step check of 181 (D1 `Ø2.3 THRU`; a hole swapped to Precision shows 2 decimals). Then merge PR #32.
  2. Failures become fix tasks (next number 182). 179's reviewer follow-ups are in `TODO.md` (the M6 to M8 stale drill is fix soon). A `tapped but its tap drill could not be read` log line means 179's three sources all failed for that hole.
  3. Open for Aidan: spec 09 question 29, spec 04 questions 20 to 25, the Hole Table icon.
- Gotchas: build with `-p:DeployToInventor=false` while Inventor is open; one build at a time. Flaky Core tests: NewPartCreator, RenamePlan, Numbering, SaveAsReplacer, SaveAsCopyCreator, RegisterStore. Long Bash heredocs failed here; use Write or python io.
- Read first: `docs/checklists/m4-fixes-177-180.md`, `TODO.md` follow-ups for 178 and 179.

## Big items

| Item | Spec | State | Notes |
|---|---|---|---|
| Foundations (M0) | 01 | **Done, tested in Inventor** | Settings, log, ribbon, error reporting, Settings and About windows, editing-command base, "AWB Addin" branding |
| Part properties window (M1) | 07 | **Built and merged, partly tested in Inventor** | Opens on parts and assemblies, pre-fills, writes and undoes correctly. A never-saved part shows New part, Not saved yet and Not assigned; the blank Part type entry can be clicked. Some checklist steps not yet reported; see `TODO.md` |
| Window style and icons (M1b) | 08 | **Done, tested in Inventor** | Tasks 020 to 031, then 151 (new window icons, Outline chosen state, ribbon option C, 7j AWB Hole; checked by Aidan 2026-11-12, merged in PR #31). Shared look in light and dark, all three windows restyled, icons on ribbon and headers, XAML rules test. Every check passed on Windows. Two follow-ups on the build's deploy line are in `TODO.md` |
| Part creation and numbering (M2) | 02 | **Done, tested in Inventor** | Every check passed; the last run 1 checks were reported on 2026-10-17. Run 2 checked on Windows 2026-10-11; fix run 053 to 061 and fix run 2 (spike 062, tasks 063 to 066) checked 2026-10-12 and passed. Merged into main from `claude/m2-fix-run` |
| Material and finish (M3) | 03 | **Done, tested in Inventor** | Tasks 067 to 080, merged into main from `claude/m3-material-finish` on 2026-10-17: starter library and finish list, Material and Finish window and button, Part Properties Change button and warning, Settings and About on every ribbon, windows that fit the screen, Settings as one resizable page of collapsible sections, dialogs with only a close button, and a themed scroll bar. Every check passed |
| AWB Hole (M4 part 1) | 09 | **Done, tested in Inventor** | Decided by Aidan 2026-10-17; spike 081 done with Aidan. Tasks 082 to 090 checked by Aidan 2026-10-18 (making, picking, previewing, tapped and dowel holes, OK, Cancel and Undo work). Then built on branch `claude/m4-hole-generator`, compiled on Windows, not yet run in Inventor: fixes and requests 093 to 101, mockup 102 (approved with changes 2026-10-23), dowel seats 106, window redesign 104, takeover of Hole and H 091, Edit Feature and double-click 092. Aidan's first look on 2026-10-24 gave six changes, planned as tasks 103 (in place for every command, top priority), 105 and 107 to 119: client graphics preview, Simple and Taper Tapped types, Spotface, toleranced clearance holes, depths below the seat, centres per point, a window that fits at 150 percent. Aidan checked 091 to 106 on 2026-10-25: six failures and seven requests, planned as tasks 120 to 132 in the same round. That round (105 to 138) was built on Windows (2026-11-07) and checked by Aidan from 2026-11-07 to 2026-11-09, with a temporary self-test (spike 139, local only) for the measurable steps: everything passed except hand colours on a remade hole (122-6, 122-8), the sketch point mini toolbar (133-1, 133-2, 133-5) and the highlight in place (105-1). Those fixes, the marking menu's Hole, and Aidan's requests and his first-look answers became tasks 140 to 150, built on Windows on 2026-11-09 (not yet run in Inventor; checklist `docs/checklists/m4-fixes-140-150.md`). Aidan checked them on 2026-11-10: all passed but 140-2, fixed with two decisions as tasks 152 to 154, which passed the same day. PR #29 merged into main on 2026-11-10 |
| Hole table wizard (M4 part 2) | 04 | **Built**, not yet run in Inventor | Spike 160 done with Aidan 2026-11-17: tables drawn as sketched symbols, one `AWB Centre Mark` style on every hole. Core tasks 155 to 159, 161 and 162 and add-in tasks 163 to 167 built and reviewed on branch `claude/m4-hole-table` (add-in compiled on Windows 2026-11-17). Aidan's check: `docs/checklists/m4-hole-table-163-167.md` (167 passed 2026-11-18). Fixes and requests 168 to 173, built 2026-11-18; 163 to 173 passed Aidan's check 2026-11-20. His fix and request from it, 174 to 176 (selected point picks free centres; automatic out-of-date check with Update now or Later), built and reviewed on Windows 2026-11-20; Aidan's check of them passed but for four failures, fixed as 177 to 180; 177 to 179 passed Aidan's check 2026-11-20, 180 reversed by him and reverted as 181 |
| Drawing tools (M5) | 05 | Not started | Waiting on which commands Aidan wants. Will include a custom drawing template |
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
