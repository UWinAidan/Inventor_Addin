# Status

Where each big item stands. One line per item, kept current by the parent session. For the order of work and the task lists see `docs/PLAN.md`; for what needs doing next see `TODO.md`.

Last updated: 2026-10-23

## Session handoff (ContextKeeper)

- Updated: 2026-10-23 13:28. Branch `claude/m4-hole-generator`, latest commit `2c99181` (task 102: Mockup revised after Aidan's comments).
- Goal: run M4 part 1 per `docs/WORKFLOW.md`: mockup 102, then Aidan's changes, then 106, 104, 091, 092.
- Done this session:
  - Task 102 mockup committed (`ccae5a4`), then revised after Aidan's comments (`2c99181`): `docs/ui-mockups/awb-hole-window.html` (source; render command in its top comment), `.png`, `README.md` (choices 1 to 11 approved, 12 to 15 new).
  - Plan commit `dc20443`: Aidan's decisions in `DECISIONS.md` (2026-10-23 rows: solid red preview for now, mockup approved, fit 720-tall screen, distinct icons, dowel seats); spec 09 updated (question 9 answered, new question 14); new task `docs/tasks/106-dowel-seat-sizes.md`; `docs/tasks/104-awb-hole-window-redesign.md` written in full (depends on 106); `docs/PLAN.md` table and list.
- Current state: Core tests 3382 passed (last run during 102, before 106). Nothing from 093 to 102 run in Inventor yet.
- In progress: task 106 handed to an implementer subagent (Windows build); not yet reviewed or committed. If this session ends, check `git status` for its uncommitted changes, then run the reviewer on `docs/tasks/106-dowel-seat-sizes.md` before committing.
- Next steps:
  1. Finish 106: reviewer, then commit `task 106: Dowel seats sized in the window`, copy follow-ups to `TODO.md`.
  2. Task 104 (window redesign) from the revised mockup: port the icon paths (M/H/V/L/Z in a 20x20 box) and `holeProfile()` from the HTML into Core.
  3. Tasks 091 then 092.
  4. Then update STATUS/TODO, push, and open the PR per WORKFLOW (Aidan checks 091 to 106, runs spike 103).
- Gotchas: `cat > file` with no heredoc in Git Bash waits on stdin and hangs; write scripts with the Write tool into the scratchpad. String replacements in TODO.md can miss because of near-identical wording; grep first. `GetValueFromExpression` throws "Unspecified error" with no reason; abort a nested transaction before its parent.
- Read first: `docs/WORKFLOW.md`, `docs/PLAN.md` (M4 part 1), spec `docs/features/09-awb-hole.md`, task files 104 and 106.

## Big items

| Item | Spec | State | Notes |
|---|---|---|---|
| Foundations (M0) | 01 | **Done, tested in Inventor** | Settings, log, ribbon, error reporting, Settings and About windows, editing-command base, "AWB Addin" branding |
| Part properties window (M1) | 07 | **Built and merged, partly tested in Inventor** | Opens on parts and assemblies, pre-fills, writes and undoes correctly. A never-saved part shows New part, Not saved yet and Not assigned; the blank Part type entry can be clicked. Some checklist steps not yet reported; see `TODO.md` |
| Window style and icons (M1b) | 08 | **Done, tested in Inventor** | Tasks 020 to 031. Shared look in light and dark, all three windows restyled, icons on ribbon and headers, XAML rules test. Every check passed on Windows. Two follow-ups on the build's deploy line are in `TODO.md` |
| Part creation and numbering (M2) | 02 | **Done, tested in Inventor** | Every check passed; the last run 1 checks were reported on 2026-10-17. Run 2 checked on Windows 2026-10-11; fix run 053 to 061 and fix run 2 (spike 062, tasks 063 to 066) checked 2026-10-12 and passed. Merged into main from `claude/m2-fix-run` |
| Material and finish (M3) | 03 | **Done, tested in Inventor** | Tasks 067 to 080, merged into main from `claude/m3-material-finish` on 2026-10-17: starter library and finish list, Material and Finish window and button, Part Properties Change button and warning, Settings and About on every ribbon, windows that fit the screen, Settings as one resizable page of collapsible sections, dialogs with only a close button, and a themed scroll bar. Every check passed |
| AWB Hole (M4 part 1) | 09 | **Building** | Decided by Aidan 2026-10-17; spike 081 done with Aidan. Tasks 082 to 090 built on branch `claude/m4-hole-generator` (workbook reader, ISO 286, hole rules, view-model, Settings Holes section, icon, hole feature, picking and preview, window and command). Checked by Aidan 2026-10-18: making, picking, previewing, tapped and dowel holes, OK, Cancel and Undo work; three bugs and seven requests planned as 093 to 105. 091 (takeover of Hole and H) and 092 (Edit Feature and double-click) to come |
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

- Tasks finished: 90 (001 to 090), plus 093 to 102 on branch `claude/m4-hole-generator`
- Core tests: 2650 after task 080; they run in the cloud and on Windows with `dotnet test tests/InventorAddin.Core.Tests`
