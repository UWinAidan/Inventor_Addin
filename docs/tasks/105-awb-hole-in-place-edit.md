# 105: AWB Hole in a part edited in place

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command")
- **Status:** todo
- **Depends on:** 103, 092
- **Needs:** windows build
- **Parallel-safe with:** 109 (disjoint files), 134

## Goal

AWB Hole works on a part edited in place inside an assembly, as it does in the part's own window: for new holes, and for editing holes through Edit Feature and double-click (task 092). Aidan raised it at the 090 check and made it the top priority on 2026-10-24. Spike 103 settled how; this task builds what its Recommendations say. It also makes the window cancel when the user switches to another document, in place or not. Spike 103 found that the window stays open today.

## Scope

- `src/InventorAddin/Commands/AwbHoleCommand.cs`
  - The command runs on `InventorHost.ActiveEditDocument` when it is a part, and the in-place refusal goes. In a part's own window, `ActiveEditDocument` is the part, so nothing changes there.
  - The class remarks lose the "refused" text.
- `src/InventorAddin/Holes/HoleSession.cs`
  - **Proxies.** Items that are a `SketchPointProxy` or a `PlanarSketchProxy` become their `NativeObject` before they are used. This covers `OnSelect` and the default picks, which read the part's `SelectSet` and the active document's (in place, the top-level document holds the selection).
  - **Ending on a switch.** The session subscribes to `ApplicationEvents.OnDeactivateDocument`. At `kBefore`, for the document that was `ActiveDocument` when the session began (the assembly in place, the part in its own window), it ends as Cancel does: transaction aborted, graphics and highlight cleared, window closed. It is unsubscribed wherever `OnCloseDocument` is.
- `src/InventorAddin/Holes/HoleEditRedirect.cs`
  - `CurrentPart()` returns `ActiveEditDocument` when it is a part.
  - `HoleOf` takes a `FaceProxy` through `CreatedByFeature`. A `HoleFeatureProxy`, whether given directly or from a face, becomes its `NativeObject`.
  - `EditFeaturePressed` reads the selection from the part's `SelectSet`, then the active document's, then falls back to the offered hole, as today.
  - The class remarks lose "only in a part open in the window".
- `src/InventorAddin.Core/Holes/HoleSessionRules.cs` (or `HoleEditRules.cs`)
  - One rule, with tests: whether a document deactivating ends the session, given the session's starting `ActiveDocument` name, the deactivating document's name and whether the timing is before. The paths are compared ignoring case.
- `tests/InventorAddin.Core.Tests/Holes/…` for that rule.
- `docs/features/09-awb-hole.md`
  - "The command": the in-place line says how it works now. The session ends when the user switches to another document (Default, from spike 103).
  - Open question 10 is marked answered.

Out of scope:

- The client graphics preview (task 110).
  - If 110 is done first, 105 checks that it draws in place.
  - If 105 is done first, 110 draws on the part's `ComponentDefinition.ClientGraphicsCollection` only, with no occurrence transform. Spike 103 question 8 found that this shows in place, and 110's brief says so.
  - Neither task needs code for the other.
- The mini toolbar's Hole entry (task 133).
- Every other command in place (task 109).

## Acceptance criteria

- [ ] With a part edited in place, the AWB Addin tab's button, the 3D Model takeover button and **H** each open the AWB Hole window on that part.
- [ ] Picking, unpicking, the blue highlight, the shown sketch and the preview work in place. OK makes the hole on the part, and one Undo in the assembly window removes it. Cancel leaves both documents as they were.
- [ ] Double-click on a hole face, and Edit Feature on a hole (browser and mini toolbar), open the window on that hole in place. Faces of other features are left to Inventor.
- [ ] Return, closing the assembly, and switching to another document with the window open each end the session as Cancel does. Switching away from a part in its own window does too.
- [ ] Nothing changes in a part's own window apart from the switch rule.
- [ ] Every proxy-to-native conversion goes through `ComSafe`. A proxy that gives no native object is logged and skipped, not thrown.
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes, and the solution builds on Windows.

## Notes for the implementer

- **Spike 103's Results are the reference** (`docs/tasks/103-spike-in-place-edit.md`). The spike code is on the local branch `spike/103-in-place-edit`. `git diff claude/m4-hole-generator spike/103-in-place-edit -- src/InventorAddin/Holes` shows the proxy handling that worked: `NativeOf` in `HoleSession` and `HoleOf` in `HoleEditRedirect`. Do not copy `Spike103.cs` or its options. The defaults it proved are the design:
  - transactions on the part;
  - native objects;
  - the highlight set on the part.
- Transactions already start on `(_Document)_doc`, the part, and that is right in place. One Undo in the assembly window reverts it (spike 103 question 3). Do not move them to the assembly.
- **Confirmed members:**
  - `Application.ActiveEditDocument`;
  - `SketchPointProxy.NativeObject`, `PlanarSketchProxy.NativeObject`, `HoleFeatureProxy.NativeObject`;
  - `Face.CreatedByFeature` on a `FaceProxy`;
  - `ApplicationEvents.OnDeactivateDocument(_Document, EventTimingEnum, NameValueMap, out HandlingCodeEnum)`.

  These were all used in spike 103 and built on Windows.
- The occurrence name can differ from the file name. Never look the occurrence up by file name. This task needs no occurrence at all.
- Edit the spec text only where listed. Log the switch rule in `Follow-ups` if anything about it surprised you. The parent logs it in `DECISIONS.md`.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Open an assembly and double-click one of your parts to edit it in place. Press **H**: the AWB Hole window opens on that part, and the default centres are picked and blue.
  2. Click centres to unpick and pick them. The highlight and preview follow. Press OK: the hole is in the part. Press Undo once in the assembly window: the hole is gone.
  3. Start AWB Hole again from the AWB Addin tab, then press Cancel: nothing changed in the part or the assembly.
  4. Double-click an AWB hole's face in place: the window opens on that hole. Change its size, press OK, then Undo once: it reverts. Right-click the hole in the browser and choose Edit Feature: our window opens.
  5. With the window open in place, press Return: the window closes and nothing is changed. Repeat, then close the assembly: the window closes. Repeat, then switch to another open document: the window closes and nothing is changed.
  6. In a part's own window, open AWB Hole and switch to another document: the window closes. Everything else works as before.

## Follow-ups
