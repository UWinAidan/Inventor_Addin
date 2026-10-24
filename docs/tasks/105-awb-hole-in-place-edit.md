# 105: AWB Hole in a part edited in place

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command")
- **Status:** todo (written in full from spike 103's results)
- **Depends on:** 103, 092
- **Needs:** windows build
- **Parallel-safe with:** 109

## Goal

AWB Hole works on a part edited in place inside an assembly (Aidan, 090 check; top priority, Aidan 2026-10-24), for new holes and for editing holes (Edit Feature and double-click, task 092), as it does in the part's own window.

## Scope

- Written in full from spike 103's recommendations. Expected: the command and launcher accept the active edit document; `HoleSession` for transactions, picking, highlighting and the client graphics preview (task 110) in context; 092's edit routing in context (the `TODO.md` follow-up "the redirect should follow task 105 into parts edited in place" is folded in here); the window cancels safely when the in-place edit ends.
- Run before 110 if 110 is not done yet: the brief says which parts of the preview code each touches, so the second task adapts.

## Acceptance criteria

- To be written from spike 103.

## Verification

Filled in by the implementer.

## Follow-ups
