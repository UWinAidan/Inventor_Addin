# 103: Spike: editing a part in place, for AWB Hole and every AWB command

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command"); every spec whose command is on a model ribbon (02, 03, 07)
- **Status:** todo
- **Depends on:** 090
- **Needs:** **windows** (spike, with Aidan in Inventor)
- **Parallel-safe with:** 107, 108 (other spikes, same session with Aidan, separate branches)

## Goal

Aidan overturned the default that refuses AWB Hole while a part is edited in place inside an assembly, and on 2026-10-24 made in-place editing the **top priority, for AWB Hole and every other AWB tool**: he double-clicks parts in assemblies all the time. Before building it, test the Inventor behaviour it needs with Aidan, as spike 081 did, and check every command in place. Nothing from this spike is merged; task 105 (AWB Hole) and task 109 (every other command) are written from its results.

## Questions: AWB Hole

1. Which document the command sees (`Application.ActiveEditDocument` is the part, `ActiveDocument` the assembly), and that hole features made on the part's `ComponentDefinition` appear in the part and in the assembly view.
2. Which ribbon Inventor shows while a part is edited in place (the Part ribbon, or the Assembly ribbon with part tabs), so whether the AWB Hole button on the AWB Addin tab and the takeover button in the 3D Model tab's Modify panel (task 091) are there, and whether **H** reaches AWB Hole.
3. Transactions: on which document the session's outer and inner transactions must start (the part, or the assembly) so that one Undo in the assembly window takes the whole hole away, and Cancel leaves both as they were.
4. Picking: what `InteractionEvents` `OnSelect` returns for a sketch point clicked in context (a `SketchPointProxy` in the assembly's space?), and how to get the part's `SketchPoint` (`NativeObject`); whether the selection filter and `ResetSelections` work the same.
5. Highlighting: whether a `HighlightSet` must be made on the assembly with proxies, or works on the part, to show picked centres blue in context.
6. Showing the sketch: `PlanarSketch.Visible` and `Shared` (task 094) in context.
7. Colours, tolerance, attributes: unchanged, since they are on the part; confirm.
8. The client graphics preview (spike 107) in context: whether graphics made on the part's `ClientGraphicsCollection` show in the assembly window, or must be made on the assembly with the occurrence's transform.
9. Edit Feature and double-click (task 092) in context: which events fire (`OnLinearMarkingMenu`, `OnContextMenu`, `UserInputEvents.OnDoubleClick`) and what they carry (a `HoleFeatureProxy`?), so the redirect can open AWB Hole on the part's hole.
10. Ending the edit (Return), closing the assembly, or switching to another document while the window is open: what events tell us, so the session cancels safely.

## Questions: every other command (the audit)

For each command, run it while a part is edited in place, and where it makes sense while a sub-assembly is edited in place, and record what happens: which document it acts on, whether it should act on the part or the assembly, whether its window opens and fills from the right document, and whether one Undo in the assembly window reverts it. What each uses today (from the code, 2026-10-24):

| Command | Document today | Known or likely issue |
|---|---|---|
| New Part | `ActiveDocument` folder | Probably fine: the new part goes in the assembly's folder; confirm it opens on its own and the in-place edit is not disturbed |
| Save As Copy | `ActiveDocument` | Copies the assembly while a part is edited in place; Aidan decides whether it should copy the part |
| Save As and Replace | the assembly's selection (task 060) | Selection inside an in-place edit; replacing the part being edited |
| Part Properties | `ActiveEditDocument`, transaction on `ActiveDocument` | Undo in place was never checked (`TODO.md`, open since task 006) |
| Material and Finish | the part being edited (task 076 targeting) | Confirm the window and one Undo |
| Create Print | `ActiveDocument` | Prints the assembly while a part is edited in place (`TODO.md`, task 044 follow-up) |
| Mass Update | a folder | Probably not affected; confirm |
| Export Model Data | `ActiveEditDocument` | Confirm it exports the part |
| Settings, About | none | Confirm they open |
| AWB Hole, Edit Feature, double-click | refused, not redirected | Questions 1 to 10 above |

## How

- A spike branch `spike/103-in-place-edit`, off `claude/m4-hole-generator`, with a temporary build of AWB Hole that accepts an in-place part and logs every step and object type to `awbaddin.log`, and a log line at the start of every command naming `ActiveDocument`, `ActiveEditDocument` and the ribbon. Aidan runs it on a small assembly of his own parts and reports; the results go in this file.
- Rounds with Aidan, as spike 081: the AWB Hole questions first (they are the hardest), then the audit table, one command at a time.

## Results

Filled in after the rounds with Aidan.

## Recommendations

Filled in after the rounds with Aidan; task 105 (AWB Hole) and task 109 (every other command) are written from them. For each command in the audit: works as it is, or what to change, and the behaviour Aidan chose where there was a choice (log it in `DECISIONS.md` as Aidan's).
