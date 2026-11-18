# 172: Add-in: the Hole Table window checks the stored tables when it opens and offers Update

- **Milestone:** M4 part 2 (request from Aidan's check, 2026-11-18)
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Decided behaviour": Update)
- **Status:** done
- **Depends on:** 171, 167
- **Needs:** windows (add-in; Aidan checks in Inventor)
- **Parallel-safe with:** 173

## Goal

Show task 171's check in the Hole Table window: when the window opens it runs the check, and if any stored view's tables are out of date it says which and why, with a button to update those views and a way to carry on without.

## Scope

- `src/InventorAddin/UI/HoleTableWindow.xaml` and `.xaml.cs`: after the window is shown (`ContentRendered` or `Loaded` plus a dispatcher post, as the busy indicator already does), show the busy indicator and call `CheckStoredTables()`. If anything is out of date or could not be checked, show a message panel at the top of the window (the shared style's colours and spacing, spec 08) listing each view and its reason, with **Update these views** (bound to `UpdateOutOfDateCommand`) and **Not now** (dismiss). Nothing shows when every stored table is up to date.
- `src/InventorAddin/HoleTable/HoleTableSource.cs`: override `IHoleTableSource.GetKeyedViews(sheetKey)` (task 171) with a public method of the same signature that lists only views that already have a key and never calls `WriteKeys`, so the check writes nothing to the drawing (no undo entry, drawing not marked changed).
- Log the check's result once at Info (`Hole table: 1 view out of date: VIEW1 on Sheet:1 (2 holes moved)`), and every per-view Update result.

Out of scope: Core changes (task 171).

## Acceptance criteria

- [x] The check adds no undo entry and writes no keys on sheets other than the one the window lists. (Listing the active sheet's views writes keys on first use, as task 163 decided; that is unchanged.)
- [x] The check runs once per open, with the busy indicator, and the window stays usable afterwards.
- [x] Update these views updates each listed view in its own transaction (one Undo per view, as the Update button).
- [x] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors.

## Notes for the implementer

- No `UseLayoutRounding` (`CLAUDE.md`). Match the existing window's layout and the spec 08 style.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false` on Windows: 0 errors (9 existing warnings). `dotnet test tests/InventorAddin.Core.Tests`: 4861 passed, 0 failed (includes the spec 08 XAML style-rule test, which is why the notice uses a new shared `NoticePanel` style in `UI/Theme/Styles.xaml`). Not run in Inventor.
- **Not compiled (changed under `src/InventorAddin`):** none; all built on Windows. Behaviour in Inventor is untested. Changed: `UI/HoleTableWindow.xaml`, `UI/HoleTableWindow.xaml.cs`, `UI/Theme/Styles.xaml` (new `NoticePanel`), `HoleTable/HoleTableSource.cs` (`GetKeyedViews`, `GetViews` now shares `ListViews`).
- **Inventor API members not confirmed:** none new; `GetKeyedViews` reuses `ReadKeys` and the existing view enumeration.
- **Manual checklist for Inventor:**
  1. A drawing whose tables are up to date: Hole Table opens with no message.
  2. Move a hole in the model (change a sketch dimension), open Hole Table: the message names the view and "holes moved". Update these views: the table's XDIM and YDIM change, the message goes, one Undo reverts it.
  3. Add and remove a hole centre: the message says added and removed.
  4. Rotate a view so the holes are no longer seen end on: the message says they are no longer seen end on. Update removes that view's tables (as now).
  5. Not now: the message goes, and the window works as before.
  6. A drawing with tables on two sheets: both sheets' views are checked.
  7. A drawing whose tables were made before this change (version 1 state): opens without errors; moves are not reported, added and removed are.
  8. A two-sheet drawing with stored tables on sheet 2: open Hole Table on sheet 1 (its views already listed once). The Undo list has no new entry and the drawing is not marked changed (no save prompt on close). Then pick sheet 2: only that listing may add the view key undo entry, and only if its views were never listed.

## Follow-ups

- Review: "Update these views" has no access key, unlike the window's other commands (U is taken).

Things noticed but not done.

- `HoleTableSource.GetSheets` and the selected sheet's `GetViews` still write keys when the window is built (before the check), so opening on a drawing whose sheets or views were never listed still adds one key-writing undo entry from the window itself, not from the check. Only the check is write-free. If the acceptance criterion should hold for the whole open, the constructor path needs a keyed-only listing too (Core change).
- Per-view Update results are logged as the one status sentence the view-model builds (`Status`), from the window code-behind, not one log line per view.
