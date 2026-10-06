# 018: Read the snapshot from Inventor and write property changes

- **Milestone:** M1
- **Feature spec:** `docs/features/07-part-properties.md` (Add-in side; Notes for planning: unconfirmed API behaviour)
- **Status:** todo
- **Depends on:** 014
- **Needs:** cloud (writes add-in code that is not compiled here; Aidan builds it)
- **Parallel-safe with:** 011, 012, 015, 016, 017

## Goal

The add-in can turn an open document into a `PartPropertiesSnapshot` and apply a list of `PropertyWrite`s to it. These are the two Inventor-facing halves the Part Properties command (019) plugs together.

## Scope

- `src/InventorAddin/Extraction/PartPropertiesReader.cs` (new)
- `src/InventorAddin/Extraction/PropertyWriter.cs` (new)
- `src/InventorAddin/Extraction/PropertyReader.cs` (set name constants point at `PropertySetNames` in Core; nothing else changes)

Out of scope:

- Transactions. The writer runs inside whatever transaction its caller started (019 uses `RunInTransaction` from 017).
- Any decision about what to write. That is the plan from 014.
- The existing `PropertyReader.Set`. Leave it; it has no callers that matter here. Note in Follow-ups whether it can go.

## Acceptance criteria

Reader: `PartPropertiesReader.Read(Document doc)` returns a `PartPropertiesSnapshot`

- [ ] `FullFileName` from `doc.FullFileName` (empty when never saved); `DocumentKind` from `DocumentKinds.Of(doc)`
- [ ] Part Number, Description, Designer, and the custom Part Type, Detailer and Finish through `PropertyReader.GetString` with the locations from `PartPropertyStorage` (no repeated property names in the add-in)
- [ ] Cost through `PropertyReader.Get`, converted to `decimal?` with `Convert.ToDecimal(value, CultureInfo.InvariantCulture)` inside `ComSafe.Get`; anything unreadable becomes null
- [ ] Material: for a part (`DocumentKind.IsPart()`), `((PartDocument)doc).ActiveMaterial.DisplayName` as in `PartExtractor`; empty for assemblies
- [ ] Weight: the component definition's `MassProperties.Mass` formatted by `UnitsFormatter.Mass`, as `PartExtractor.ReadMass` and `AssemblyExtractor` do. Any failure gives null (the window shows a dash). Reuse the existing code paths rather than copying them
- [ ] `EditBlock`: `ReadOnlyFile` when the file exists and has the read-only attribute (reuse the check in `ModelExtractor`; make it `internal static` if needed); otherwise `NotModifiable` when `doc.IsModifiable` is false; otherwise `None`. If `IsModifiable` cannot be read, treat it as modifiable and log `WARN`
- [ ] Every getter is wrapped in `ComSafe.Get`; the reader never throws for a missing property

Writer: `PropertyWriter.Apply(Document doc, IReadOnlyList<PropertyWrite> writes)`

- [ ] Set on a standard property: `doc.PropertySets[set][name].Value = value`. Text writes a `string`; Currency writes the `decimal`
- [ ] Set on a custom property: update `Value` when the property exists, otherwise `PropertySet.Add(value, name)`
- [ ] Remove on a custom property: `Property.Delete()` when it exists; nothing when it does not
- [ ] Remove on a standard property throws `ArgumentException` (the plan never produces one)
- [ ] Any COM failure is rethrown wrapped in `InvalidOperationException` whose message names the property, so the status line can say which write failed. No `ComSafe` around writes: a failed write must abort the caller's transaction
- [ ] `PropertyReader`'s four set name constants equal the `PropertySetNames` constants (assign them, do not retype the strings)
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes; the verification section lists every add-in file as not compiled

## Notes for the implementer

- Block namespace, explicit usings, `IOPath` alias if you need `System.IO.Path` (see `CLAUDE.md`).
- Members to confirm against the Inventor API reference, because nothing in the repo uses them yet: `Document.IsModifiable`, `Property.Delete`, `PropertySet.Add` (already used in `PropertyReader.Set`, so its argument order there is the reference). List each one you could not confirm.
- Whether the Cost property accepts a `decimal` (COM currency) or needs a `double` is not settled. Write `decimal` and put the question on the checklist; do not add fallbacks that guess.
- Reading mass can be slow on a large assembly. Read it once per command run; note in Follow-ups if it looks like it needs to be optional.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** none here; 019 carries the checks, because nothing calls the reader or writer until then.

## Follow-ups

Things noticed but not done.
