# 129: AWB Hole as a split button with Inventor Hole and the default toggle

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command")
- **Status:** done
- **Depends on:** 128
- **Needs:** windows build
- **Parallel-safe with:** none (same files as 120, 121, 128)

## Goal

Aidan, 2026-10-25: AWB Hole on the 3D Model and Sheet Metal tabs becomes a **split button**. Its dropdown has **Inventor Hole**, which runs Inventor's Hole once, and **Use Inventor's Hole by default**, a toggle that is the same setting as "Use Inventor's Hole command" in AWB Settings.

## Scope

- From task 128: the split button goes on the Sheet Metal panel `HoleCommandTakeover` finds as well as on the Modify panel, and stays covered by Core `AddinUi.ForeignButtons` so unload and load remove it.
- `src/InventorAddin/UI/RibbonSetup.cs`, `HoleCommandTakeover.cs`, a new command for Inventor Hole and one for the toggle in `src/InventorAddin/Commands/`.
- The split button (`CommandControls.AddSplitButton` or what the API reference gives; check the members), with AWB Hole as its main button. When the setting is on, the split button is hidden and Inventor's Hole shows, as today; Default: the toggle is then reached from AWB Settings or the AWB Addin tab's AWB Hole (which stays a plain button).
- **Inventor Hole** runs Inventor's Hole command once (`ControlDefinitions["PartHoleCmd"].Execute`, or the sheet metal one) without changing the setting. While it runs, the takeover must not redirect it to AWB Hole (task 092 stops Inventor's hole edit started other ways; a new Inventor hole from this button is allowed). Its icon is Inventor's Hole icon if it can be read, else ours (as task 092).
- **Use Inventor's Hole by default** shows its state (pressed or a check), saves the setting as Settings' Save does, and applies it at once. The Settings window shows the same value next time it opens.
- Core: any rule about which button shows for which setting goes in `HoleTakeoverRules` with tests.

## Acceptance criteria

- [ ] The split button shows on both tabs with the two dropdown items.
- [ ] Inventor Hole opens Inventor's Hole once; the next H opens AWB Hole.
- [ ] The toggle and the Settings switch always agree, and the toggle takes effect at once.
- [x] The solution builds on Windows; `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Filled in by the implementer.

- **Ran:** on Windows (Inventor 2026 installed, Inventor not running): `dotnet build src/InventorAddin.Core` (0 errors); `dotnet build InventorAddin.slnx -tl:off` (build succeeded, 0 errors; the 8 warnings are the existing CS8600 ones in CreatePrint, NewPart, SaveAsCopy and SaveAsReplace, none in files this task changed; the add-in was deployed); `dotnet test tests/InventorAddin.Core.Tests` (3793 passed, 0 failed, 0 skipped).
- **Not compiled (changed under `src/InventorAddin`):** none. `Commands/HoleDropdownCommands.cs` (new), `UI/HoleCommandTakeover.cs` and `UI/RibbonSetup.cs` all compiled on Windows against the Inventor 2026 interop. Nothing has run inside Inventor yet.
- **Inventor API members not confirmed:** every member was checked by reflection on `Autodesk.Inventor.Interop.dll` (2026): `CommandControls.AddSplitButton(ButtonDefinition DisplayedControl, ObjectCollection ButtonDefinitions, bool UseLargeIcon, bool ShowText, string TargetControlInternalName, bool InsertBeforeTargetControl)`, `ButtonDefinition.Pressed` (get and set), `ControlDefinition.Execute()`, `CommandControl.ControlDefinition`, `CommandControl.ChildControls`, `TransientObjects.CreateObjectCollection` and `ObjectCollection.Add` (as `HoleFeatureWriter`). Behaviour that only Inventor can show:
  - what `InternalName` and `ControlDefinition` Inventor reports for a split button. The add-in finds its split button by its name, its top command or any dropdown entry (`AddinUi.IsControlOf`), and logs what Inventor reported when it adds one ("split button added on panel ...; Inventor names it ... with top command ..."). A warning "it cannot be found again, so it may be added twice" in that line means none of the three matched.
  - whether the dropdown must list the top command too. The add-in lists AWB Hole, Inventor Hole, then the toggle, as Inventor's samples pass the displayed button in the collection.
  - how a pressed `ButtonDefinition` looks in a split button's dropdown (highlighted or checked). It is only visible while the setting is off, so it shows unpressed in practice.
- **Manual checklist for Inventor:**
  1. Open a part, 3D Model tab, Modify panel: AWB Hole is a large split button where Inventor's Hole was. Its dropdown has AWB Hole, Inventor Hole (with Inventor's Hole icon) and Use Inventor's Hole by default (Settings icon, not pressed). In `%APPDATA%\AwbAddin\awbaddin.log`, the line "split button added on panel id_PanelP_ModelModify" has no "cannot be found again".
  2. Pick Inventor Hole: Inventor's own Hole panel opens; make a hole and OK it. Then press H: the AWB Hole window opens. Click the split button's top half: AWB Hole opens.
  3. With the AWB Hole window open, pick Inventor Hole: the AWB Hole window closes as Cancel (the part is as it was) and Inventor's Hole opens.
  4. Pick Use Inventor's Hole by default: the split button goes, Inventor's Hole button is back, and H opens Inventor's Hole. Open AWB Settings: "Use Inventor's Hole command" is ticked. Untick it and Save: the split button is back with the toggle not pressed, and H opens AWB Hole.
  5. Open a sheet metal part, Sheet Metal tab: the same split button where Inventor's Hole was, with the same dropdown; repeat 2 and 4 there. The Flat Pattern tab still has Inventor's Hole.
  6. With the AWB Hole window closed, right-click a sketch point and pick Hole on the mini toolbar: AWB Hole opens. Then Inventor Hole from the split button, Cancel Inventor's Hole, and the mini toolbar's Hole again: AWB Hole opens (the request is not left over).
  7. Unload and load the add-in (Add-In Manager): one split button on each tab, not two; the log's unload line counts the split buttons removed.
  8. Read-only settings file (make `%APPDATA%\AwbAddin\settings.json` read-only), pick Use Inventor's Hole by default: a message says the setting was not changed, and nothing changes. Make the file writable again.

## Defaults taken (for the parent to log)

- The split button's dropdown lists AWB Hole first, then Inventor Hole, then the toggle (Inventor's split buttons list the top command too).
- The toggle's icon is the Settings icon; Inventor Hole's is Inventor's Hole icon, else AWB Hole's.
- If either dropdown command fails to register, or Inventor refuses the split button, AWB Hole is the plain button of task 091 there, with a warning in the log.
- Inventor Hole runs `PartHoleCmd` on the Sheet Metal tab too (the Sheet Metal tab's Hole button is `PartHoleCmd`, task 128), after its own event returns; an open AWB Hole window is cancelled by Inventor's Hole starting, as by any other command.
- A failed save of the toggled setting changes nothing and shows a message, as Settings' Save does.
- `InventorHoleRunRequest` needed no change: Inventor Hole runs `PartHoleCmd`, which already keeps the request (TODO follow-up from task 133). Inventor Hole calls `HoleEditRedirect.Active?.InventorHoleAsked()` just before it runs Inventor's Hole.

## Follow-ups

- The Settings note under "Use Inventor's Hole command" (`SettingsViewModel.UseInventorHoleCommandNoteText`) still says only "Takes effect when you save"; it could say the AWB Hole dropdown sets the same switch at once.
- If the log shows the split button "cannot be found again" (step 1), `AddinUi.IsControlOf` needs whatever name Inventor gives a split button.
