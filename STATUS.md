# Status

Where each big item stands. One line per item, kept current by the parent session. For the order of work and the task lists see `docs/PLAN.md`; for what needs doing next see `TODO.md`.

Last updated: 2026-11-22

## Session handoff (ContextKeeper)

- Updated: 2026-11-22 18:06. Branch `claude/m5-drawing-tools`, latest commit `348dbe2` (pushed).
- Goal: M5 tasks in chunks: implementer, then reviewer, one commit per task, push. Stop after each chunk with a summary, Defaults and a checklist. Nothing from work prints goes in the repo (Aidan's screenshots are in ADDIN_PICS, gitignored).
- Done this session:
  - Spikes 183 and 184; chunk 1 (211, 185, 186, 189, 190); 214 and 213.
  - 187 code (`58323b0`). Aidan's first template check gave 11 items, now tasks 215 to 221 and 210 reworked (`700fc55`).
  - Done since: 215 `677df08` (layout: mockup sizes, tolerance columns, baselines, centred border, `PrintTextFit` width estimate), 216 `e462f3b`/`dc52b8c` (decimal marker on dimension and FCF styles, STATUS reads User Status id 17, FontSize = size × 0.727 cap height, RangeBox overflow warnings), 221 `7d2339b` (Settings labels).
  - Q28 answered (`348dbe2`): try (a) the part's mass unit set to lb (spike); if a fresh drawing still prints kg, use (b), a Mass text property refreshed whenever the part is saved.
  - Core tests 5161 passed at the last run; the solution builds with 0 errors.
- In progress:
  - The 217 implementer is running (finish print name and Finish Spec backfill); its result is not seen yet (unverified).
  - Aidan deployed the 216 build and is to rebuild both templates with Build Print Template and check them against task 216's checklist. He also reports the log's "width ratio" mean (about 1.0 confirms FontSize is the cap height).
  - The Q28 spike `spike-local/183/spike-q28-lb.ps1` is ready. It failed only because Inventor was closed (`GetActiveObject` MK_E_UNAVAILABLE). Rerun it when Aidan has Inventor open with `AWB Imperial.idw` closed. It works on a copy in `C:\AwbSpike\q28\`.
- Next:
  1. Run the Q28 spike. If lb prints, write a task: New Part and Mass Update set an imperial part's mass unit to lb. If not, write a task for (b): a Mass property written on part save (an on-save event handler) and the template field switched to it. Record the result in spec 05 question 28.
  2. When Aidan passes the rebuilt templates, commit `libraries/Templates/AWB Metric.idw` and `AWB Imperial.idw`, and mark 187 and 216 done in their task files and in PLAN. If he reports problems, write fix tasks.
  3. Review 217, then commit it. Then 219, 210 and 220. Run spike 218 (shrinking long title block values) with Aidan's OK.
  4. Then chunk 2's 188, 191, 192 and 193, and the chunk 2 summary.
- Gotchas:
  - Numbering and RegisterStore tests are flaky on timing; rerun.
  - Agents can hit the session limit; resume them with SendMessage.
  - In Python, '\a' and '\1' inside path strings become control characters. Use raw strings or chr(92).
  - Bash heredocs holding Python with quotes break; write a .py file in the job tmp folder.
  - Don't build while Aidan deploys (both use `obj/`).
- Read first: `docs/WORKFLOW.md`, `docs/PLAN.md` M5, tasks 216 and 217 to 221, DECISIONS.md rows dated 2026-11-22.

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
