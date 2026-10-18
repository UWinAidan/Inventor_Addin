# 103: Spike: AWB Hole in a part edited in place in an assembly

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command")
- **Status:** todo
- **Depends on:** 090
- **Needs:** **windows** (spike, with Aidan in Inventor)
- **Parallel-safe with:** 102

## Goal

Aidan overturned the default that refuses AWB Hole while a part is edited in place inside an assembly: he uses in-place editing a lot. Before building it, test the Inventor behaviour it needs with Aidan, as spike 081 did. Nothing from this spike is merged; task 105 builds on its results.

## Questions

1. Which document the command sees (`Application.ActiveEditDocument` is the part, `ActiveDocument` the assembly), and that hole features made on the part's `ComponentDefinition` appear in the part and in the assembly view.
2. Transactions: on which document the session's outer and inner transactions must start (the part, or the assembly) so that one Undo in the assembly window takes the whole hole away, and Cancel leaves both as they were.
3. Picking: what `InteractionEvents` `OnSelect` returns for a sketch point clicked in context (a `SketchPointProxy` in the assembly's space?), and how to get the part's `SketchPoint` (`NativeObject`); whether the selection filter and `ResetSelections` work the same.
4. Highlighting: whether a `HighlightSet` must be made on the assembly with proxies, or works on the part, to show picked centres blue in context.
5. Showing the sketch: `PlanarSketch.Visible` and `Shared` (task 094) in context.
6. Colours, tolerance, attributes: unchanged, since they are on the part; confirm.
7. Ending the edit (Return), closing the assembly, or switching to another document while the window is open: what events tell us, so the session cancels safely.

## How

- A spike branch `spike/103-in-place-edit`, off `claude/m4-hole-generator`, with a temporary build of AWB Hole that accepts an in-place part and logs every step and object type to `awbaddin.log`. Aidan runs it on a small assembly and reports; the results go in this file.

## Results

Filled in after the rounds with Aidan.

## Recommendations

Filled in after the rounds with Aidan; task 105 is written from them.
