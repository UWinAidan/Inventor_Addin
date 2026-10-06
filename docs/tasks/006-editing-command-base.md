# 006: Editing command base

- **Milestone:** M0
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Shared plumbing: Command base)
- **Status:** todo
- **Depends on:** 005
- **Needs:** cloud (writes add-in code that is not compiled here; Aidan builds it)
- **Parallel-safe with:** 007

## Goal

Commands that change a document have a base class that runs them inside one Inventor transaction with a matching command type, so one undo reverts the whole command and a failure leaves the document as it was. M1 onwards builds on it.

## Scope

- `src/InventorAddin/Commands/AddinCommand.cs` (command type becomes a virtual property, default query-only)
- `src/InventorAddin/Commands/DocumentEditCommand.cs` (new)

Out of scope:

- Any concrete editing command. The first one arrives in M1.
- `RibbonSetup.cs` (task 007)
- Placeholder or test buttons on the ribbon

## Acceptance criteria

- [ ] `AddinCommand` has `protected virtual CommandTypesEnum CommandType => CommandTypesEnum.kQueryOnlyCmdType;` and `Register` passes it to `AddButtonDefinition` instead of the hard-coded value
- [ ] The two export commands are unchanged in behaviour (still query-only)
- [ ] `DocumentEditCommand : AddinCommand` is abstract. Subclasses implement `Execute(Document doc)` and override `CommandType` (abstract there, so each editing command must choose one)
- [ ] It resolves the target document with `InventorHost.ActiveEditDocument`. If there is none, it shows a short message and does not start a transaction
- [ ] It starts one transaction with `Application.TransactionManager.StartTransaction(doc, DisplayName)`, calls `Execute(doc)`, then `End()`. On any exception it calls `Abort()`, then rethrows so `AddinCommand`'s error reporting (task 005) logs and shows it
- [ ] An exception thrown by `Abort()` itself is logged and does not hide the original exception
- [ ] A subclass can narrow which document kinds it accepts (for example, a virtual `bool CanRunOn(DocumentKind kind)` using `DocumentKinds.Of`), with a short message when the active document is the wrong kind. The rule of which kinds a command accepts stays in the subclass, not in the base
- [ ] Code comments state that the transaction name is what appears in Inventor's Undo list
- [ ] The verification section lists both files as not compiled and lists `TransactionManager.StartTransaction`, `Transaction.End` and `Transaction.Abort` as unconfirmed unless checked against the Inventor API reference

## Notes for the implementer

- No existing code uses transactions, so there is no in-repo example. Check the members against the Inventor 2026 API reference (`TransactionManager` object). `StartTransaction(DocumentObject, DisplayName)` returns a `Transaction`.
- Command types from the API: `kShapeEditCmdType`, `kNonShapeEditCmdType`, `kFilePropertyEditCmdType`, `kEditMaskCmdType`, `kQueryOnlyCmdType`, `kUpdateWithReferencesCmdType`. Confirm the spelling against the reference; `kQueryOnlyCmdType` is already used in `AddinCommand.cs`.
- There is nothing to click in Inventor yet. The first M1 editing command is what proves this in Inventor; say so in the verification section.

## Verification

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. Build on Windows. The build succeeds.
  2. *Export Model Data* and *Export Libraries* still work.
  3. The transaction behaviour itself is checked by the first editing command in M1 (single undo entry named after the command; document unchanged after a failure).

## Follow-ups
