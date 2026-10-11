# 052: Spike: rename references, copies and the selection in a sub-assembly (Windows)

- **Milestone:** M2 (fix run after run 2)
- **Feature spec:** `docs/features/02-part-creation-and-numbering.md`
- **Status:** done
- **Depends on:** none
- **Needs:** windows (run by a local session on Aidan's PC, with Aidan for the browser steps)
- **Parallel-safe with:** every task

## Goal

Find out, before changing code, why Aidan's run 2 checks found bugs 1, 3, 4 and 6, using Inventor itself rather than guesses:

1. Why did Rename leave a drawing pointing at the deleted old file?
2. Can an occurrence be renamed through the API, and does the name stick?
3. Do Save As Copy's and Save As and Replace's copies carry their new Part Number?
4. Where is the selection when a part inside a sub-assembly is picked in the browser?
5. Can a drawing be saved under a new name, and can its old file then be deleted?

## Scope

A throwaway console app, kept out of the repo (it lived in the session's scratch folder), built against the add-in's `Autodesk.Inventor.Interop.dll` like spike 032's. Mode `auto` starts its own invisible Inventor and makes its own files in a scratch folder. Mode `selection` attaches to Aidan's running Inventor and only reads `ActiveDocument`, `ActiveEditDocument` and their `SelectSet`.

## Results

Run on 2026-10-11 in Inventor 2026.2 on Aidan's PC. Out of process over COM, as spike 032.

### 1. Rename and references (bug 1): the cause

The add-in's Rename runs `SaveAs(new, false)` on the part, then replaces the old reference in each file found, then saves "the open documents the SaveAs repointed" **only when they are `Dirty`**.

- After `SaveAs(new, false)` on the part, every **open** document that uses it (a visible drawing and a visible assembly were tested) refers to the new path in memory **but has `Dirty = False`**. Spike 032 reported the assembly as `Dirty`; on this run neither was.
- **`Save()` on a document with `Dirty = False` writes nothing.** A drawing repointed in memory, saved with `Save()` and reopened, still referred to the old file.
- So today's Rename leaves every open drawing and assembly that uses the part pointing at the old file on disk, then deletes the old file. Reproduced exactly (T0): the drawing and the assembly reopen with the reference missing. This is what Aidan saw: his print was open when he renamed.
- **`Document.Dirty` can be set.** `doc.Dirty = true` then `Save()` writes the new reference; both files reopened clean.
- A **closed** file opened invisibly after the `SaveAs` still refers to the old path, and `FileDescriptor.ReplaceReference(new)` followed by `Save()` fixes it, as spike 032 found. This held for a second rename in the same session too.
- Fix: after `SaveAs`, every document that refers to the file, open or closed, gets its reference replaced where it still names the old path, then `Dirty = true`, then `Save()`.

### 2. Occurrence names (bug 3)

- Inventor does **not** rename occurrences when the file is renamed: they stay `P:1`, `P:2`.
- `ComponentOccurrence.Name` has a setter. Setting it in an open assembly, and in a closed assembly opened invisibly, then saving, stuck: reopened as `P2:1`, `P2:2`.
- Renaming an occurrence set `Dirty = True` in one run and not in another; set `Dirty = true` before saving regardless.

### 3. Copies (bug 6)

- `SaveAs(copy, true)` keeps the original open under its own name. `Documents.Open(copy, true)` returns the copy (its own `FullFileName`), even though the copy has the **same `InternalName`** as the open original. Part Number written on it and saved stays on the copy; the original keeps its own.
- The Save As and Replace path (component loaded by an open assembly, `SaveAs(copy, true)`, `Documents.Open(copy, false)`, write, `Save`, `Close(true)`, `Replace(copy, false)`) gives the copy its own number on disk, and the occurrence is renamed to the copy's file name by `Replace`.
- Aidan's own copies from the run 2 checks, read back from disk without changes: `AWB-10010,BRACKET_COPY.ipt` has `AWB-10010`, `…COPY2` `AWB-10011`, `…COPY3` `AWB-10012`, `AWB-10015,BRACKET_replaced.ipt` `AWB-10015`. **Bug 6 did not reproduce.** Aidan was not sure where he saw it ("file name and maybe Part Properties"). The fix run adds a read-back check and a log line, and the name-first change fixes the pre-filled name.
- `PropertySets["Inventor Summary Information"]["Author"].Value = …` writes and reads back after saving (for task 055).

### 4. The selection inside a sub-assembly (bug 4)

With Aidan's `ASSY_TOP` holding `ASSY_SUB` holding `PIN`:

- **Picked in the browser, not editing in place:** `ActiveDocument` and `ActiveEditDocument` are both `ASSY_TOP`. `SelectSet.Count = 1`, the item is a `ComponentOccurrenceProxy` (`Type = kComponentOccurrenceProxyObject`) and `is ComponentOccurrence` is true; its `ParentOccurrence` is `ASSY_SUB:1`. Clicking Save As and Replace then opened the dialog. This case works today.
- **With `ASSY_SUB` edited in place** (double-clicked in the browser) and `PIN` picked: `ActiveEditDocument` is `ASSY_SUB`, and **its `SelectSet.Count = 0`**. The selection is in `ActiveDocument.SelectSet` (`ASSY_TOP`'s): one `ComponentOccurrenceProxy`, `PIN:1` under `ASSY_SUB:1`.
- `DocumentEditCommand` hands commands `ActiveEditDocument`, so Save As and Replace looks in the empty set and says "Select one component". Fix: read the selection from `ActiveDocument`.

### 5. Renaming a drawing (bug 2)

- A closed drawing opened invisibly: `SaveAs(newName.dwg, false)`, `Close(true)`, delete the old file: works, and the new drawing's reference is good.
- An open drawing: `SaveAs(newName.dwg, false)` works, but **the old `.dwg` stays locked** ("being used by another process") until the document is closed. After `Close(true)` the old file deletes. So an open drawing has to be closed after its `SaveAs` before the old file can go; reopen it to leave Aidan's window as it was.

### Members confirmed to compile and work

`Document.Dirty` (get and set), `Document.Save`, `Document.SaveAs(string, bool)`, `Document.Close(bool)`, `Document.InternalName`, `Document.SelectSet`, `Application.ActiveDocument`, `Application.ActiveEditDocument`, `SelectSet.Count`, `SelectSet[int]`, `ComponentOccurrence.Name` (get and set), `ComponentOccurrence.ParentOccurrence`, `ComponentOccurrence.Replace(string, bool)`, `ComponentOccurrenceProxy`, `File.ReferencedFileDescriptors`, `FileDescriptor.FullFileName`, `FileDescriptor.ReplaceReference`, `FileDescriptor.ReferenceMissing`, the `Author` summary property.

## Follow-ups

- Spike 032's statement that `SaveAs` makes an open assembly `Dirty` did not hold on this run; spec 02 now says it does not.
