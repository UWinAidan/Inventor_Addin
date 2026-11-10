# 142: The marking menu's Hole follows the takeover

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command")
- **Status:** done
- **Depends on:** 141
- **Needs:** windows build
- **Parallel-safe with:** 140, 143 (not 141: same file)

## Goal

A part's right-click marking menu (the radial ring) has a Hole button with Inventor's icon. It opens Inventor's Hole even while AWB Hole has taken over. It must open AWB Hole by the same rule as the mini toolbar's Hole, and Inventor's Hole when "Use Inventor's Hole" is on (Aidan, 2026-11-07, `DECISIONS.md`).

## Scope

- `src/InventorAddin/Holes/HoleEditRedirect.cs`:
  - Handle the radial marking menu. `UserInputEvents.OnRadialMarkingMenu` is the likely event, but it is unconfirmed. Check it in the interop by reflection, as task 122 did, and name what you found.
  - Log the ring's commands once per session at Info, as task 133 logs the sheet metal toolbar.
  - Put AWB Hole, on our `ButtonDefinition`, in the place of the ring's Hole entry, by the same rule as the mini toolbar.
  - If the ring's Hole is a command the takeover's backstop does not catch yet, add it to the backstop rule.
  - Unsubscribe in `Dispose`, as for the other events.
- `src/InventorAddin.Core/Holes/HoleTakeoverRules.cs` and its tests: the command name and any rule.
- `docs/features/09-awb-hole.md`: one line.

Out of scope:

- The linear marking menu's Edit Feature (task 092), already handled.

## Acceptance criteria

- [x] Core tests for the rule.
- [ ] The ring's entries are logged once per session. (Written; waits on Aidan in Inventor.)
- [x] Built on Windows, 0 errors; Core tests pass.

## Notes for the implementer

If the radial menu cannot be changed through the API, keep the backstop: stop Inventor's command and open AWB Hole, as `StopInventorsMiniToolbarHole` does. Report that the ring still shows Inventor's icon. Use task 141's check-and-restore approach if the ring is a `NameValueMap` like the mini toolbar.

## Verification

### What was found

- **The event.** `UserInputEvents.OnRadialMarkingMenu` exists in `Autodesk.Inventor.Interop.dll` (Inventor 2026), checked by reflection: delegate `UserInputEventsSink_OnRadialMarkingMenuEventHandler(ObjectsEnumerator SelectedEntities, SelectionDeviceEnum SelectionDevice, RadialMarkingMenu RadialMenu, NameValueMap AdditionalInfo)`. The API reference (`Autodesk.Inventor.Interop.xml`) says it fires before the ring is shown, that `RadialMenu` is the ring Inventor would show, and that "any changes you make are transient and will only be used for that display of the marking menu". Spike 081 had already hooked it.
- **The ring is not a `NameValueMap`.** `RadialMarkingMenu` has eight slots, `NorthControl`, `NortheastControl`, `EastControl`, `SoutheastControl`, `SouthControl`, `SouthwestControl`, `WestControl` and `NorthwestControl`, each an `object` get/set ("the command to display"), plus `InternalName`, `Name`, `BuiltIn`, `Clear`, `CreateRadialSubMenu`. So there is no insert or remove: AWB Hole's `ButtonDefinition` is set into the slot. Task 141's insert-and-undo rules do not apply; the same idea does: read the ring before and after, and set back any slot that changed if the swap did not hold.
- **The ring's Hole.** Read from the running Inventor 2026 through COM, read-only (`Environment.GetRadialMarkingMenu()` on the default, no-selection ring): the Part environment's ring is north `PartFilletCmd`, north-east `PartExtrudeCmd`, east `PartRevolveCmd`, **south-east `PartHoleCmd`**, south `AppSketch2DWrapperCmd`, south-west `PartWorkPlaneCmd`, west `AppUndoCmd`, north-west `AppMeasureDistanceCmd`, each a `ButtonDefinition`. The sheet metal environment's default ring has no Hole (`PartSwitchRepresentationCmd`, `SheetMetalFaceCmd`, `SheetMetalStylesCmd`, `SheetMetalFlangeCmd`, `AppSketch2DWrapperCmd`, `SheetMetalCutCmd`, `AppUndoCmd`, `AppMeasureDistanceCmd`). Rings for a selection (a face, a sketch) were not read; the per-ring log line covers them.
- **The backstop did not catch it.** The ring's Hole is `PartHoleCmd`, the ribbon's Hole, which the backstop (task 133) did not stop: it stopped only `PartDMHoleCmd`. So the ring opened Inventor's Hole.

### What was built

- `HoleEditRedirect` subscribes to `OnRadialMarkingMenu` (and unsubscribes in `Dispose`). While AWB Hole has Hole and a part is being edited, it reads the eight slots, finds Inventor's Hole (`RingHoleSlots`: `PartHoleCmd` or `PartDMHoleCmd`), sets AWB Hole's `ButtonDefinition` there, and reads the ring back. If it is not Inventor's ring with only that slot changed (`RingSwapHeld`), it logs a warning, sets each changed slot back to the object Inventor gave (`RingSlotsToRestore`, each on its own), and logs the ring again. A ring with an unreadable slot name is not touched.
- Each ring is logged at Info once per session, by ring name and part kind (`RingLogKey`), before the swap and after it: "Hole takeover: the marking menu ring 'None' in a part, before the swap: north PartFilletCmd, ..., south-east PartHoleCmd, ...".
- **The Flat Pattern tab keeps Inventor's Hole (review round 2).** Its Hole is `PartHoleCmd` too and stays visible, and in a flat pattern `ActiveEditDocument` is still the sheet metal part. So the adapter reads whether a flat pattern is being edited: `Application.ActiveEditObject is FlatPattern`, falling back to the active environment's `InternalName` containing "FlatPattern" (`HoleTakeoverRules.InFlatPattern`). Core's `TakesOverHoleEntries(owner, isPart, inFlatPattern)` gates the ring swap and the mini toolbar `PartDMHoleCmd` swap, and `StopsInventorHole(command, owner, isPart, inFlatPattern, askedFor)` is false in a flat pattern. The Edit Feature redirect (task 092) is unchanged.
- **Run request choice.** A `PartHoleCmd` start the add-in asked for is let through and **keeps** the request, so a `PartDMHoleCmd` that Inventor's Hole may start next is let through too. Whether that chain happens is not known; clearing the request would risk stopping Inventor Hole half-way. Any other command start clears the request, so it cannot let a later unrelated Hole through.
- **Backstop widened.** `StopsInventorMiniToolbarHole` is now `StopsInventorHole` and stops `PartHoleCmd` as well as `PartDMHoleCmd`, under the same rule (AWB Hole has Hole, a part is being edited, the add-in did not ask). The split button's "Inventor Hole" runs `PartHoleCmd` after asking, so `InventorHoleRunRequest.StartIsAskedFor("PartHoleCmd")` now returns true while a request is pending, and still keeps the request for a `PartDMHoleCmd` that may follow (its two tests changed from false to true). The log line names where the start came from (`InventorHoleStoppedMessage`).

### Verification

- **Ran:**
  - Round 1: `dotnet build InventorAddin.slnx -p:DeployToInventor=false --no-incremental` (Inventor running): 0 errors; Core tests 4447 of 4447.
  - Round 2, after the flat pattern fix: `dotnet build InventorAddin.slnx --no-incremental` on Windows, Inventor closed, so **deployed** to `%APPDATA%\Autodesk\Inventor 2026\Addins`: build succeeded, 0 errors. Warnings only in files this task did not touch (CS8600 in four commands, xUnit2012 in `HoleDiagramTests.cs`).
  - `dotnet test tests/InventorAddin.Core.Tests`: 4461 passed, 0 failed of 4461. `HoleTakeoverRulesTests`: 150 of 150.
- **Not compiled (changed under `src/InventorAddin`):** none. `src/InventorAddin/Holes/HoleEditRedirect.cs` and `src/InventorAddin/Commands/HoleDropdownCommands.cs` (comment only) were built and deployed on Windows.
- **Inventor API members not confirmed:** none by name. `UserInputEvents.OnRadialMarkingMenu` and its delegate, `RadialMarkingMenu.InternalName` and the eight `...Control` properties (get and set) were checked by reflection in the interop and compiled. Behaviour not yet seen in Inventor: (1) whether setting a slot to our `ButtonDefinition` shows AWB Hole's icon in the ring and runs `Awb_AwbHole`; (2) whether the change is transient as documented, so the ring is Inventor's again when "Use Inventor's Hole" is on; (3) whether a slot's object passes `is ButtonDefinition` (the interop's `ButtonDefinitionObject` declares no `ControlDefinition` base, so the reader tries `ButtonDefinition`, then `ControlDefinition`, then `RadialMarkingMenu`; if none match, the slot reads as unreadable and the ring is left alone, which the before line would show as "(unnamed)"); (4) whether stopping `PartHoleCmd` with `StopActiveCommand` once its event has returned works as it does for `PartDMHoleCmd`; (5) whether `Application.ActiveEditObject` is the `FlatPattern` while the flat pattern is edited (`ActiveEditObject` and `FlatPattern` are in the interop and compile; the flat pattern environment's internal name was not read, so the "FlatPattern" name fallback is a guess, used only if `ActiveEditObject` cannot be read).
- **Manual checklist for Inventor:** close Inventor, build `InventorAddin.slnx` with deploy, start Inventor. Logs are in `%APPDATA%\AwbAddin\awbaddin.log`.
  1. In a part, with AWB Hole active, right-click in the graphics window with nothing selected. The ring's south-east slot shows AWB Hole's icon and opens AWB Hole. The log has "Hole takeover: the marking menu ring '...' in a part, before the swap: ..., south-east PartHoleCmd, ..." and "..., after the swap: ..., south-east Awb_AwbHole, ...". Send both lines.
  2. Right-click again: the ring is the same and nothing more is logged for it. Right-click with a face selected, and with a sketch selected: each different ring is logged once.
  3. If the ring still shows Inventor's Hole: the log has a warning "replacing Hole on the marking menu ring should have left ... but left ..." and a line "after Inventor's ring was put back: ...". Pressing Hole in the ring must then open AWB Hole (the backstop), with "Hole takeover: Inventor's Hole (PartHoleCmd: the marking menu, a shortcut or the command search) started; AWB Hole opens instead." Send those lines. If a before line shows "(unnamed)" slots, send it: the slot type check (item 3 under "not confirmed") did not match.
  4. With "Use Inventor's Hole" on: the ring's south-east is Inventor's Hole and opens Inventor's Hole; no ring line is logged.
  5. Split button > Inventor Hole, with AWB Hole active: Inventor's Hole dialog opens and stays open (the backstop lets the asked-for start through).
  6. In a sheet metal part and in a part edited in place: as step 1. In sheet metal the default ring has no Hole, so expect only a before line ("in a sheet metal part") and no change.
  7. The mini toolbar on a sketch point (task 141) and Edit Feature (task 092) work as before.
  8. Sheet metal part, Flat Pattern tab: Hole opens Inventor's Hole and stays open; the right-click ring shows Inventor's Hole; nothing is logged as stopped. A sketch point's mini toolbar there shows Inventor's Hole.

## Follow-ups

- Sub-menus in the ring (a `RadialMarkingMenu` in a slot) are logged as "submenu <name>" but not searched for a Hole. Inventor's default part ring has none; a user's customised ring might.
- The ring has an overflow linear menu below it, which arrives through `OnLinearMarkingMenu`. If Hole appears there (not in the default part menu as far as known), it is not swapped; the backstop still catches it.
- Once Aidan's log confirms the swap holds, the before and after ring lines can drop to Debug.
