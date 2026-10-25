# 133: The mini toolbar's Hole button opens AWB Hole

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command")
- **Status:** done
- **Depends on:** 105, 092
- **Needs:** windows build
- **Parallel-safe with:** 109, 134 (not 105: same file)

## Goal

Left-clicking a sketch point opens Inventor's mini toolbar, and that toolbar has a Hole button. The button is Inventor's `PartDMHoleCmd`, not the ribbon's `PartHoleCmd`, so the takeover (task 091) does not cover it, and it opens Inventor's Hole. This happens in a part's own window and in a part edited in place (spike 103, round 2; Aidan, 2026-10-25). While AWB Hole has taken over Inventor's Hole, this button must open AWB Hole too.

## Scope

- `src/InventorAddin.Core/Holes/HoleTakeoverRules.cs`:
  - `public const string InventorMiniToolbarHoleCommand = "PartDMHoleCmd";` (spike 103).
  - A rule, with tests, beside `HoleEditRules.MiniToolbarIndex`: given the toolbar's command names in order, it returns the index before which `Awb_AwbHole` is inserted (Inventor's Hole entry's position, 1-based as the map is). It returns null when Inventor's entry is absent or ours is already there.
  - A backstop rule: whether a start of `PartDMHoleCmd` is stopped and AWB Hole opened instead. It is stopped when AWB Hole owns the Hole command and the start is not one the add-in asked for (task 129's "Inventor Hole" entry runs Inventor's Hole once and must not be caught).
- `tests/InventorAddin.Core.Tests/Holes/HoleTakeoverRulesTests.cs` (or the existing file for these rules).
- `src/InventorAddin/Holes/HoleEditRedirect.cs`, which already follows `OnContextualMiniToolbar` and `OnActivateCommand`:
  - **The mini toolbar.** When AWB Hole owns the Hole command and the document being edited is a part (task 105's `CurrentPart()`), `Awb_AwbHole` is inserted where `PartDMHoleCmd` is and Inventor's entry is removed. This uses the same `Insert` then `Remove` that task 092 uses for Edit Feature. This runs whatever was clicked, not only for holes, and before the Edit Feature check returns early.
  - **The backstop.** When `PartDMHoleCmd` starts anyway and the backstop rule says so, Inventor's command is stopped once its event has returned (`StopActiveCommand`, deferred as the Edit Feature backstop is), and AWB Hole opens as its button does (`AwbHoleWindowLauncher.ShowNew` on the part).
  - The class summary mentions the Hole entry.
- `src/InventorAddin/UI/HoleCommandTakeover.cs` (or wherever task 129 runs Inventor's Hole once): if 129 is done, its "Inventor Hole" run marks the next `PartDMHoleCmd` start as asked for. If 129 is not done, note in Follow-ups that 129 must do this.
- `docs/features/09-awb-hole.md`, "The command": one line. "The mini toolbar's Hole button on a sketch point opens AWB Hole too, while AWB Hole has taken over Hole (spike 103)."

Out of scope:

- The ribbon, H, and the Sheet Metal tab (tasks 091, 120, 128).
- Other mini toolbar or marking-menu entries.

## Acceptance criteria

- [ ] With the takeover on, the mini toolbar shown on a sketch point has AWB Hole's button where Inventor's Hole was, and Inventor's is gone. This holds in a part's own window and in a part edited in place.
- [ ] Pressing it opens the AWB Hole window on the part, as the ribbon button does, with the clicked point among the picks when the default picks include it.
- [ ] With "Use Inventor's Hole command" on, the mini toolbar is left as Inventor builds it.
- [ ] If Inventor's `PartDMHoleCmd` starts by another route while AWB Hole owns Hole, it is stopped and AWB Hole opens. Task 129's "Inventor Hole" still runs Inventor's Hole.
- [ ] A failure is logged and leaves Inventor's entry. Nothing throws into Inventor's events.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes, and the solution builds on Windows.

## Notes for the implementer

- Spike 103's log of the toolbar, on a `SketchPointProxy` in place, was `PartDMHoleCmd, SketchEditSketchCtxCmd, AssemblyVisibilityCtxCmd`. Pressing Hole fired `OnActivateCommand PartDMHoleCmd`. Read the entries the way `OnContextualMiniToolbar` already does (`displayed.Name[i]`, 1-based).
- The value inserted with our name is Inventor's entry's value when it has one, else our `ButtonDefinition` for AWB Hole, as task 092 does for Edit Feature (spike 081). The AWB Hole `ButtonDefinition` is kept by `RibbonSetup`; pass it in the way `_editFeature` is passed. The mini toolbar shows icons only, so our button needs its icon. Use AWB Hole's icon, not Inventor's.
- `HoleEditRedirect` asks `_owner()` at each event, so a Settings change takes effect at once. Keep that.
- A sheet metal part may show a different Hole command on its mini toolbar. Log the names seen in a sheet metal part at Info once per session, and replace only `PartDMHoleCmd`.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -tl:off -p:DeployToInventor=false` on Windows: build succeeded, 0 errors (the 8 warnings are the existing CS8600 ones, none in the changed files; checked with `--no-incremental`). `dotnet test tests/InventorAddin.Core.Tests`: 3659 passed, 0 failed, 0 skipped. Not yet run in Inventor.
- **Not compiled (changed under `src/InventorAddin`):** none; `HoleEditRedirect.cs` and `RibbonSetup.cs` were built on Windows. Behaviour in Inventor is not yet seen.
- **Inventor API members not confirmed:** none by name. Every member was already in use: `NameValueMap.Count`, `Name[i]`, `Value[name]`, `Insert(name, value, index, before)`, `Remove` (task 092, `HoleEditRedirect`), `CommandManager.StopActiveCommand` (task 092), `UserInputEvents.OnActivateCommand` and `OnContextualMiniToolbar` (task 092), `SketchPoint` as a selection type (`HoleSession`, `PartExtractor`). Behaviour not yet seen in Inventor: (1) what `Value["PartDMHoleCmd"]` holds; if it is Inventor's own definition, the inserted entry might show Inventor's icon or run Inventor's Hole instead of ours (checklist item 1 shows which); (2) whether `item is SketchPoint` is true for a `SketchPointProxy` in a sheet metal part edited in place, for the sheet metal log line (only the log depends on it); (3) whether the ribbon's `PartHoleCmd` also fires `PartDMHoleCmd`.
- **Manual checklist for Inventor:**
  1. In one of your parts, with a sketch that has points, left-click a point. The mini toolbar shows AWB Hole's icon, not Inventor's Hole. Press it: the AWB Hole window opens, and the clicked point is one of the picked centres.
  2. Repeat with the part edited in place inside an assembly.
  3. Turn "Use Inventor's Hole command" on in AWB Settings, then left-click a point: the mini toolbar has Inventor's Hole, and it opens Inventor's Hole. Turn the setting off again.
  4. If task 129 is done, choose Inventor Hole from the AWB Hole split button: Inventor's Hole opens and is not stopped.
  5. In a sheet metal part, left-click a sketch point and report what the mini toolbar shows (the log names its entries).
  5a. In a sheet metal part's flat pattern, left-click a sketch point and report what the mini toolbar shows. The flat pattern should keep Inventor's Hole (spec 09); if AWB Hole's icon shows there, report it.
  6. After items 1 to 5, open `%APPDATA%\AwbAddin\awbaddin.log`. It should have no "Hole takeover: replacing Hole on the mini toolbar failed" error. In item 1, if the window opens and the log has "Inventor's Hole from the mini toolbar started", the inserted entry ran Inventor's command and the backstop caught it. Report that line if you see it.

## Follow-ups

- **Task 129 must mark its "Inventor Hole" run as asked for.** Just before it runs Inventor's Hole, it calls `HoleEditRedirect.Active?.InventorHoleAsked()`. That lets the next `PartDMHoleCmd` start through once (`InventorHoleRunRequest` in Core). A start of `PartHoleCmd` keeps the request, and any other command clears it. If 129 runs a different command, add that command to the commands that keep the request in `InventorHoleRunRequest.StartIsAskedFor`. 129 was not done when this task ran, so nothing calls `InventorHoleAsked` yet.
- `HoleEditRules`' class remarks still say editing works "on the part open in the window only, not on a part edited in place". Task 105 changed that, so the remark is stale.
- If checklist item 1 shows Inventor's icon on the inserted entry, or pressing it opens Inventor's Hole, always pass our `ButtonDefinition` as the value for the Hole entry instead of Inventor's entry's value.
- The backstop opens AWB Hole with `ShowNew` only, as the brief says. It does not call `AwbHoleWindowLauncher.CommandPressed` first. If a session is already open when `PartDMHoleCmd` starts, the session sees a foreign command and cancels, and a new window opens. This is worth checking if it comes up.
