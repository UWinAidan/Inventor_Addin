# Status

Where each big item stands. One line per item, kept current by the parent session. For the order of work and the task lists see `docs/PLAN.md`; for what needs doing next see `TODO.md`.

Last updated: 2026-11-13

## Session handoff (ContextKeeper)

- Updated: 2026-11-17 11:20 (round 7, one centre mark for every hole). Goal: spike 160 for Aidan. No add-in code was changed, so nothing was built or tested. Branch `claude/m4-hole-table`, latest commit `352fa6d`.
  - Uncommitted here: this file, `TODO.md`, `DECISIONS.md`, `.gitignore` (adds `spike-local/`), and the task 160 results (rounds 2 to 7).
  - Spike scripts are on branch `spike/160-hole-table` (worktree `../Inventor_Addin-spike160`, folder `spikes/160/`). Pushed up to `c128267` with Aidan's OK. `caff63d` (round 7) is committed but **not pushed**.
- Aidan reviewed round 6: option 1 is final (each table one sketched symbol, Update redraws it, edits through the wizard). Logged in `DECISIONS.md`, with his centre mark rule.
- Round 7 on the `_spike` drawing (not saved; `spike160-round7.ps1`, task 160 "Round 7"):
  - One local style `AWB Centre Mark` (gap 0.0001, mark size 0.01, overshoot and extension 0.2 cm) on every hole's outermost visible circle: seat, thread circle (`kThreadEdge`), or the hole. Round 6's seat style is deleted.
  - Precision mark symbol lost its own centre lines; the AWB centre mark draws them.
  - Measured: lines end 2.0 mm past the circle on E3, A1 and B2.
- Next:
  1. Aidan checks round 7 (pictures `spike-local/160/round7-*.png`); ask whether to push `caff63d`.
  2. Write the add-in tasks (task 160 "Then") for the symbol tables: Update redraws the definitions; edits live in attribute sets (C10); hole ids use the reference key (C11, task 155); create `AWB Centre Mark` if missing.
- Gotchas:
  - PowerShell: variable names ignore case. An `AttributeSet` is enumerable, so returning one through a helper unrolls it. `Log` inside a function must use `Write-Host`.
  - `SketchedSymbolDefinition.Edit` needs a by-ref `DispatchWrapper` through `InvokeMember`. The sketch it returns is dead after `ExitEdit`, so read counts before.
  - Sketched symbols have no `RangeBox`, so work out a table's extent from its widths and heights.
  - Aborting a part transaction leaves `Dirty` true; clear it when the part is restored.
  - Centre mark style `GapDistance = 0` is refused ("Value does not fall within the expected range"); 0.0001 cm works. With extension lines on, `OvershootDistance` (not `ExtensionLength`) sets how far the lines run past the circle.
  - Bash heredocs containing PowerShell `$(...)` failed to parse; write files with the Write tool instead. `TODO.md`, `DECISIONS.md` and the task files are CRLF; `STATUS.md` is LF.
- Read first: task 160 "Round 6" and "Round 7", `spike-local/160/spike160-round7.ps1`.

## Big items

| Item | Spec | State | Notes |
|---|---|---|---|
| Foundations (M0) | 01 | **Done, tested in Inventor** | Settings, log, ribbon, error reporting, Settings and About windows, editing-command base, "AWB Addin" branding |
| Part properties window (M1) | 07 | **Built and merged, partly tested in Inventor** | Opens on parts and assemblies, pre-fills, writes and undoes correctly. A never-saved part shows New part, Not saved yet and Not assigned; the blank Part type entry can be clicked. Some checklist steps not yet reported; see `TODO.md` |
| Window style and icons (M1b) | 08 | **Done, tested in Inventor** | Tasks 020 to 031, then 151 (new window icons, Outline chosen state, ribbon option C, 7j AWB Hole; checked by Aidan 2026-11-12, merged in PR #31). Shared look in light and dark, all three windows restyled, icons on ribbon and headers, XAML rules test. Every check passed on Windows. Two follow-ups on the build's deploy line are in `TODO.md` |
| Part creation and numbering (M2) | 02 | **Done, tested in Inventor** | Every check passed; the last run 1 checks were reported on 2026-10-17. Run 2 checked on Windows 2026-10-11; fix run 053 to 061 and fix run 2 (spike 062, tasks 063 to 066) checked 2026-10-12 and passed. Merged into main from `claude/m2-fix-run` |
| Material and finish (M3) | 03 | **Done, tested in Inventor** | Tasks 067 to 080, merged into main from `claude/m3-material-finish` on 2026-10-17: starter library and finish list, Material and Finish window and button, Part Properties Change button and warning, Settings and About on every ribbon, windows that fit the screen, Settings as one resizable page of collapsible sections, dialogs with only a close button, and a themed scroll bar. Every check passed |
| AWB Hole (M4 part 1) | 09 | **Done, tested in Inventor** | Decided by Aidan 2026-10-17; spike 081 done with Aidan. Tasks 082 to 090 checked by Aidan 2026-10-18 (making, picking, previewing, tapped and dowel holes, OK, Cancel and Undo work). Then built on branch `claude/m4-hole-generator`, compiled on Windows, not yet run in Inventor: fixes and requests 093 to 101, mockup 102 (approved with changes 2026-10-23), dowel seats 106, window redesign 104, takeover of Hole and H 091, Edit Feature and double-click 092. Aidan's first look on 2026-10-24 gave six changes, planned as tasks 103 (in place for every command, top priority), 105 and 107 to 119: client graphics preview, Simple and Taper Tapped types, Spotface, toleranced clearance holes, depths below the seat, centres per point, a window that fits at 150 percent. Aidan checked 091 to 106 on 2026-10-25: six failures and seven requests, planned as tasks 120 to 132 in the same round. That round (105 to 138) was built on Windows (2026-11-07) and checked by Aidan from 2026-11-07 to 2026-11-09, with a temporary self-test (spike 139, local only) for the measurable steps: everything passed except hand colours on a remade hole (122-6, 122-8), the sketch point mini toolbar (133-1, 133-2, 133-5) and the highlight in place (105-1). Those fixes, the marking menu's Hole, and Aidan's requests and his first-look answers became tasks 140 to 150, built on Windows on 2026-11-09 (not yet run in Inventor; checklist `docs/checklists/m4-fixes-140-150.md`). Aidan checked them on 2026-11-10: all passed but 140-2, fixed with two decisions as tasks 152 to 154, which passed the same day. PR #29 merged into main on 2026-11-10 |
| Hole table wizard (M4 part 2) | 04 | In progress | Spike 160 done with Aidan 2026-11-17: tables drawn as sketched symbols, one `AWB Centre Mark` style on every hole (task 160 "Recommendations"). Core tasks 155 to 159, 161 and 162 run first; then the add-in tasks 163 to 167 |
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
