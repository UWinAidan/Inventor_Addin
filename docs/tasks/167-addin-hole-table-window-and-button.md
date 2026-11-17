# 167: Add-in: the Hole Table Wizard window and its ribbon button

- **Milestone:** M4 part 2
- **Feature spec:** `docs/features/04-hole-table-wizard.md` ("Target dialog")
- **Status:** todo
- **Depends on:** 159, 163
- **Needs:** windows (add-in; Aidan checks in Inventor)
- **Parallel-safe with:** 164, 165

## Goal

A **Hole Table** button on the AWB Addin tab of the drawing ribbon opens the Hole Table Wizard window. The window is bound to task 159's view-model and task 163's source, in the shared window look.

## Scope

- **`UI/HoleTableWindow.xaml`.** Shown through `WindowHost`, with the shared style from `UI/Theme/`. No `UseLayoutRounding` (`CLAUDE.md`). Its layout follows the spec's "Target dialog":
  - **Header:** the drawing name, a Sheet list, and a View list.
  - **Create Tables tab:**
    - View, Analyze View and Create Tables;
    - the two lists, each row showing Tag, Description and the Split checkbox;
    - Swap and Delete between the lists;
    - Generate Precision Table and Generate Clearance Table.
  - **Edit Table tab** (task 162): the stored views, and NOTES per group.
  - **Bottom row:** Align, Renumber, Update, Cancel, a status line and a progress bar.
- **Command.** `Commands/HoleTableCommand.cs` (`Awb_HoleTable`), enabled only when a drawing is active. Its icon follows the task 151 icon set: a new 16 and 32 px icon in the same style, or the closest existing icon until Aidan picks one (Default; ask him).
- Button text and tooltip in our own words.
- Until tasks 164 to 166 land, the table commands report "not built yet" through the source.

## Acceptance criteria

- [ ] Builds on Windows. The XAML rules test passes.
- [ ] The manual checklist below passes.

## Notes for the implementer

- Follow an existing window for structure and binding, for example `AwbHoleWindow` or `MassUpdateWindow`, and `RibbonSetup` for the drawing ribbon.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. The button shows on the drawing ribbon only.
  2. The window opens at 100% and 150% display scale, in light and dark themes.
  3. Analyze View fills the lists.
  4. Swap, Delete, Split and Renumber change the tags.
  5. Cancel closes without changes.

## Follow-ups

Things noticed but not done.
