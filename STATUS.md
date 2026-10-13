# Status

Where each big item stands. One line per item, kept current by the parent session. For the order of work and the task lists see `docs/PLAN.md`; for what needs doing next see `TODO.md`.

Last updated: 2026-10-12

## Big items

| Item | Spec | State | Notes |
|---|---|---|---|
| Foundations (M0) | 01 | **Done, tested in Inventor** | Settings, log, ribbon, error reporting, Settings and About windows, editing-command base, "AWB Addin" branding |
| Part properties window (M1) | 07 | **Built and merged, partly tested in Inventor** | Opens on parts and assemblies, pre-fills, writes and undoes correctly. A never-saved part shows New part, Not saved yet and Not assigned; the blank Part type entry can be clicked. Some checklist steps not yet reported; see `TODO.md` |
| Window style and icons (M1b) | 08 | **Done, tested in Inventor** | Tasks 020 to 031. Shared look in light and dark, all three windows restyled, icons on ribbon and headers, XAML rules test. Every check passed on Windows. Two follow-ups on the build's deploy line are in `TODO.md` |
| Part creation and numbering (M2) | 02 | **Done, tested in Inventor** (a few run 1 checks still to report; see `TODO.md`) | Run 2 checked on Windows 2026-10-11; fix run 053 to 061 and fix run 2 (spike 062, tasks 063 to 066) checked 2026-10-12 and passed. Merged into main from `claude/m2-fix-run` |
| Material and finish (M3) | 03 | **Planned: spike only** | Aidan answered the spec on 2026-10-12. Spike 067 done; the rest is planned after Aidan has seen its results |
| Hole generator and hole table wizard (M4) | 04 | Not started | Starts with a hole generator for part files (details to discuss with Aidan). Wizard: Core half needs three answers; add-in half needs a Windows spike |
| Drawing tools (M5) | 05 | Not started | Waiting on which commands Aidan wants. Will include a custom drawing template |
| iLogic injection (M6) | 06 | Not started | Waiting on which rules |
| Distribution (installer, download page) | none yet | Parked | Was to be decided when M2 was planned; still waiting on Aidan |

States used: Not started, Planned, In progress, Built (merged, not tested in Inventor), Partly tested, Done (tested in Inventor), Parked, Proposed.

## What works in Inventor today

- An **AWB Addin** tab on the part, assembly and drawing ribbons.
- **Part Properties** (parts and assemblies): shows part number, part name, part type, designer, detailer, cost, material, finish and weight. Part type, designer, detailer and cost can be edited. One Undo reverts an Apply.
- **One shared window look** in Inventor's light and dark themes, with keyboard navigation, and icons on the ribbon buttons and window headers.
- **Settings**: show developer tools, default designer.
- **About**: version, build time, Inventor version, log folder.
- **Developer panel** (off by default): export the active document or the asset libraries to JSON.
- **Build deploy line**: a Windows build copies the add-in into Inventor and says whether it did. The `Deployed` line shows only with `-tl:off`; see `TODO.md`.

## Numbers

- Tasks finished: 67 (001 to 067)
- Core tests: 2187 after task 066; they run in the cloud and on Windows with `dotnet test tests/InventorAddin.Core.Tests`
