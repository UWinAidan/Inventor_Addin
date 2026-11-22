# Status

Where each big item stands. One line per item, kept current by the parent session. For the order of work and the task lists see `docs/PLAN.md`; for what needs doing next see `TODO.md`.

Last updated: 2026-11-22

## Session handoff (ContextKeeper)

- Updated: 2026-11-22 10:31. Branch `claude/m5-drawing-tools`, latest commit `384c7ae`. Nothing committed this session; docs edits below are uncommitted.
- Goal: run spikes 183 and 184 live with Aidan in Inventor, then M5 tasks in PLAN order in chunks of 4 to 5 (implementer, reviewer, one commit per task, push after each; stop after each chunk with a summary, Defaults and an Inventor checklist). Fit in a Finish Spec property task (spec 05 q23) and the two 179 follow-ups in TODO.md. Nothing from work prints in the repo.
- Done this session:
  - Spike 183 rounds 1 and 2 done and recorded in `docs/tasks/183-spike-print-template-and-views.md` (Results). Covered: title block from code, field syntax, border zones, logo, ISO start file, third angle via a local standard copy, white sheet colour, sheet formats A4/A3/ANSI B, saving the `.idw` template, new drawings from it, custom fields matched by name with no id.
  - Aidan decided (DECISIONS.md 2026-11-22): a custom "Status" property printed by the template, written by Part Properties alongside Design Status; the property name is "Material Size". Default row: a Status mismatch on open is a pre-fill (spec 07 q7).
  - Edited DECISIONS.md, spec 05, spec 07 (q7), tasks 185 and 187, PLAN.md; added `docs/tasks/210-part-properties-status.md`.
  - Renamed the test part's "Material size" to "Material Size" and saved the part (Aidan allowed it).
- Current state: no code changed; nothing built or tested this session. Test template at `C:\AwbSpike\183\AWB-spike.idw` (outside the repo). Unsaved `Drawing5` is open in Inventor (closing it is harmless).
- Open:
  - Sheet metal part mass prints "N/A" (asked Aidan to check its iProperties Physical tab).
  - Triad lands about 8 mm off: `SketchedSymbol.Position` is the symbol centre, so the build must offset it.
  - Waiting on Aidan's go for round 3.
- Next:
  1. Spike 183 round 3, on Aidan's go: views on a new drawing from the test template.
     - Part: base, projected top and right (third angle), an iso view, shaded and hidden-line.
     - Sheet metal part: the flat pattern view.
     - Read back `DrawingView` Width, Height and Center.
     - Assembly `C:\Users\4aida\OneDrive\Documents\CADING\DESIGN FILES\PRINT PROFILES\DESK\DES_ASSY.iam`: parts list and revision table styles and columns (Vendor, Stock Number, a custom Make/Buy).
  2. Record round 3. Update tasks 186, 187 and 192 with the confirmed members (views are task 192; task 183's text says 191, which is wrong). Commit the spike docs.
  3. Spike 184 (`docs/tasks/184-spike-drawing-tools-api.md`).
  4. M5 chunks, as in the goal.
- Gotchas:
  - PowerShell names ignore case. `$Part` clobbered `$part`, and `$ud` clobbered `$UD`, which caused all the "Value does not fall within the expected range" refusals of custom fields. `$pid` is read-only. `MP` is an alias, so don't use it as a function name.
  - A function that logs with Write-Output returns those lines; log with `[Console]::WriteLine` instead.
  - Don't pipe the long scripts through `tail`: they take minutes, `tail` shows nothing until the end, and the job got moved to the background.
  - `GetResultText` needs text boxes from `Definition.Sketch.TextBoxes`, not the ones `AddFitted` returned during Edit.
  - `Documents.Add` fails while the template is open in Inventor.
  - `AddUsingSheetFormat`: pass `Missing` as the model.
  - Logs contain the material name from Aidan's library; keep it out of the repo.
- Read first: task 183 Results, `spike-local/183/` (scripts and logs; round2c, probe2 and probe3 are the latest), DECISIONS.md 2026-11-22 rows.

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
