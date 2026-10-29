# 128: AWB Hole replaces Inventor's Hole on the Sheet Metal tab

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command")
- **Status:** done
- **Depends on:** 121
- **Needs:** windows build
- **Parallel-safe with:** none (same files as 120, 121, 129)

## Goal

Aidan, 2026-10-25: AWB Hole also replaces Inventor's Hole on the **Sheet Metal** tab, under the same "Use Inventor's Hole command" switch. The **Flat Pattern** tab keeps Inventor's. This replaces the Default that the Sheet Metal tab keeps Inventor's Hole.

## Scope

- `src/InventorAddin/UI/RibbonSetup.cs`, `HoleCommandTakeover.cs`: find the Sheet Metal tab's panel that holds Inventor's Hole (look it up at run time by the control's command, `PartHoleCmd` or the sheet metal one, rather than a guessed panel name; log what was found), put AWB Hole before it, large, and hide Inventor's button there, the same way task 091 does on the 3D Model tab. The switch shows and hides both. Add the new button to Core's `AddinUi.ForeignButtons` (task 121), or an unload leaves it behind.
- If the Sheet Metal tab uses a different command name for its Hole, H is still one binding: confirm in the log which command H reaches on a sheet metal part.
- AWB Hole itself already works on sheet metal parts (task 090 Default); confirm a hole made on a sheet metal face is correct.

## Acceptance criteria

- [ ] On a sheet metal part, the Sheet Metal tab shows AWB Hole where Inventor's Hole was; H opens AWB Hole.
- [ ] Flat Pattern still shows Inventor's Hole.
- [ ] The switch brings Inventor's Hole back on both tabs.
- [ ] The solution builds on Windows; `dotnet test tests/InventorAddin.Core.Tests` passes.

## Verification

Implemented on Aidan's Windows PC (Inventor 2026 installed, Inventor open, so no deploy).

What changed:

- Core `SheetMetalHolePanel` (new, `Holes/SheetMetalHolePanel.cs`): given every panel of the Part ribbon as read from
  Inventor (tab id and name, panel id, top-level control names), `Find` picks the panel on the Sheet Metal tab
  (`HoleTakeoverRules.SheetMetalTabId`, `id_TabSheetMetal`) that holds `PartHoleCmd`, preferring spike 081's
  `id_PanelP_SheetMetalModify` when several do. The Flat Pattern and 3D Model tabs are never chosen. Its log line lists
  every Part ribbon panel with a Hole command (any control with "Hole" in its name, ours and our tab left out), so the
  real tab, panel and command names show in the log. It is a warning when no panel is found, or when the Sheet Metal tab
  has a Hole command other than `PartHoleCmd` (not taken over; H is moved from `PartHoleCmd` only).
- Core `HoleTakeoverRules`: `SheetMetalTabId`, `SheetMetalPanelId`; `AppliedMessage` takes `sheetMetalTab` and then
  names "the 3D Model and Sheet Metal tabs".
- Core `AddinUi.ForeignButtons`: a second entry, AWB Hole on the Part ribbon's Sheet Metal tab. `ForeignButton.PanelId`
  is now nullable; null means every panel on the tab, since the panel is found at run time.
- Core `SettingsViewModel.UseInventorHoleCommandNoteText`: says "the 3D Model and Sheet Metal tabs".
- `HoleCommandTakeover`: at every apply (startup, settings saved, shortcut reset, unload) the Part ribbon is read and the
  Sheet Metal panel found with `SheetMetalHolePanel`; the lookup is logged when its result differs from the last one.
  Taking over adds AWB Hole large before `PartHoleCmd` on that panel and hides Inventor's, as on the Modify panel
  (`ShowOnPanel`); giving back shows Inventor's and hides ours on both panels (`GiveBackOnPanel`). Reading the ribbon
  cannot cost the 3D Model tab its takeover: each tab and each panel is read in its own try/catch, and one that throws
  (a contextual tab, another add-in's tab, a panel not filled yet) is skipped and named in the lookup's log line, which
  is then a warning. If the lookup still throws (the Part ribbon or its tab list cannot be read), it is logged as a
  warning, "the Sheet Metal tab could not be read; only the 3D Model tab is taken over", and treated as no panel; giving
  back does the same. No Sheet Metal panel is a warning only. Only an exception while changing a panel that was found
  (`ShowOnPanel`) fails the whole take-over and gives Hole back on both tabs, as task 091 does (spec: a take-over that
  fails gives Inventor's Hole back). H stays one binding: the shortcut is on the `PartHoleCmd` and AWB Hole
  definitions, not on the controls.
- `RibbonSetup.RemoveForeignButtons`: the tab is found by enumerating the ribbon's tabs, not by indexing, so a tab id
  that is not on the ribbon (the Sheet Metal one is unconfirmed) is reported through the new
  `AddinUiRemovalReport.TabNotFound` as one info line, "tab id_TabSheetMetal was not found on the Part ribbon, so there
  is nothing to remove there. If AWB Hole shows on that tab, its id is wrong.", not as a failure. An entry with no panel
  id searches every panel on its tab.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false -tl:off`: succeeded, 0 errors, 8 warnings, all
  CS8600 in files this task did not touch (CreatePrintCommand, NewPartCommand, SaveAsCopyCommand, SaveAsReplaceCommand).
  `dotnet test tests/InventorAddin.Core.Tests`: 3762 passed, 0 failed (after the review fixes; 3761 passed on two
  runs before them). One earlier run had 1 failure,
  `RenamePlanTests.ListFilesToSearch_AFolderThatDoesNotAnswer_IsSkippedAfterTheProbeTimeout`, a timing test unrelated to
  this task; it passed on both later runs (see Follow-ups).
- **Not compiled (changed under `src/InventorAddin`):** none. Built on Windows; not run in Inventor.
- **Inventor API members not confirmed:** every member used was checked against `Autodesk.Inventor.Interop.dll` by
  reflection (`RibbonTab.InternalName`, `DisplayName`, `RibbonPanels`; `RibbonPanel.InternalName`, `CommandControls`;
  `CommandControls.AddButton(ButtonDefinition, bool, bool, string, bool)`; `CommandControl.InternalName`, `Visible`,
  `Delete`). Internal names not confirmed offline (running Inventor could not be reached from this shell): the Sheet
  Metal tab `id_TabSheetMetal` (no spike saw it; the log lists the real one), `PartHoleCmd` on that tab and the panel
  `id_PanelP_SheetMetalModify` (from spike 081 section 3), and the Flat Pattern tab's name (only used in tests; Flat
  Pattern is left alone because it is not the Sheet Metal tab). Also not known: whether the Part ribbon's Sheet Metal
  panels can be read at startup with no sheet metal part open (the 3D Model tab could, task 091).
- **Manual checklist for Inventor:**
  1. Start Inventor with the add-in. In `%APPDATA%\AwbAddin\awbaddin.log`, find the line "Hole takeover: Hole commands
     on the Part ribbon: ...". It should list the 3D Model tab's Modify panel, the Sheet Metal tab's panel and the Flat
     Pattern tab's panel, each with `PartHoleCmd`, and say "On the Sheet Metal tab, AWB Hole goes before PartHoleCmd on
     panel ...". Then "AWB Hole has the Hole button on the 3D Model and Sheet Metal tabs and the H shortcut". If it is a
     warning instead ("No panel on the Sheet Metal tab ..." or "also has ..."), send the line: it names the real tab,
     panel and command.
  2. Open a sheet metal part. Sheet Metal tab: AWB Hole (large) where Hole was, Inventor's Hole hidden. Click it and
     make a hole on a flange: the hole is right, OK keeps it, one Undo removes it.
  3. On the sheet metal part press H: AWB Hole opens; the log shows "Command Awb_AwbHole (AWB Hole) started.".
  4. 3D Model tab of the same part: AWB Hole in Hole's place, as before.
  5. Create or go to the flat pattern. Flat Pattern tab: Inventor's Hole is there, no AWB Hole.
  6. Settings: tick "Use Inventor's Hole command" (the note now names both tabs), Save. Inventor's Hole is back on the
     Sheet Metal and 3D Model tabs, AWB Hole hidden there; H opens Inventor's Hole. Untick, Save: AWB Hole is back on both.
  7. Add-In Manager: unload the add-in. The log's "Ribbon (add-in unloading): removed ..." counts 2 buttons on Inventor's
     panels (3D Model and Sheet Metal), Inventor's Hole shows on both tabs. Load it again: AWB Hole is back on both tabs,
     and the loading line says nothing was left. If the log instead has "tab id_TabSheetMetal was not found on the Part
     ribbon, so there is nothing to remove there", the Sheet Metal tab's real id is different: the count is then 1, and
     an AWB Hole left on the Sheet Metal tab after unloading means the id must be corrected (step 1's line names the
     real one). It is not a failure and is not logged as a warning.
  8. Tools > Customize > Keyboard > Reset All, close the dialog: H still opens AWB Hole on a sheet metal part (task 120).

## Follow-ups

- If checklist step 1 shows the Sheet Metal tab's panels cannot be read at startup (warning "No panel on the Sheet Metal
  tab" though the tab has Hole once a sheet metal part is open), the takeover needs re-applying when a sheet metal part
  is first activated.
- `RenamePlanTests.ListFilesToSearch_AFolderThatDoesNotAnswer_IsSkippedAfterTheProbeTimeout` failed once and passed on
  the next two runs: it looks timing-sensitive (probe timeout).
- Task 129 (split button) must put its split button on the Sheet Metal panel `HoleCommandTakeover` finds, and keep
  `AddinUi.ForeignButtons`' Sheet Metal entry (any panel on the tab) matching whatever it adds there.
