# Status

Where each big item stands. One line per item, kept current by the parent session. For the order of work and the task lists see `docs/PLAN.md`; for what needs doing next see `TODO.md`.

Last updated: 2026-10-28

## Session handoff (ContextKeeper)

- Updated: 2026-10-28 20:45. Branch `claude/m4-hole-generator` at `de4231d`, not pushed; PR #29 open against `main`. Spike branches (local only, never merged): `spike/107-client-graphics` (`b56f0db`), `spike/108-hole-types` (`f0a2f33`).
- Goal: run the M4 part 1 round in `docs/PLAN.md` order, one implementer and one reviewer per task, one commit per task.
- Done this session:
  - `4e8de5c`: spike 107 final result (round 4 passed in Inventor: drill point, seats off the edge, end caps); tasks 107 and 108 ticked; task 110 confirmed (edge samples, vertex fallback, 0.001 cm tip).
  - `d2c2460` task 135 (Rename renames any AWB-format occurrence name; Save As and Replace keeps task 134's rule).
  - `e81fdac` task 120 (H after a shortcut reset: waits for the Customize dialog to close, then gives back and takes over; capped at 3 re-applies).
  - `c22a4f3` task 121 (reload: tab and Modify-panel button deleted on load and unload; command definitions kept, a Default in DECISIONS).
  - `task 128` (AWB Hole on the Sheet Metal tab) committed after a second review; the tab and panel ids are unconfirmed (log shows them).
- State:
  - Nothing from 135, 120 or 121 has been run in Inventor. The installed add-in is still the spike 108 round 4 build: Inventor was open all session, so every build used `-p:DeployToInventor=false`.
  - Two Core tests flaked once each (in `TODO.md`).
- Next steps:
  1. Follow the PLAN order: 129, 123, 124, 125, 122, 126, 111, 112, 113, 119, 131, 132, 127, 130, 110, 117, 114, 115, 116, 136, 118.
  2. When Inventor is closed, run `dotnet build InventorAddin.slnx -tl:off` on this branch so the installed add-in is this branch's again.
- Gotchas:
  - Check `tasklist //FI "IMAGENAME eq Inventor.exe"` before building. If Inventor is open, pass `-p:DeployToInventor=false`.
  - Task 128 must list its Sheet Metal button in Core `AddinUi.ForeignButtons`, so unload removes it.
  - Spike 107's preview code is read with `git show spike/108-hole-types:src/InventorAddin/Spike107.cs` (task 110).
- Read first: `docs/PLAN.md` ("Round after PR #29's first look"), `TODO.md` Next up, the next task file.

## Big items

| Item | Spec | State | Notes |
|---|---|---|---|
| Foundations (M0) | 01 | **Done, tested in Inventor** | Settings, log, ribbon, error reporting, Settings and About windows, editing-command base, "AWB Addin" branding |
| Part properties window (M1) | 07 | **Built and merged, partly tested in Inventor** | Opens on parts and assemblies, pre-fills, writes and undoes correctly. A never-saved part shows New part, Not saved yet and Not assigned; the blank Part type entry can be clicked. Some checklist steps not yet reported; see `TODO.md` |
| Window style and icons (M1b) | 08 | **Done, tested in Inventor** | Tasks 020 to 031. Shared look in light and dark, all three windows restyled, icons on ribbon and headers, XAML rules test. Every check passed on Windows. Two follow-ups on the build's deploy line are in `TODO.md` |
| Part creation and numbering (M2) | 02 | **Done, tested in Inventor** | Every check passed; the last run 1 checks were reported on 2026-10-17. Run 2 checked on Windows 2026-10-11; fix run 053 to 061 and fix run 2 (spike 062, tasks 063 to 066) checked 2026-10-12 and passed. Merged into main from `claude/m2-fix-run` |
| Material and finish (M3) | 03 | **Done, tested in Inventor** | Tasks 067 to 080, merged into main from `claude/m3-material-finish` on 2026-10-17: starter library and finish list, Material and Finish window and button, Part Properties Change button and warning, Settings and About on every ribbon, windows that fit the screen, Settings as one resizable page of collapsible sections, dialogs with only a close button, and a themed scroll bar. Every check passed |
| AWB Hole (M4 part 1) | 09 | **Built, waiting for Aidan's check in Inventor** | Decided by Aidan 2026-10-17; spike 081 done with Aidan. Tasks 082 to 090 checked by Aidan 2026-10-18 (making, picking, previewing, tapped and dowel holes, OK, Cancel and Undo work). Then built on branch `claude/m4-hole-generator`, compiled on Windows, not yet run in Inventor: fixes and requests 093 to 101, mockup 102 (approved with changes 2026-10-23), dowel seats 106, window redesign 104, takeover of Hole and H 091, Edit Feature and double-click 092. Aidan's first look on 2026-10-24 gave six changes, planned as tasks 103 (in place for every command, top priority), 105 and 107 to 119: client graphics preview, Simple and Taper Tapped types, Spotface, toleranced clearance holes, depths below the seat, centres per point, a window that fits at 150 percent. Aidan checked 091 to 106 on 2026-10-25: six failures and seven requests, planned as tasks 120 to 132 in the same round. The round is **Planned**, starting with spike 103 |
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
