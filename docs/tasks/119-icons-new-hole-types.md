# 119: Icons for Simple, Taper Tapped and Spotface

- **Milestone:** M4 part 1
- **Feature spec:** `docs/features/09-awb-hole.md` ("The window"); `docs/features/08-window-style.md` (icons)
- **Status:** todo
- **Depends on:** 104
- **Needs:** **Python with Pillow** (as task 087)
- **Parallel-safe with:** 110 to 113, 117

## Goal

The window's icon buttons for the two new types and the new seat (Aidan, 2026-10-24), in the style of task 104's icons and each clearly different from the others (Aidan, 2026-10-23): Simple unlike Clearance, Taper Tapped showing tapered threads unlike Tapped, Spotface a shallow wide seat unlike Counterbore.

## Scope

- `tools/icons/render.py` and the icon sources it draws from; the rendered light and dark PNGs; the `HoleIcon.{name}.Material` keys in `Styles.xaml` for the names task 112 adds to `HoleIcons`.
- If task 112 is not done, use the names `TypeSimple`, `TypeTaperTapped` and `SeatSpotface` and tell the parent, so 112 uses them.

## Acceptance criteria

- [ ] Three new icons in both themes, at the size the window uses, visibly different from every existing hole icon.
- [ ] Every `HoleIcons.All` name has a `Styles.xaml` key (the `TODO.md` follow-up from task 104, folded in here as a Core test).

## Verification

Filled in by the implementer.

## Follow-ups
