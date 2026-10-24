# 109: Every AWB command works in a part edited in place

- **Milestone:** M4 part 1
- **Feature spec:** specs 02, 03, 07 (each command's own section); `docs/features/09-awb-hole.md` ("The command")
- **Status:** todo (written in full from spike 103's audit)
- **Depends on:** 103
- **Needs:** windows build
- **Parallel-safe with:** 105 (only if spike 103 shows they touch disjoint files)

## Goal

Aidan, 2026-10-24: editing a part in place must work for every AWB tool, not only AWB Hole. Spike 103 runs every command in place with Aidan; this task fixes what it found, so each command acts on the right document, its window fills from it, and one Undo in the assembly window reverts it.

## Scope

- Written in full from spike 103's Recommendations, one section per command that needs a change. Expected candidates, from the code on 2026-10-24: Create Print (prints the assembly while a part is edited in place; task 044 follow-up in `TODO.md`, folded in here), Save As Copy (copies the assembly), Part Properties (Undo in place never checked; open since task 006). The ones the spike shows already work are listed in Verification as checked, not changed.
- Where a command must choose between the part being edited and the assembly, that choice is a Core rule beside the existing `MaterialFinishTargeting`, with tests.
- If the fixes are large, split this task by command when it is written; each part keeps its own commit.

## Acceptance criteria

- To be written from spike 103.

## Verification

Filled in by the implementer.

## Follow-ups
