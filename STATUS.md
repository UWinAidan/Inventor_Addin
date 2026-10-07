# Status

Where each big item stands. One line per item, kept current by the parent session. For the order of work and the task lists see `docs/PLAN.md`; for what needs doing next see `TODO.md`.

Last updated: 2026-10-07

## Big items

| Item | Spec | State | Notes |
|---|---|---|---|
| Foundations (M0) | 01 | **Done, tested in Inventor** | Settings, log, ribbon, error reporting, Settings and About windows, editing-command base, "AWB Addin" branding |
| Part properties window (M1) | 07 | **Built and merged, partly tested in Inventor** | Opens on parts and assemblies, pre-fills, writes and undoes correctly. Some checklist steps not yet reported; see `TODO.md` |
| Window style and icons (M1b) | 08 | In progress | Cloud run 1 done (021 to 025): Core text and theme logic, icon drawings, build deploy line. Spike 020 done on Windows. Cloud run 2 (026 to 031) in progress |
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
- **Developer panel** (off by default): export the active document or the asset libraries to JSON.

## Numbers

- Tasks finished: 25 (001 to 025)
- Core tests: see the latest task file's verification section; they run in the cloud and on Windows with `dotnet test tests/InventorAddin.Core.Tests`
