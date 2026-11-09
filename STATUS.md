# Status

Where each big item stands. One line per item, kept current by the parent session. For the order of work and the task lists see `docs/PLAN.md`; for what needs doing next see `TODO.md`.

Last updated: 2026-10-29

## Session handoff (ContextKeeper)

- Updated: 2026-11-07 10:40. Branch `claude/m4-hole-generator`, everything pushed (the handoff and checklist commit is the latest). PR #29 open against `main`; its description is the round summary plus the combined checklist (2026-11-07).
- Goal: finish the M4 part 1 round after PR #29's first look (one implementer and one reviewer per task, one commit per task), then one combined Inventor checklist for Aidan.
- Done this session: 114, 115, 116, 136, 118 (window fits at 150 percent: Units on the header line via `DialogHeader.Trailing`, Centres as a row, `CompactDialogContent` styles, diagram 104 tall) and 138 (colour swatch on a toleranced hole's Tolerance row, Aidan's answer to spec 09 question 28; `HolePrecision.TolerancedRow/ColourFitIn/ColourIn`, shared with `HoleRules.Build`). Each reviewed PASS, built on Windows with 0 errors (4 old CS8600 warnings). Decisions and Defaults in `DECISIONS.md`; follow-ups in `TODO.md`.
- State: Core tests 4375; a few known flaky `Numbering` tests fail per full parallel run and pass alone. Nothing of the round (105 to 138) has run in Inventor.
- Combined checklist: `docs/checklists/m4-round-after-pr29.md` (about 200 steps, 12 groups, labelled `task-step`), committed and in PR #29's description. 134 step 6 dropped (superseded by 135); 106 steps 6, 7, 10 included.
- Next steps:
  1. Aidan runs the checklist in Inventor; failures become fix tasks in `docs/PLAN.md`.
- Open questions for Aidan (in `TODO.md` follow-ups): seat boxes shift a row when Fit replaces Fastener type (118); three hole layouts still scroll with the diagram open (118).
- Gotchas: check `tasklist //FI "IMAGENAME eq Inventor.exe"` before building, use `-p:DeployToInventor=false`; edit tracking files with Python `newline=''` and an `assert count==1` per replace; keep STATUS.md out of task commits; resume a subagent stopped on the usage limit with SendMessage.
- Read first: `docs/WORKFLOW.md`, `TODO.md` ("Next up", follow-ups for 114 to 138), `docs/checklists/m4-round-after-pr29.md`.

## Big items

| Item | Spec | State | Notes |
|---|---|---|---|
| Foundations (M0) | 01 | **Done, tested in Inventor** | Settings, log, ribbon, error reporting, Settings and About windows, editing-command base, "AWB Addin" branding |
| Part properties window (M1) | 07 | **Built and merged, partly tested in Inventor** | Opens on parts and assemblies, pre-fills, writes and undoes correctly. A never-saved part shows New part, Not saved yet and Not assigned; the blank Part type entry can be clicked. Some checklist steps not yet reported; see `TODO.md` |
| Window style and icons (M1b) | 08 | **Done, tested in Inventor** | Tasks 020 to 031. Shared look in light and dark, all three windows restyled, icons on ribbon and headers, XAML rules test. Every check passed on Windows. Two follow-ups on the build's deploy line are in `TODO.md` |
| Part creation and numbering (M2) | 02 | **Done, tested in Inventor** | Every check passed; the last run 1 checks were reported on 2026-10-17. Run 2 checked on Windows 2026-10-11; fix run 053 to 061 and fix run 2 (spike 062, tasks 063 to 066) checked 2026-10-12 and passed. Merged into main from `claude/m2-fix-run` |
| Material and finish (M3) | 03 | **Done, tested in Inventor** | Tasks 067 to 080, merged into main from `claude/m3-material-finish` on 2026-10-17: starter library and finish list, Material and Finish window and button, Part Properties Change button and warning, Settings and About on every ribbon, windows that fit the screen, Settings as one resizable page of collapsible sections, dialogs with only a close button, and a themed scroll bar. Every check passed |
| AWB Hole (M4 part 1) | 09 | **Checked in Inventor; fixes planned (tasks 140 to 148)** | Decided by Aidan 2026-10-17; spike 081 done with Aidan. Tasks 082 to 090 checked by Aidan 2026-10-18 (making, picking, previewing, tapped and dowel holes, OK, Cancel and Undo work). Then built on branch `claude/m4-hole-generator`, compiled on Windows, not yet run in Inventor: fixes and requests 093 to 101, mockup 102 (approved with changes 2026-10-23), dowel seats 106, window redesign 104, takeover of Hole and H 091, Edit Feature and double-click 092. Aidan's first look on 2026-10-24 gave six changes, planned as tasks 103 (in place for every command, top priority), 105 and 107 to 119: client graphics preview, Simple and Taper Tapped types, Spotface, toleranced clearance holes, depths below the seat, centres per point, a window that fits at 150 percent. Aidan checked 091 to 106 on 2026-10-25: six failures and seven requests, planned as tasks 120 to 132 in the same round. That round (105 to 138) was built on Windows (2026-11-07) and checked by Aidan from 2026-11-07 to 2026-11-09, with a temporary self-test (spike 139, local only) for the measurable steps: everything passed except hand colours on a remade hole (122-6, 122-8), the sketch point mini toolbar (133-1, 133-2, 133-5) and the highlight in place (105-1). Those fixes, the marking menu's Hole, and Aidan's requests are planned as tasks 140 to 148 |
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
