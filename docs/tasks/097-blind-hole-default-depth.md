# 097: Blind holes start at the last depth used, or 2 x D

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("Blind holes", "The workbook"; question 7)
- **Status:** todo
- **Depends on:** 090
- **Needs:** windows build (Core plus a settings field)
- **Parallel-safe with:** 093, 094, 095, 096

## Goal

Aidan's answer to spec 09 question 7: a blind clearance or dowel hole starts at the last depth used, or at 2 x D the first time, where 2 is a value on the workbook's Settings tab.

## Scope

- Core: `HoleWorkbookReader` and `HoleSettings` (a new Settings value, "Blind depth multiplier", starter 2), `AwbHoleViewModel` (the starting depth), `AddinSettings` (the last depth used), with tests
- `libraries/AWB_Holes.xlsx`: add the setting to the Settings tab, in the tab's own layout. Edit the package's XML directly (unzip, change the sheet and shared strings, zip again) so every other cell, formula and cached value stays as it is; do not save it through a library that drops cached formula values. Check it still reads with no problems.
- `src/InventorAddin/Holes/HoleSession.cs` or the window: record the depth on OK

## Acceptance criteria

- [ ] A new blind clearance or dowel hole (Termination Distance) starts at the depth of the last blind clearance or dowel hole made with OK in those units (Metric and Imperial each remember their own), else at multiplier x the size's nominal diameter, rounded as the thread depth is.
- [ ] The last depth is kept in the add-in's settings file, so it survives a restart (Default). A depth that was an expression (task 100) is kept as its value.
- [ ] The Settings value is read like the thread multipliers: a missing or unreadable value uses the starter value; a workbook without the row (Aidan's own copy) reads with no problem reported, since the setting is new.
- [ ] Switching to Distance, changing the size or units, on a hole with no depth typed in this window: the depth follows these rules; a depth the user typed in this window is kept.
- [ ] OK is no longer disabled just because a blind hole has no depth; an emptied depth box still disables it.
- [ ] An edited hole keeps its own depth.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; `dotnet build InventorAddin.slnx -p:DeployToInventor=false` builds

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. With a fresh settings file: a new clearance M6, Distance: the depth is 12 mm (2 x 6). OK.
  2. A new hole, Distance: 12 mm again. Type 20, OK. The next new blind hole starts at 20 mm, also after an Inventor restart. Imperial starts from its own last depth.
  3. Change the Settings tab's value to 3 in your workbook, clear the settings file's last depth (or use a fresh one): M6 starts at 18 mm.

## Follow-ups
