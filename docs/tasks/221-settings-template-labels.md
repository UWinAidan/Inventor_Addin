# 221: Settings template labels

- **Milestone:** M5 (from Aidan's task 187 check, 2026-11-22)
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Settings)
- **Status:** todo
- **Depends on:** 186
- **Needs:** windows build
- **Parallel-safe with:** any

## Goal

The Settings labels "Metric print template" and "Imperial print template" are cut off; shorten them to "Metric template" and "Imperial template" (Aidan, 2026-11-22).

## Scope

- `src/InventorAddin/UI/SettingsWindow.xaml` labels, with access keys that clash with nothing (task 186's follow-up: Alt+I was shared).
- `SettingsViewModel` field names used in messages, and their tests.

## Acceptance criteria

- [ ] Both labels show in full at 150% display scale.
- [ ] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors and the Core tests pass.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):**
- **Inventor API members not confirmed:**
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. …

## Follow-ups

Things noticed but not done.
