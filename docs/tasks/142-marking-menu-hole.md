# 142: The marking menu's Hole follows the takeover

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The command")
- **Status:** todo
- **Depends on:** 141
- **Needs:** windows build
- **Parallel-safe with:** 140, 143 (not 141: same file)

## Goal

A part's right-click marking menu (the radial ring) has a Hole button with Inventor's icon. It opens Inventor's Hole even while AWB Hole has taken over. It must open AWB Hole by the same rule as the mini toolbar's Hole, and Inventor's Hole when "Use Inventor's Hole" is on (Aidan, 2026-11-07, `DECISIONS.md`).

## Scope

- `src/InventorAddin/Holes/HoleEditRedirect.cs`:
  - Handle the radial marking menu. `UserInputEvents.OnRadialMarkingMenu` is the likely event, but it is unconfirmed. Check it in the interop by reflection, as task 122 did, and name what you found.
  - Log the ring's commands once per session at Info, as task 133 logs the sheet metal toolbar.
  - Put AWB Hole, on our `ButtonDefinition`, in the place of the ring's Hole entry, by the same rule as the mini toolbar.
  - If the ring's Hole is a command the takeover's backstop does not catch yet, add it to the backstop rule.
  - Unsubscribe in `Dispose`, as for the other events.
- `src/InventorAddin.Core/Holes/HoleTakeoverRules.cs` and its tests: the command name and any rule.
- `docs/features/09-awb-hole.md`: one line.

Out of scope:

- The linear marking menu's Edit Feature (task 092), already handled.

## Acceptance criteria

- [ ] Core tests for the rule.
- [ ] The ring's entries are logged once per session.
- [ ] Built on Windows, 0 errors; Core tests pass.

## Notes for the implementer

If the radial menu cannot be changed through the API, keep the backstop: stop Inventor's command and open AWB Hole, as `StopInventorsMiniToolbarHole` does. Report that the ring still shows Inventor's icon. Use task 141's check-and-restore approach if the ring is a `NameValueMap` like the mini toolbar.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:**
  1. In a part, with AWB Hole active, right-click in the graphics window. The ring's Hole shows AWB Hole and opens it. The log lists the ring's commands.
  2. With "Use Inventor's Hole" on, the ring's Hole opens Inventor's Hole.
  3. In a sheet metal part and in a part edited in place: as step 1.

## Follow-ups
