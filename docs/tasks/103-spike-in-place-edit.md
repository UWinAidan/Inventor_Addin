# 103: Spike: editing a part in place, for AWB Hole and every AWB command

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command"); every spec whose command is on a model ribbon (02, 03, 07)
- **Status:** done (rounds 1 and 2 with Aidan, 2026-10-25)
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

Two rounds with Aidan on 2026-10-25, on branch `spike/103-in-place-edit` (round 1 `313e7cf`, round 2 `2e4e650`; local only, never merged). Test assembly: `ASSY_GEARBOX` with the part `BRACKET_V5` edited in place. Options were read from `%APPDATA%\AwbAddin\spike103.txt`; everything below ran on the defaults (transaction on the part, proxies converted to native objects, highlight set on the part). Every step was logged as `SPIKE 103:` in `awbaddin.log`.

**AWB Hole**

1. **Documents.** In place, `ActiveDocument` is the assembly and `ActiveEditDocument` is the part; the environment is `PMxPartEnvironment`. Hole features made on the part's `ComponentDefinition` appear in the part and in the assembly view. Pass.
2. **Ribbon.** Inventor shows the Part ribbon. The AWB Hole button on the AWB Addin tab, the takeover button in the 3D Model tab's Modify panel, and **H** all start AWB Hole. Pass.
3. **Transactions.** The outer, step and preview-paint transactions all start on the part (`(_Document)part`). OK makes the hole, and one Undo in the assembly window removes it. Cancel leaves the part and the assembly as they were. Pass.
4. **Picking.** In context, `OnSelect` returns a `SketchPointProxy` whose `Parent` is a `PlanarSketchProxy`. `NativeObject` gives the part's `SketchPoint` and `PlanarSketch`, and the session works on those. The selection filter and `ResetSelections` behave as they do in the part's own window. Pass. When the part is open in its own window, `OnSelect` returns a plain `SketchPoint`, so the conversion is needed only in place. The default picks read `SelectSet` from both the part and the active document; both were empty in every run.
5. **Highlighting.** A `HighlightSet` made on the part, with the native sketch points, shows blue in context. A set on the assembly is not needed. Pass.
6. **Showing the sketch.** `PlanarSketch.Visible` and `Shared` (task 094) work in context. The log shows `Visible True, Shared False`, then `Shared True`. Pass.
7. **Colours, tolerance, attributes.** These are on the part and are unchanged. Pass.
8. **Client graphics (round 2).** One-centimetre crosses were drawn at each centre in two places: green on the part's `ComponentDefinition.ClientGraphicsCollection`, and yellow on the assembly's with `GraphicsNode.Transformation = ActiveOccurrence.Transformation`. Both land exactly on the hole centres, follow picks, survive rotate and zoom, and clear on Cancel. Pass. Graphics on the part are enough, and they need no transform. `LineGraphics` has no `Color`: colour goes through `GraphicsDataSets.CreateColorSet` with `ColorBinding = kOverallColor`.
9. **Edit Feature and double-click (task 092).** A double-click or the mini toolbar on a hole face arrives as a `FaceProxy`. `CreatedByFeature` gives a `HoleFeatureProxy`, and its `NativeObject` is the part's `HoleFeature`, which opens the right hole. Pass. A face of another feature gives that feature's proxy, for example `ExtrudeFeatureProxy`, and the redirect leaves it alone. A click on the occurrence in the assembly, not in place, arrives as a `ComponentOccurrence`.
10. **Ways out with the window open (round 2).**
    - **Return** runs `AppReturnPreviousCmd`, and Inventor terminates the session's `InteractionEvents`. The session's existing "ended by Inventor" path aborts the outer transaction and closes the window. Then `AssemblyFinishEditCmd` runs, and `OnEnvironmentChange` reports `PMxPartEnvironment kTerminateEnvironmentState`. Pass.
    - **Closing the assembly** closes the window. Pass.
    - **Switching to another document leaves the window open. Fail**: it should cancel, as Inventor's own commands do. The switch fires `ApplicationEvents.OnDeactivateDocument` (`kBefore`) for the assembly, then `OnEnvironmentChange PMxPartEnvironment kSuspendEnvironmentState`. The session's `InteractionEvents` are not terminated at that point.

**The mini toolbar's Hole entry (round 1 open item, round 2).** A left click on a sketch point opens a mini toolbar with `PartDMHoleCmd, SketchEditSketchCtxCmd, AssemblyVisibilityCtxCmd`. Its Hole button is `PartDMHoleCmd`, not the ribbon's `PartHoleCmd`, so the takeover (task 091) does not cover it, and clicking it opens Inventor's Hole. **Fail**, in place and, Aidan reports, in a part's own window too. Round 1's unexplained `PartDMHoleCmd` start at 09:25:17 was this button.

**Other observations**

- An occurrence's name can differ from its file. `BRACKET_V4,AWB-10017:1` is the occurrence of `BRACKET_V5,AWB-10022.ipt`, so the occurrence must never be found by file name. `ActiveOccurrence`, `ContainingOccurrence` and `NativeObject` are the way.
- Painting the preview colour took 38 to 100 ms per change in place (339 ms the first time). Task 110 replaces the paint with client graphics.

**The audit**

Aidan reports that every other AWB command works on the part when it is edited in place (Pass). The log has a line for each command started, and it shows these runs in place (active document the assembly, edit document the part):

| Command | Seen in place in the log | Result |
|---|---|---|
| AWB Hole | yes, rounds 1 and 2 | as above |
| Material and Finish | yes, 09:58:16 | acts on the part (task 075 targeting); Pass |
| Part Properties | yes, 09:58:38, 10:02:04, 10:02:25 | acts on the part; Pass. One Undo in place is still not recorded (open since task 006) |
| About | yes, with no document | opens |
| New Part, Save As Copy, Save As and Replace, Create Print, Mass Update, Export Model Data, Settings | **no**: no start line for these on 2026-10-25 | Aidan's Pass is not backed by the log. From the code: Create Print and Save As Copy use `ActiveDocument`, so in place they would act on the **assembly**; New Part uses the assembly's folder; Export Model Data uses `ActiveEditDocument`; Save As and Replace reads the top-level assembly's selection by design (task 060) |
| Any command in a sub-assembly edited in place | **no** | not run |

**Rename (reported in round 2, not part of the spike).** Aidan saw `BRACKET_V5,AWB-10022.ipt` still shown as `BRACKET_V4,AWB-10017:1` in `ASSY_GEARBOX` after Rename, with the assembly open and with it closed. The log gives the cause:

- Rename does rename occurrences. On 2026-10-12 it renamed 2 when `BRACKET_V3` became `BRACKET_V4,AWB-10017`.
- On 2026-10-17, Save As and Replace replaced `BRACKET_V4,AWB-10017.ipt` with its copy `BRACKET_V5,AWB-10022.ipt`. Inventor's `ComponentOccurrence.Replace` keeps the occurrence's name, so the occurrence stayed `BRACKET_V4,AWB-10017:1`.
- From then on, the occurrence is not named after the file it points at. Rename renames only occurrences named after the old file (Aidan, 2026-10-11), so today's renames (`BRACKET_V5` to `BRACKET_V1` to `BRACKET_V2`) each logged "0 occurrences renamed".
- The gap is in Save As and Replace, not in Rename. Task 134 fixes it.

## Recommendations

**Task 105 (AWB Hole in place).**

- **Documents.** The command, the launcher, and the edit redirect use `ActiveEditDocument` when it is a part. The refusal goes.
- **Transactions.** Every session transaction starts on the part. This is the same as in a part's own window, so there is no branch.
- **Proxies.** `OnSelect` items and default picks that are `SketchPointProxy` or `PlanarSketchProxy` become their `NativeObject`. Edit Feature and double-click items that are `FaceProxy` or `HoleFeatureProxy` go through `CreatedByFeature`, then `NativeObject`.
- **Highlight.** The highlight set stays on the part.
- **Ending the session.** The session also ends when the document that was active at its start is deactivated (`OnDeactivateDocument`, `kBefore`): the assembly in place, the part in its own window. It ends the way Cancel does. Return and closing the assembly need nothing new.

**Task 110 (preview with client graphics).** Draw on the part's `ComponentDefinition.ClientGraphicsCollection` only, with no occurrence transform. It shows in place as well as in the part's own window. Written into 110's notes.

**Task 133 (new).** The takeover also covers the mini toolbar's Hole entry. In `OnContextualMiniToolbar`, when AWB Hole owns the Hole command, `Awb_AwbHole` is inserted where `PartDMHoleCmd` is and Inventor's entry is removed. This uses the same `Insert` then `Remove` method that task 092 uses for Edit Feature (spike 081), and it applies in a part's own window and in place. As a backstop, if `PartDMHoleCmd` still starts, it is stopped and AWB Hole is opened instead.

**Task 109 (every other command).** Every command acts on the document being edited (`ActiveEditDocument`): the part, or the sub-assembly, edited in place. This was Aidan's requirement on 2026-10-24, and he reports it as the behaviour he saw. The exceptions:
- Save As and Replace reads the top-level assembly's selection (task 060).
- New Part keeps the active document's folder (Default).
- Material and Finish keeps its own task 075 targeting.

What changes:
- Create Print prints the part being edited, not the assembly. This is a Default, and folds in the task 044 follow-up.
- Save As Copy copies the part being edited, not the assembly. This is a Default.
- The choice is a Core rule listing every command, with tests.
- The commands not seen in the log, and the sub-assembly case, are on 109's Inventor checklist, as is Part Properties' one Undo in place.

**Task 134 (new, from the Rename report).** Save As and Replace renames the occurrence it replaced when that occurrence was named after the original file: `OLD,AWB-10017:1` becomes `COPY,AWB-10022:1`, inside the same transaction, so one Undo reverts both. This is the rule Rename already uses (`RenamePlan.RenamedOccurrence`). Aidan's existing `BRACKET_V4,AWB-10017:1` was left stale by the old behaviour. Rename, as decided, leaves it alone, so it needs renaming by hand once, unless Aidan answers spec 02 question 27 the other way.
