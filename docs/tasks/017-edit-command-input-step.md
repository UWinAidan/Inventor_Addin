# 017: Editing command base: input step and per-edit transactions

- **Milestone:** M1
- **Feature spec:** `docs/features/07-part-properties.md` (Notes for planning: `DocumentEditCommand` needs a step before the transaction)
- **Status:** done (add-in code not compiled)
- **Depends on:** none
- **Needs:** cloud (writes add-in code that is not compiled here; Aidan builds it)
- **Parallel-safe with:** 011, 012, 013, 014, 015, 016, 018

## Goal

`DocumentEditCommand` can show a dialog before any transaction starts, and a window that applies edits several times (Apply, then OK) can wrap each apply in its own transaction. Closes the task 006 follow-up about the missing input hook.

## Scope

- `src/InventorAddin/Commands/DocumentEditCommand.cs`

Out of scope:

- Any command that uses the new members (019).
- Changing which document the transaction starts on. The in-place edit question stays a manual check in 019.

## Acceptance criteria

- [x] `protected virtual bool GatherInput(Document doc)`, default `true`, runs after the document-kind check and before any transaction. Returning `false` ends the command with no transaction and no message
- [x] `protected void RunInTransaction(Document doc, Action edit)` starts one transaction named `DisplayName` on `(_Document)doc`, runs `edit`, ends it; on an exception it aborts (logging an abort failure, as now) and rethrows. It is the only place in the class that starts a transaction
- [x] `protected virtual void Run(Document doc)`, whose default is `if (GatherInput(doc)) RunInTransaction(doc, () => Execute(doc));`. The sealed `Execute()` calls `Run(doc)` after the existing null and kind checks
- [x] A command that overrides only `Execute(Document)` behaves exactly as before: same checks, same messages, one transaction, abort on failure
- [x] An interactive command can override `Run` and call `RunInTransaction` once per apply. The XML comments say so and say that each call is one Undo step
- [x] `Execute(Document)` stays abstract. A command that overrides `Run` and never calls `Execute` implements it with a body that throws `NotSupportedException`, and the comment on `Run` says this
- [x] No other file changes. `dotnet test tests/InventorAddin.Core.Tests` still passes; the verification section lists the file as not compiled

## Notes for the implementer

- Keep `using` lines and block namespace style as they are. No new Inventor members: `TransactionManager.StartTransaction`, `Transaction.End` and `Transaction.Abort` are already used in this file.
- Keep the remarks about the transaction name being what shows in the Undo list.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet test tests/InventorAddin.Core.Tests` in the cloud: 214 passed, 0 failed, 0 skipped (this also builds `src/InventorAddin.Core`). The add-in project was not built (`CLAUDE_CODE_REMOTE=true`).
- **Not compiled (changed under `src/InventorAddin`):** `src/InventorAddin/Commands/DocumentEditCommand.cs`
- **Inventor API members not confirmed:** none. No new members; `TransactionManager.StartTransaction`, `Transaction.End` and `Transaction.Abort` are the calls the file already made, moved into `RunInTransaction`.
- **Manual checklist for Inventor:** none here; 019 is the first command that uses it and carries the checks.

## Follow-ups

Things noticed but not done.
