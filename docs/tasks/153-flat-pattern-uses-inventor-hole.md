# 153: In a flat pattern, H, double-click and Edit Feature open Inventor's Hole

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md`
- **Status:** todo
- **Depends on:** 142
- **Needs:** windows build
- **Parallel-safe with:** 152, 154 (disjoint files; run one after another anyway, they share the build)

## Goal

Aidan's checklist 141 (2026-11-10): on a sheet metal part's Flat Pattern, the mini toolbar shows Inventor's Hole as designed, but the H shortcut and a double-click on a hole still open AWB Hole. Decision (Aidan, 2026-11-10): Flat Pattern uses Inventor's Hole everywhere. H, double-click and Edit Feature open Inventor's Hole (or Inventor's own hole edit) there.

## Scope

- `src/InventorAddin.Core/Holes/HoleTakeoverRules.cs` and tests: the rule that, in a flat pattern, the AWB Hole command started by its shortcut hands over to Inventor's Hole, and a double-click or Edit Feature on a hole is left to Inventor. Reuse `InFlatPattern` and `TakesOverHoleEntries` where they fit.
- `src/InventorAddin/Commands/AwbHoleCommand.cs` (or wherever the command starts the session): in a flat pattern, start Inventor's `PartHoleCmd` instead of opening the window, marked as asked for so the backstop in `HoleEditRedirect` lets it through (as the split button's Inventor Hole entry does, task 142 step 5). Log one Info line.
- `src/InventorAddin/Holes/HoleEditRedirect.cs`: `OnDoubleClick` and the Edit Feature route (`EditFeaturePressed`, and the swap of Edit Feature for `HoleEditFeatureCommand` if it happens in a flat pattern) leave Inventor's own behaviour in a flat pattern. If our Edit Feature command is already showing and pressed there, it runs Inventor's edit for that hole.
- `src/InventorAddin/Commands/HoleEditFeatureCommand.cs` if needed.
- `docs/features/09-awb-hole.md`: one line under the Flat Pattern rule.

Out of scope:

- The ribbon and the marking menu in a flat pattern (task 142, passed).
- The folded model: unchanged.

## Acceptance criteria

- [ ] Core tests for the flat pattern rule.
- [ ] In a flat pattern: H opens Inventor's Hole and it stays open; double-click on a hole and Edit Feature open Inventor's hole edit. Outside a flat pattern nothing changes.
- [ ] `dotnet build InventorAddin.slnx` on Windows, 0 errors; Core tests pass.

## Notes for the implementer

Read task 142's brief and Verification (the backstop, the "asked" flag, `InFlatPattern`). Find how the split button starts Inventor's Hole as asked for and reuse it. Inventor's edit command name for a feature is not confirmed in this repo: find what Inventor runs today when our redirect is not in the way, and list any name you could not confirm. Inventor may be running: check `tasklist //FI "IMAGENAME eq Inventor.exe"` and build with `-p:DeployToInventor=false` while it runs.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. …

## Follow-ups
