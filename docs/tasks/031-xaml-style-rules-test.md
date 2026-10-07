# 031: A test that keeps windows on the shared styles

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (How it is built: "No colours, font sizes or margins typed into a window's XAML. ... This is checked in review.")
- **Status:** todo
- **Depends on:** 026, 027, 028, 029, 030
- **Needs:** cloud
- **Parallel-safe with:** none

## Goal

The spec's rules for window XAML are checked by a test that runs in the cloud, so a later window that types its own colour or margin fails `dotnet test` instead of relying on a reviewer to spot it.

## Scope

- `tests/InventorAddin.Core.Tests/Styling/WindowXamlRulesTests.cs` (new)

Out of scope:

- Any change to the windows or styles. If the test finds a breach in the current files, fix it only if the fix is a one-line move to an existing shared style; otherwise record it in Follow-ups and exempt that one element in the test, with a comment naming the follow-up.
- The proposed reviewer checklist in spec 08 ("Also in this milestone"). Aidan has not agreed to it.

## Acceptance criteria

- [ ] The test finds the repository root by walking up from the test assembly's folder to the folder holding `InventorAddin.slnx`, and fails with a clear message if it cannot
- [ ] It loads every `*.xaml` under `src/InventorAddin/UI/` except `UI/Theme/` as XML (`System.Xml.Linq`; no WPF reference) and checks, for each element:
  - no attribute named `Background`, `Foreground`, `BorderBrush`, `Fill`, `Stroke`, `FontSize`, `FontWeight`, `FontFamily`, `Margin` or `Padding`, and no property element for them (`<Button.Margin>`)
  - no `Width`, `Height`, `MinWidth`, `MaxWidth`, `MinHeight` or `MaxHeight` with a literal number (a binding, `Auto` and star sizes are allowed)
  - no `StaticResource` reference to a style or brush key defined in `UI/Theme/` (those must be `DynamicResource`)
  - every `TextBox`, `ComboBox` and `CheckBox` has a label: a `FieldRow` ancestor, a `Label` whose `Target` names it, or (for `CheckBox`) its own content
- [ ] Each failure message names the file, the element and the rule
- [ ] A second test checks that `Colors.Light.xaml` and `Colors.Dark.xaml` define the same keys, and that those keys are exactly the spec's sixteen tokens with a `Brush` suffix
- [ ] `UI/Controls/` is checked too, except that it may set the sizes and gaps the spec's spacing table lists when it takes them from `Styles.xaml`; if that exception needs a rule-by-rule allowance, keep the allowance list in the test file, short and commented
- [ ] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- This is a test of add-in files run from the Core test project, which has no WPF reference. Reading the files as XML is enough; do not reference the add-in project.
- Make sure the XAML files are found in a cloud checkout and on Windows (path separators, case).
- Keep the test readable: one small method per rule, with the list of forbidden attributes at the top.
- The test is a default logged in `DECISIONS.md`: Aidan may drop it.

## Verification

Filled in by the implementer.

- **Ran:**
- **Not compiled (changed under `src/InventorAddin`):** none
- **Inventor API members not confirmed:** none
- **Manual checklist for Inventor:** none

## Follow-ups

Things noticed but not done.
