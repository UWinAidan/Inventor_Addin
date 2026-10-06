# 011: Agent definitions use the product name

- **Milestone:** M1
- **Feature spec:** `docs/features/07-part-properties.md` (Notes for planning: carry-over)
- **Status:** done
- **Depends on:** none
- **Needs:** cloud
- **Parallel-safe with:** 012, 013, 014, 015, 016, 017, 018

## Goal

The `implementer` and `reviewer` agent definitions call the product by its current name, so agents stop seeing the old "Inventor Workflow Tools" name (task 010 follow-up).

## Scope

- `.claude/agents/implementer.md`
- `.claude/agents/reviewer.md`

Out of scope:

- Any other wording or rule change in the agent files.
- Finished task files in `docs/tasks/`; they are history.

## Acceptance criteria

- [ ] Both files say "AWB Addin" where they said "Inventor Workflow Tools"
- [ ] A case-insensitive search of `.claude/` for `workflow ?tools` finds nothing
- [ ] The YAML front matter (`name`, `description`, `tools`, `disallowedTools`) is unchanged
- [ ] No other line changes

## Notes for the implementer

- Text only. No build is needed, but run `dotnet test tests/InventorAddin.Core.Tests` anyway to confirm nothing else moved.

## Verification

Filled in by the implementer.

- **Ran:** `grep -riE 'workflow ?tools' .claude` finds nothing. `git diff -- .claude` shows one line changed in each file, and the front matter is unchanged. `dotnet test tests/InventorAddin.Core.Tests`: 214 passed, 0 failed (the working tree also held uncommitted changes from tasks running at the same time).
- **Not compiled (changed under `src/InventorAddin`):** none
- **Inventor API members not confirmed:** none
- **Manual checklist for Inventor:** none

## Follow-ups

Things noticed but not done.

- The replacement reads "for the AWB Addin add-in", which says "add-in" twice. It could become "for AWB Addin, an Inventor add-in". That rewording was out of scope here.
