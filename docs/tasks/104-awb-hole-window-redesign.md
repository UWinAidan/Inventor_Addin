# 104: AWB Hole window redesign

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window"); `docs/features/08-window-style.md`
- **Status:** todo (waits for Aidan's approval of the mockup, task 102)
- **Depends on:** 102 (approved), 101
- **Needs:** windows build
- **Parallel-safe with:** 105

## Goal

Build the AWB Hole window as Aidan approved it in task 102's mockup: compact, icon buttons for Type, Seat, Termination and Drill point, and a live diagram of the hole with its dimensions.

## Scope

- Written in full once Aidan has approved the mockup, with his comments. Expected: Core, a hole diagram model (outline, dimension lines, labels and values from the view-model, in diagram units), with tests; the add-in, the window's XAML, the icon buttons and their icons, and a control that draws the diagram model in the shared theme's brushes.

## Notes from earlier tasks

- Task 098's Tolerance item template hides the row name in the closed box with a `ComboBoxItem` ancestor binding that fails on purpose (a harmless binding trace). A `SelectionBoxItemTemplate` or a template selector in `UI/Theme` would be cleaner; check the closed box is wide enough for `G7  +0.016 / +0.004`.

## Acceptance criteria

- To be written from the approved mockup.

## Verification

Filled in by the implementer.

## Follow-ups
