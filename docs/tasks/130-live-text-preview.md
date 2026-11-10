# 130: Text boxes update the preview as you type

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window")
- **Status:** done
- **Depends on:** 124, 126
- **Needs:** windows build (Core first)
- **Parallel-safe with:** none

## Goal

Aidan, 2026-10-25: every text box in the AWB Hole window updates the preview live, about **300 ms after the last keystroke**, without clicking elsewhere. While the text is incomplete or invalid, the last good preview stays and the message shows under the box. This replaces the task 090 Default that depth boxes update on losing focus or Enter.

## Scope

- Core: a small debounce helper with an injectable clock or scheduler (so it is testable), and the view-model rule: text that parses (number or Inventor expression, checked as today) is applied after the pause; text that does not leaves the model and the preview at the last good value and sets the box's message; Enter and losing focus apply at once. Tests with a fake clock.
- `src/InventorAddin/UI/AwbHoleWindow.xaml(.cs)`: bindings update on each keystroke (`UpdateSourceTrigger=PropertyChanged`) through the debounce, for every text box (depths, seat boxes, and the Diameter and Custom boxes when 112 and 113 add them; their tasks follow this rule). Expression checks that call Inventor (`HoleExpressionChecker`) run only after the pause, on the UI thread.
- OK uses the latest text: pressing OK within the 300 ms applies it first.

## Acceptance criteria

- [ ] Typing `12` into Depth changes the preview about 300 ms after the last key, with no focus change.
- [ ] Typing `d0+` shows the message under the box and keeps the last preview; finishing it as `d0+2` updates it.
- [ ] OK straight after typing uses the typed value.
- [x] Core tests for the debounce; `dotnet test tests/InventorAddin.Core.Tests` passes; the solution builds on Windows.

## Verification

Filled in by the implementer.

- **Ran (Aidan's PC, Windows, Inventor 2026 installed, Inventor not running):**
  - `dotnet build src/InventorAddin.Core`: build succeeded, 0 warnings.
  - `dotnet test tests/InventorAddin.Core.Tests`: 4178 tests. One full run passed 4178, failed 0. Three other full runs each had 1 to 3 failures, all in `Numbering` tests that touch the file system and fail after about 3 s (`SaveAsCopyCreatorTests`, `SaveAsReplacerTests`, `RegisterStoreTests`, `RenamePlanTests`, `NewPartCreatorTests`). It was a different set each run. The `Numbering` tests alone pass 862 of 862, and this task does not touch them (see Follow-ups). 29 tests were added: `DebouncerTests` (8) and `AwbHoleLiveTypingViewModelTests` (21). No existing test changed.
  - `dotnet build InventorAddin.slnx`: build succeeded, with the deploy step, because Inventor was closed. The only warnings are the existing CS8600 ones in `CreatePrintCommand`, `NewPartCommand`, `SaveAsCopyCommand` and `SaveAsReplaceCommand`.
- **Not compiled (changed under `src/InventorAddin`):** none. `UI/AwbHoleWindow.xaml`, `UI/AwbHoleWindow.xaml.cs` and the new `UI/DispatcherDebounceTimer.cs` compiled in the Windows build above. Nothing has been run in Inventor.
- **Inventor API members not confirmed:** none. No Inventor API member is used. Expression checks still go through the existing `IHoleExpressionChecker`, now only after the pause or on a commit, on the UI thread, because the `DispatcherTimer` runs on the window's dispatcher.
- **WPF members used for the first time in this add-in:** `System.Windows.Threading.DispatcherTimer` (the constructor taking `DispatcherPriority` and `Dispatcher`, plus `Interval`, `Tick`, `Start` and `Stop`), `UIElement.AddHandler` and `RemoveHandler` with `UIElement.LostFocusEvent`, and `UpdateSourceTrigger=PropertyChanged`. All are standard WPF and compiled. Their runtime behaviour is untested.
- **What changed:**
  - Core: `ViewModels/Debouncer.cs` adds `IDebounceTimer`, a one-shot timer interface, and `Debouncer`, which has `Post`, `Flush`, `Cancel` and `TypingPause` = 300 ms. `AwbHoleViewModel` gains `UseTypingTimer`, `CommitTyping`, `DiscardTyping` and `HasPendingTyping`. With a timer set, every text property that goes through `SetText` keeps its text as a draft. This covers Depth, Thread and Drill depth, the six seat boxes, Diameter, and the Custom Upper and Lower. The getter returns the draft, so the box is not rewritten under the caret: `1.` stays `1.`. The draft is applied after the pause by the existing rule. A number, or an expression Inventor reads, sets the field and the preview. Other text keeps the last good value and shows its message. A commit applies at once and lets the box show its settled text. Enter, leaving a box, the angle dropdown closing, OK, and any change made elsewhere in the window all commit. OK commits first and then needs `CanOk`. It is enabled while typed text is waiting, so OK pressed straight after typing works even when the previous text was invalid. Cancel drops waiting text. Without a timer, every set applies at once as before, so all earlier tests are unchanged.
  - Add-in: `DispatcherDebounceTimer` wraps a `DispatcherTimer` and stops it on its first tick. `AwbHoleWindow` gives the view-model this timer and commits on any TextBox `LostFocus`, which includes the text box inside the angle dropdown. Enter commits before it checks OK. `DropDownClosed` on the angle box commits, so picking 60 or 90 still applies at once. Waiting text is dropped when the window closes. The seven bound boxes now use `UpdateSourceTrigger=PropertyChanged`.
  - Spec 09: no change. "Text boxes update the preview as you type" already says this. The blank-box Default below is listed for the parent to add to the spec and to `DECISIONS.md`.
- **Manual checklist for Inventor:** open a part of your own with an unconsumed sketch containing a few points. Start AWB Hole on the 3D Model tab, or press H.
  1. Blind clearance hole (Termination Distance). Click in Depth, select its text and type `12`. Without clicking anywhere, about a third of a second after the last key the red preview and the diagram show 12.
  2. In Depth type `d0+`, using a parameter your part has. The message appears under the box after the pause, OK is greyed, and the preview stays at the last good depth. Add `2`: the message goes, `= ... mm` appears beside the box, and the preview updates.
  3. Type `1.` and pause: the box still shows `1.` and the preview is at 1. Finish `1.5` and press Tab: the box shows `1.5`.
  4. Clear Depth and pause: the preview does not move and there is no message. Press Tab: the blank is applied, as leaving a box empty did before this task.
  5. The same as steps 1 and 2 in a seat box (Counterbore, Cbore depth with `d0+` then `d0+2`), and in Thread depth and Drill depth on a blind tapped hole.
  6. Countersink seat: type `82` in Csink angle and wait. The preview follows with no focus change. Open the list and click 60: it applies at once. Note that typing `6` may autocomplete to `60` (task 127), and if you pause there, 60 is previewed before you type the next digit.
  7. Type `14` in Depth and press Enter straight away: the hole is made at 14. Do it again with the OK button clicked straight after typing: the hole has the typed value.
  8. Type a depth and press Esc within a third of a second: the window closes and the part is as it was, with no extra preview flash. Do it again clicking Cancel with the mouse: the part is as it was (a preview flash before it closes is expected, see Follow-ups).
  10. OK with a blank box waiting and no centres picked: note whether a clickable OK that does nothing is confusing.
  9. Type in a box, then click a sketch point in the model before the pause ends: the centre is picked and the depth still applies after the pause.

## Defaults

Behaviour picked to keep work moving. The parent should log these in `DECISIONS.md` as Default; Aidan may overturn them.

- **A blank box waits for a commit.** Enter, leaving the box or OK apply it; the pause does not. Clearing a box to retype it therefore does not move the preview, and does not put back a table value, as a blank thread or drill depth would. Spec 09's "While the text is incomplete" is read as including an empty box.
- **OK is enabled while typed text waits for the pause**, even when the previous text was invalid. Pressing it applies the text first and makes the hole only if the window is then valid. Otherwise OK would stay greyed for up to 300 ms after a fix.
- **A change made anywhere else in the window applies the typed text first.** This covers Units, Type, Seat, Size, Flip and so on, so the change works on what is typed. Picking a hole centre does not apply it, because centres do not go through the cascade. The typed text applies after the pause as usual.
- **The pause applies the typed text but leaves the box as typed.** `1.` is not tidied to `1` until a commit, so the caret and the text are never changed under the user's fingers.

## Follow-ups

- Tasks 114 and 115, binding the Simple Diameter box and the Custom Upper and Lower boxes: bind them with `UpdateSourceTrigger=PropertyChanged`. Their view-model text properties (`DiameterText`, `ToleranceUpperText`, `ToleranceLowerText`) already go through `SetText`, so the debounce, the keep-last-good rule and the commits apply with no Core change. The window's `LostFocus` handler and Enter handling cover any TextBox. `AwbHoleLiveTypingViewModelTests.TheDiameterAndCustomLimitBoxes_FollowTheSameRule` already covers them. The same applies to the Spotface diameter and depth boxes when they are bound.
- `Numbering` tests that touch the file system fail intermittently in a full parallel run on Aidan's PC. Each failure takes about 3 s, and a different set fails each run. They pass alone. This is probably a timeout in a folder probe under load, or the repository sitting in OneDrive. It is not related to this task.
