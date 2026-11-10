# 141: The sketch point mini toolbar keeps every button, with AWB Hole in Hole's place

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command")
- **Status:** done
- **Depends on:** 133
- **Needs:** windows build
- **Parallel-safe with:** 140, 143 (not 142: same file)

## Goal

Product bug from Aidan's checks 133-1, 133-2 and 133-5 (2026-11-09). It happens in normal parts, in place and in sheet metal parts. While AWB Hole has taken over, left-clicking a sketch point shows a mini toolbar with only Extrude and Revolve. Inventor's Hole is removed, AWB Hole is not added, and Edit Sketch and Create Sketch are gone too. With "Use Inventor's Hole" on, the toolbar shows Extrude, Revolve, Hole, Edit Sketch and Create Sketch. AWB Hole must take Hole's place, and every other button must stay where it was.

## Scope

- `src/InventorAddin/Holes/HoleEditRedirect.cs`: `ReplaceMiniToolbarHole` and `OnContextualMiniToolbar`.
  - Log the toolbar's entries (names, in order) **before and after** the swap, at Info, for each mini toolbar on a sketch point, not only in sheet metal.
  - Find why the entries after Hole are lost. Suspects: `Insert`'s index or before/after flag, an entry value Inventor will not take, or `Insert` failing part-way with no exception. Fix it so the result is Inventor's list with `PartDMHoleCmd` replaced by `Awb_AwbHole` at the same position.
  - If the inserted entry would show Inventor's icon or run Inventor's Hole, insert our `ButtonDefinition` (task 133 follow-up).
  - If the list after the swap is not Inventor's list with that one change, put Inventor's list back and log a warning. A broken toolbar is worse than Inventor's Hole.
- `src/InventorAddin.Core/Holes/HoleTakeoverRules.cs` and its tests: any changed index rule, and a rule that checks the list after the swap against the list before.

Out of scope:

- The marking menu (task 142).
- The Edit Feature swap on a hole's mini toolbar (task 092), which works.

## Acceptance criteria

- [x] Core tests for any changed or new rule.
- [ ] The log has a before line and an after line for each sketch point mini toolbar. (Written; waits on Aidan in Inventor.)
- [x] Built on Windows, 0 errors; Core tests pass. (Numbering timing tests can fail under load; see Verification.)

## Notes for the implementer

Read task 133's brief and Follow-ups, and `HoleEditRules.MiniToolbarIndex` (the Edit Feature swap). `NameValueMap.Insert` and `Remove` are already in use. Check what `Insert`'s last argument means in the API reference, and say what you found.

## Verification

### What was found

- **`NameValueMap.Insert(Name, Value, TargetIndex, InsertBefore)`**, from `Autodesk.Inventor.Interop.xml` (Inventor 2026): `TargetIndex` is "the existing name value pair next to which the new name value pair will be inserted", either a numeric index or the name of an existing pair; left out, the pair goes at the end. `InsertBefore` (the last argument) is "if the name value pair should be inserted before or after the target index", default `True`, ignored when there is no target. So `Insert(ours, value, i, true)` with `i` the 1-based index of `PartDMHoleCmd` puts ours just before it, as intended. The index and the flag are not the bug.
- **What an entry's value is.** The same file, on `UserInputEvents.OnContextualMiniToolbar`'s `DisplayedCommands`: "Name contains the internal name of the command and Value contains the entity to be highlighted when the user hovers over this command in the contextual toolbar." The value is not the command: Inventor finds the command by the entry's name. So the inserted entry cannot show Inventor's icon or run Inventor's Hole because of its value, and putting our `ButtonDefinition` there (the task 133 follow-up) is the wrong fix.
- **Leading suspect for the lost entries.** Task 133 inserted `value ?? _awbHole`: when Inventor's `PartDMHoleCmd` entry had no value, our `ButtonDefinition` became the entry's "entity to highlight". That is not an entity. The Edit Feature swap does the same (`value ?? _editFeature`) and works, most likely because Inventor's Edit Feature entry carries the hole as its value, so the fallback never applies there. On a sketch point the Hole entry may carry no value. If Inventor stops building the toolbar's map entries at one it cannot use, this matches what Aidan saw: Extrude and Revolve (not in the map at all; the logged map on a sheet metal sketch point is `PartDMHoleCmd, SketchEditSketchCtxCmd, PartVisibilityCtxCmd`) stay, and every map entry from Hole on is lost. **Not proven**: what `Value["PartDMHoleCmd"]` holds is not yet known. The new before line logs each entry's value type (`[SketchPoint]`, `[no value]` and so on), which settles it on the first click.
- **The fix.** The inserted entry now takes Inventor's entry's value exactly as it is, null included; nothing else is ever put in. The before and after lists are logged at Info, and the after list is checked against the before list (`HoleTakeoverRules.MiniToolbarSwapHeld`). If it is not Inventor's list with that one change, or `Insert` or `Remove` throws (caught, then the list checked like any other result):
  - **Still Inventor's list** (`SameMiniToolbar`; for example `Insert` refused a null value before changing anything): the map is left alone and a warning logged. An intact list is never cleared.
  - **Otherwise** a warning is logged and only what changed is undone (`UndoMiniToolbarSwap`): `Awb_AwbHole` removed if it is there, and `PartDMHoleCmd` put back with its value before the entry now at its old index (or added at the end if the list is now shorter) if it is missing.
  - **Only if that does not give Inventor's list back** is the map cleared and each entry added again in order, each `Add` guarded on its own (`ComSafe.Run`); entries that could not be added, and entries added back with no value because their value could not be read before the swap, are logged.
  - The list after the put-back is logged. Hole then shows Inventor's icon; the task 133 backstop still turns its start into AWB Hole.
- **Review fix (round 2).** The first version cleared and re-added the whole map on any failure. If `Insert` had refused the value, that would have touched an intact list and could have left it empty if an `Add` threw. Fixed as above.
- **Limit of the check.** The check sees the map, not the toolbar Inventor draws from it. If the map is right after the swap but Inventor still drops entries when drawing, the log will show a correct after line and the toolbar will still be short. That would point at the value (or something else Inventor reads from the entry) and needs Aidan's log to go further.

### Verification

- **Ran:**
  - `dotnet build InventorAddin.slnx -p:DeployToInventor=false --no-incremental` on Windows (Inventor was running, so no deploy), after the review fix: build succeeded, 0 errors. Warnings only in files this task did not touch (CS8600 in four commands, xUnit2012 in `HoleDiagramTests.cs`).
  - `dotnet test tests/InventorAddin.Core.Tests`, after the review fix: 4405 passed, 2 failed of 4407. The 2 are known Numbering timing tests (`SaveAsReplacerTests.Replace_NameThatCannotBeSet_...`, `RenamePlanTests.ListFilesToSearch_ASlowListing_...`); run alone those two classes pass, 257 of 257. (The first run, before the fix, had 4 such timing failures, all passing alone.) `HoleTakeoverRulesTests`: 96 of 96 pass.
- **Not compiled (changed under `src/InventorAddin`):** none. `src/InventorAddin/Holes/HoleEditRedirect.cs` was built on Windows. The add-in was not deployed, since Inventor was running: close Inventor and build without `-p:DeployToInventor=false` before testing.
- **Inventor API members not confirmed:** none by name. `NameValueMap.Count`, `Name[i]`, `Value[name]`, `Insert`, `Remove` were already in use (task 092); `NameValueMap.Clear` and `Add(name, value)` are new here and are in `Autodesk.Inventor.Interop.xml` and compiled; `Microsoft.VisualBasic.Information.TypeName` is already used in `SaveAsReplaceCommand`. Behaviour not yet seen in Inventor: (1) what `Value["PartDMHoleCmd"]` holds on a sketch point (the before line shows it); (2) whether `Insert` accepts a null value (if it throws and the list is unchanged, the map is left alone and the warning names the error); (3) whether the undo (`Remove`, `Insert`/`Add` of `PartDMHoleCmd`) or, as a last resort, `Clear` then `Add` gives Inventor back a toolbar it draws as before; (4) whether `item is SketchPoint` is true for a sketch point proxy in place (only decides whether a toolbar with no `PartDMHoleCmd` is logged; a toolbar with it is always logged).
- **Manual checklist for Inventor:** close Inventor, build `InventorAddin.slnx` with deploy, start Inventor. Logs are in `%APPDATA%\AwbAddin\awbaddin.log`.
  1. In a part, with AWB Hole active, left-click a sketch point. The toolbar shows Extrude, Revolve, AWB Hole (in Hole's place), Edit Sketch, Create Sketch. The log has a line "Hole takeover: the mini toolbar on a sketch point in a part, before the swap: PartDMHoleCmd [...], ..." and one "..., after the swap: Awb_AwbHole [...], ..." with the same entries in the same order. AWB Hole opens on the point. Send the two lines whatever the result: the type in brackets after `PartDMHoleCmd` is the value Inventor gave.
  2. The same in a part edited in place, and in a sheet metal part (the line says "in a sheet metal part").
  3. If the toolbar shows Inventor's Hole icon instead of AWB Hole's: the log has a warning, either "replacing Hole on the mini toolbar changed nothing; ... is left as Inventor gave it" or "... should have left ... but left ..." followed by a line "after Inventor's list was put back: ...". The toolbar must still show every button; Hole then opens AWB Hole through the backstop. If the warning says "The swap threw", check that the toolbar still has Edit Sketch and Create Sketch. Send those lines.
  4. If the after line is right but the toolbar is still short: send the before and after lines; the map was right and Inventor dropped entries when drawing it.
  5. With "Use Inventor's Hole" on: Inventor's toolbar is unchanged, no before or after line is logged, and Hole opens Inventor's Hole.
  6. On a hole, Edit Feature on the mini toolbar still opens the AWB Hole window (task 092 unchanged).

## Follow-ups

- The Edit Feature swap in `OnContextualMiniToolbar` still inserts `value ?? _editFeature`, putting a `ButtonDefinition` in as the entity to highlight when Inventor's entry has no value. It works today (Aidan's check), probably because the value is the hole. Once this task's log shows what values look like, consider passing Inventor's value as it is there too, and checking the list after that swap the same way (task 092's code; out of scope here).
- Task 133's follow-up to insert our `ButtonDefinition` as the value can be closed: per the API reference the value is the entity highlighted on hover, not the command.
- Once Aidan's log settles what `PartDMHoleCmd`'s value is and whether the swap holds, drop the before and after Info lines to Debug, or log them once per session per part kind: they are written on every sketch point click.
- The sheet-metal-only log line of task 133 (`SheetMetalMiniToolbarMessage`) is gone; the before line covers every part kind and says "in a sheet metal part" there.
