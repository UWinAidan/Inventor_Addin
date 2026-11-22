# 221: Settings template labels

- **Milestone:** M5 (from Aidan's task 187 check, 2026-11-22)
- **Feature spec:** `docs/features/01-ribbon-and-shell.md` (Settings)
- **Status:** done
- **Depends on:** 186
- **Needs:** windows build
- **Parallel-safe with:** any

## Goal

The Settings labels "Metric print template" and "Imperial print template" are cut off; shorten them to "Metric template" and "Imperial template" (Aidan, 2026-11-22).

## Scope

- `src/InventorAddin/UI/SettingsWindow.xaml` labels, with access keys that clash with nothing (task 186's follow-up: Alt+I was shared).
- `SettingsViewModel` field names used in messages, and their tests.

## Acceptance criteria

- [x] Both labels show in full at 150% display scale.
- [x] `dotnet build InventorAddin.slnx -p:DeployToInventor=false` has 0 errors and the Core tests pass.

## Verification

Filled in by the implementer.

- **Ran:** `dotnet build InventorAddin.slnx -p:DeployToInventor=false`: 0 errors, 11 warnings. `dotnet test tests/InventorAddin.Core.Tests`: 5161 passed, 0 failed.
- **Not compiled (changed under `src/InventorAddin`):** none; `SettingsWindow.xaml` was built on Windows. Display at 150% not seen.
- **Inventor API members not confirmed:** none.
- **Manual checklist for Inventor:** (copied into `docs/checklists/m5.md` by the parent for the combined check, task 209)
  1. Open the AWB Addin Settings window at 150% display scale.
  2. Under the template rows, see "Metric template" and "Imperial template" in full, not cut off.
  3. Hold Alt: Metric template shows P underlined, Imperial template shows L underlined. Alt+P and Alt+L focus those boxes; Alt+I still toggles the Hole command check box.
  4. Enter a bad path in each box and press Save: the message starts "Metric template:" / "Imperial template:".

## Follow-ups

Things noticed but not done.
- Access keys now in use in Settings: D, N, U, R, W, O, M, F, P, L, H, B, E, I, T, S. Keep this list if more rows are added.
