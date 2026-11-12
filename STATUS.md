# Status

Where each big item stands. One line per item, kept current by the parent session. For the order of work and the task lists see `docs/PLAN.md`; for what needs doing next see `TODO.md`.

Last updated: 2026-11-12

## Session handoff (ContextKeeper)

- Updated: 2026-11-12 10:26. Branch `claude/m4-hole-table` (off `main`), plan commit `e44f681`; not pushed. This handoff edit is uncommitted. No open PR.
- Goal of this session: record Aidan's answers to spec 04 questions 2 to 8 and plan M4 part 2 (hole table wizard) per `docs/WORKFLOW.md`.
- Done: spec 04 gained "Decided behaviour" and open questions 9 to 17 (Defaults); `DECISIONS.md` (7 Aidan entries, 10 Default); `docs/PLAN.md` M4 part 2 table and task list; `TODO.md` Next up and the spec 04 block; task files `docs/tasks/155` to `160`. Remote branch `claude/m1b-icon-redesign` was already deleted on GitHub (push --delete said the ref does not exist).
- Current state: plan only, no code changed; nothing built or tested this session. M4 part 2 is Planned.
- Open: Aidan has not yet said whether to push the branch, or whether to start with task 155 or spike 160.
- Next steps:
  1. Ask Aidan: push `claude/m4-hole-table`? Start 155 or spike 160?
  2. Run 155 (implementer, then reviewer, then commit `task 155: ...`), then 156 and 157 (parallel-safe), 158, 159.
  3. Spike 160 with Aidan in Inventor on branch `spike/160-hole-table`; record results in its task file, then write the add-in tasks, table model, Align, Update and Edit Table re-open tasks.
- Gotchas: check `tasklist //FI "IMAGENAME eq Inventor.exe"` before building; `-p:DeployToInventor=false` while it runs. Tracking files, specs and task files are CRLF (a `grep $'\r'` check in Git Bash wrongly reported LF; use Python byte counts). The Write tool and heredoc appends give LF, so convert after. Python `\n` escapes inside bash heredocs got mangled; for one-line fixes use the Edit tool (it keeps CRLF). Numbering timing tests can fail under parallel load.
- Read first: `docs/features/04-hole-table-wizard.md` ("Decided behaviour", questions 9 to 17), `docs/tasks/155` to `160`, `docs/PLAN.md` M4 part 2. Core inputs: `InventorHoleFacts` (`Holes/HoleReadBack.cs`), `HolePrecision.cs`, `HoleTolerance` (`Holes/HoleGeometry.cs`).

## Big items

| Item | Spec | State | Notes |
|---|---|---|---|
| Foundations (M0) | 01 | **Done, tested in Inventor** | Settings, log, ribbon, error reporting, Settings and About windows, editing-command base, "AWB Addin" branding |
| Part properties window (M1) | 07 | **Built and merged, partly tested in Inventor** | Opens on parts and assemblies, pre-fills, writes and undoes correctly. A never-saved part shows New part, Not saved yet and Not assigned; the blank Part type entry can be clicked. Some checklist steps not yet reported; see `TODO.md` |
| Window style and icons (M1b) | 08 | **Done, tested in Inventor** | Tasks 020 to 031, then 151 (new window icons, Outline chosen state, ribbon option C, 7j AWB Hole; checked by Aidan 2026-11-12, merged in PR #31). Shared look in light and dark, all three windows restyled, icons on ribbon and headers, XAML rules test. Every check passed on Windows. Two follow-ups on the build's deploy line are in `TODO.md` |
| Part creation and numbering (M2) | 02 | **Done, tested in Inventor** | Every check passed; the last run 1 checks were reported on 2026-10-17. Run 2 checked on Windows 2026-10-11; fix run 053 to 061 and fix run 2 (spike 062, tasks 063 to 066) checked 2026-10-12 and passed. Merged into main from `claude/m2-fix-run` |
| Material and finish (M3) | 03 | **Done, tested in Inventor** | Tasks 067 to 080, merged into main from `claude/m3-material-finish` on 2026-10-17: starter library and finish list, Material and Finish window and button, Part Properties Change button and warning, Settings and About on every ribbon, windows that fit the screen, Settings as one resizable page of collapsible sections, dialogs with only a close button, and a themed scroll bar. Every check passed |
| AWB Hole (M4 part 1) | 09 | **Done, tested in Inventor** | Decided by Aidan 2026-10-17; spike 081 done with Aidan. Tasks 082 to 090 checked by Aidan 2026-10-18 (making, picking, previewing, tapped and dowel holes, OK, Cancel and Undo work). Then built on branch `claude/m4-hole-generator`, compiled on Windows, not yet run in Inventor: fixes and requests 093 to 101, mockup 102 (approved with changes 2026-10-23), dowel seats 106, window redesign 104, takeover of Hole and H 091, Edit Feature and double-click 092. Aidan's first look on 2026-10-24 gave six changes, planned as tasks 103 (in place for every command, top priority), 105 and 107 to 119: client graphics preview, Simple and Taper Tapped types, Spotface, toleranced clearance holes, depths below the seat, centres per point, a window that fits at 150 percent. Aidan checked 091 to 106 on 2026-10-25: six failures and seven requests, planned as tasks 120 to 132 in the same round. That round (105 to 138) was built on Windows (2026-11-07) and checked by Aidan from 2026-11-07 to 2026-11-09, with a temporary self-test (spike 139, local only) for the measurable steps: everything passed except hand colours on a remade hole (122-6, 122-8), the sketch point mini toolbar (133-1, 133-2, 133-5) and the highlight in place (105-1). Those fixes, the marking menu's Hole, and Aidan's requests and his first-look answers became tasks 140 to 150, built on Windows on 2026-11-09 (not yet run in Inventor; checklist `docs/checklists/m4-fixes-140-150.md`). Aidan checked them on 2026-11-10: all passed but 140-2, fixed with two decisions as tasks 152 to 154, which passed the same day. PR #29 merged into main on 2026-11-10 |
| Hole table wizard (M4 part 2) | 04 | Planned | Planned 2026-11-12 after Aidan answered spec 04 questions 2 to 8. Core tasks 155 to 159 first; spike 160 with Aidan on Windows settles the table type, reading a view's holes and its ordinate origin, and where edits are kept; the add-in tasks follow from it |
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
