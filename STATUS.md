# Status

Where each big item stands. One line per item, kept current by the parent session. For the order of work and the task lists see `docs/PLAN.md`; for what needs doing next see `TODO.md`.

Last updated: 2026-10-07

## Big items

| Item | Spec | State | Notes |
|---|---|---|---|
| Foundations (M0) | 01 | **Done, tested in Inventor** | Settings, log, ribbon, error reporting, Settings and About windows, editing-command base, "AWB Addin" branding |
| Part properties window (M1) | 07 | **Built and merged, partly tested in Inventor** | Opens on parts and assemblies, pre-fills, writes and undoes correctly. Some checklist steps not yet reported; see `TODO.md` |
| Window style and icons (M1b) | 08 | **Partly tested in Inventor** | All tasks 020 to 031 merged. Builds and deploys on Windows. The three windows look right in light and dark with the dark title bar, placeholders show, ribbon icons right in both themes. A 150% scale clipping bug was fixed on `main`. Remaining checks in `TODO.md` |
| Part creation and numbering (M2) | 02 | Not started | Waiting on Aidan's numbering decisions |
| Material and finish (M3) | 03 | Not started | Waiting on Aidan's finish list and a Windows spike |
| Hole table wizard (M4) | 04 | Not started | Core half needs three answers; add-in half needs a Windows spike |
| Drawing tools (M5) | 05 | Not started | Waiting on which commands Aidan wants |
| iLogic injection (M6) | 06 | Not started | Waiting on which rules |
| Distribution (installer, download page) | none yet | Parked | To be decided when the part number generator is planned |

States used: Not started, Planned, In progress, Built (merged, not tested in Inventor), Partly tested, Done (tested in Inventor), Parked, Proposed.

## What works in Inventor today

- An **AWB Addin** tab on the part, assembly and drawing ribbons.
- **Part Properties** (parts and assemblies): shows part number, part name, part type, designer, detailer, cost, material, finish and weight. Part type, designer, detailer and cost can be edited. One Undo reverts an Apply.
- **Settings**: show developer tools, default designer.
- **About**: version, build time, Inventor version, log folder.
- **One shared look** for every window, following Inventor's light or dark theme, with a dark title bar in dark.
- **Ribbon icons** on every button, and the command's icon in each window header.
- **Developer panel** (off by default): export the active document or the asset libraries to JSON.

## Numbers

- Tasks finished: 31 (001 to 031)
- Core tests: see the latest task file's verification section; they run in the cloud and on Windows with `dotnet test tests/InventorAddin.Core.Tests`
