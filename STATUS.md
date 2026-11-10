# Status

Where each big item stands. One line per item, kept current by the parent session. For the order of work and the task lists see `docs/PLAN.md`; for what needs doing next see `TODO.md`.

Last updated: 2026-11-10

## Session handoff (ContextKeeper)

- Updated: 2026-11-10. Branch `claude/m4-hole-generator`, all pushed (latest 90ae133). PR #29 open against `main`; its description has a "Fixes 152 to 154" section with the checklist.
- Goal: close M4 part 1. Aidan runs `docs/checklists/m4-fixes-152-154.md`; when it passes, PR #29 is merged.
- Done: Aidan's 140-150 results logged (all passed except 140-2; decisions on Flat Pattern and free centres in DECISIONS.md; 7j SVGs stay as they are). Tasks 152 (5deae2b), 153 (2a63131), 154 (72fdb30): implementer + reviewer, PASS each. Follow-ups in TODO.md.
- State: build 0 errors, deployed; Core tests 4569 pass. Nothing of 152 to 154 run in Inventor.
- Next steps:
  1. Aidan's results for the 152-154 checklist; failures become fix tasks (next number 155).
  2. After Aidan merges PR #29: delete local spike branches (103, 107, 108, 139; check `git branch`); ask before deleting remote branches.
  3. Task 151 (icons) on a new branch off `main`, own PR.
- Gotchas: check `tasklist //FI "IMAGENAME eq Inventor.exe"` before building; `-p:DeployToInventor=false` while it runs. Bash heredocs with apostrophes inside a long multi-file command failed ("unexpected EOF"); use the Write tool for task files. Edit tracking files with Python `newline=''` and an assert per replace. `$TEMP/done.py NNN x` marks a task done in its file and PLAN.md. Numbering timing tests can fail under parallel load.
- Read first: `docs/checklists/m4-fixes-152-154.md`, `TODO.md` "Next up", `docs/tasks/151-icon-redesign-build.md`.

## Big items

| Item | Spec | State | Notes |
|---|---|---|---|
| Foundations (M0) | 01 | **Done, tested in Inventor** | Settings, log, ribbon, error reporting, Settings and About windows, editing-command base, "AWB Addin" branding |
| Part properties window (M1) | 07 | **Built and merged, partly tested in Inventor** | Opens on parts and assemblies, pre-fills, writes and undoes correctly. A never-saved part shows New part, Not saved yet and Not assigned; the blank Part type entry can be clicked. Some checklist steps not yet reported; see `TODO.md` |
| Window style and icons (M1b) | 08 | **Done, tested in Inventor** | Tasks 020 to 031. Shared look in light and dark, all three windows restyled, icons on ribbon and headers, XAML rules test. Every check passed on Windows. Two follow-ups on the build's deploy line are in `TODO.md` |
| Part creation and numbering (M2) | 02 | **Done, tested in Inventor** | Every check passed; the last run 1 checks were reported on 2026-10-17. Run 2 checked on Windows 2026-10-11; fix run 053 to 061 and fix run 2 (spike 062, tasks 063 to 066) checked 2026-10-12 and passed. Merged into main from `claude/m2-fix-run` |
| Material and finish (M3) | 03 | **Done, tested in Inventor** | Tasks 067 to 080, merged into main from `claude/m3-material-finish` on 2026-10-17: starter library and finish list, Material and Finish window and button, Part Properties Change button and warning, Settings and About on every ribbon, windows that fit the screen, Settings as one resizable page of collapsible sections, dialogs with only a close button, and a themed scroll bar. Every check passed |
| AWB Hole (M4 part 1) | 09 | **Done, tested in Inventor** | Decided by Aidan 2026-10-17; spike 081 done with Aidan. Tasks 082 to 090 checked by Aidan 2026-10-18 (making, picking, previewing, tapped and dowel holes, OK, Cancel and Undo work). Then built on branch `claude/m4-hole-generator`, compiled on Windows, not yet run in Inventor: fixes and requests 093 to 101, mockup 102 (approved with changes 2026-10-23), dowel seats 106, window redesign 104, takeover of Hole and H 091, Edit Feature and double-click 092. Aidan's first look on 2026-10-24 gave six changes, planned as tasks 103 (in place for every command, top priority), 105 and 107 to 119: client graphics preview, Simple and Taper Tapped types, Spotface, toleranced clearance holes, depths below the seat, centres per point, a window that fits at 150 percent. Aidan checked 091 to 106 on 2026-10-25: six failures and seven requests, planned as tasks 120 to 132 in the same round. That round (105 to 138) was built on Windows (2026-11-07) and checked by Aidan from 2026-11-07 to 2026-11-09, with a temporary self-test (spike 139, local only) for the measurable steps: everything passed except hand colours on a remade hole (122-6, 122-8), the sketch point mini toolbar (133-1, 133-2, 133-5) and the highlight in place (105-1). Those fixes, the marking menu's Hole, and Aidan's requests and his first-look answers became tasks 140 to 150, built on Windows on 2026-11-09 (not yet run in Inventor; checklist `docs/checklists/m4-fixes-140-150.md`). Aidan checked them on 2026-11-10: all passed but 140-2, fixed with two decisions as tasks 152 to 154, which passed the same day. PR #29 merged into main on 2026-11-10 |
| Hole table wizard (M4 part 2) | 04 | Not started | Planned after AWB Hole. Core half needs three answers; add-in half needs a Windows spike |
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
