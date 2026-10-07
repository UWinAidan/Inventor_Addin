# 031: A test that keeps windows on the shared styles

- **Milestone:** M1b
- **Feature spec:** `docs/features/08-window-style.md` (How it is built: "No colours, font sizes or margins typed into a window's XAML. ... This is checked in review.")
- **Status:** done
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

- [x] The test finds the repository root by walking up from the test assembly's folder to the folder holding `InventorAddin.slnx`, and fails with a clear message if it cannot
- [x] It loads every `*.xaml` under `src/InventorAddin/UI/` except `UI/Theme/` as XML (`System.Xml.Linq`; no WPF reference) and checks, for each element:
  - no attribute named `Background`, `Foreground`, `BorderBrush`, `Fill`, `Stroke`, `FontSize`, `FontWeight`, `FontFamily`, `Margin` or `Padding`, and no property element for them (`<Button.Margin>`)
  - no `Width`, `Height`, `MinWidth`, `MaxWidth`, `MinHeight` or `MaxHeight` with a literal number (a binding, `Auto` and star sizes are allowed)
  - no `StaticResource` reference to a style or brush key defined in `UI/Theme/` (those must be `DynamicResource`)
  - every `TextBox`, `ComboBox` and `CheckBox` has a label: a `FieldRow` ancestor, a `Label` whose `Target` names it, or (for `CheckBox`) its own content
- [x] Each failure message names the file, the element and the rule
- [x] A second test checks that `Colors.Light.xaml` and `Colors.Dark.xaml` define the same keys, and that those keys are exactly the spec's sixteen tokens with a `Brush` suffix
- [x] `UI/Controls/` is checked too, except that it may set the sizes and gaps the spec's spacing table lists when it takes them from `Styles.xaml`; if that exception needs a rule-by-rule allowance, keep the allowance list in the test file, short and commented
- [x] `dotnet test tests/InventorAddin.Core.Tests` passes

## Notes for the implementer

- This is a test of add-in files run from the Core test project, which has no WPF reference. Reading the files as XML is enough; do not reference the add-in project.
- Make sure the XAML files are found in a cloud checkout and on Windows (path separators, case).
- Keep the test readable: one small method per rule, with the list of forbidden attributes at the top.
- The test is a default logged in `DECISIONS.md`: Aidan may drop it.

## Verification

Filled in by the implementer.

- **Ran (cloud):** `dotnet build src/InventorAddin.Core`: 0 errors. `dotnet test tests/InventorAddin.Core.Tests`: 777 passed, 0 failed (63 of them in `WindowXamlRulesTests`), after the review fixes below. The test project builds with 0 warnings.
- **Shown to fail:** a temporary `src/InventorAddin/UI/ZzTemp.xaml` with `Width="420"`, `Margin="10"`, `Foreground="Red"`, `FontSize="15"`, `{StaticResource ValueText}` and an unlabelled `TextBox` failed `WindowXaml_UsesOnlySharedLook` with six messages, one per breach, each naming the file, line, element and rule. The file was then deleted. The same breaches are also kept as permanent theory cases (`Check_ReportsBreach`).
- **Not compiled (changed under `src/InventorAddin`):** none
- **Inventor API members not confirmed:** none
- **Manual checklist for Inventor:** none

**What the test allows, and why**

- `SizeToContent`, `ResizeMode`, `ShowInTaskbar` and `Icon="{DynamicResource HeaderIcon}"` on the windows: these are not on the forbidden list (026, 030).
- `StaticResource` to a key defined in the same file, such as `ValueOrPlaceholder` and `IdentifierOrPlaceholder` in `PartPropertiesWindow.xaml` (029). A `StaticResource` to any other key fails: a theme key must be `DynamicResource`, and an unknown key is reported too. Local style setters are checked like attributes, so a local style still may not set a colour, a font, a margin or a fixed size.
- A `TextBox` whose style is a shared read-only style needs no label, unless the element sets `IsReadOnly` to anything other than True itself. That covers `SelectableText` and `SelectableIdentifierText`, worked out from `Styles.xaml`: any style that sets `IsReadOnly` to True, and any style based on one. These are values on show (the log folder, and the part number inside 029's template), not inputs.
- `UI/Controls/`: may set `Margin`, `Padding` and the size properties only through `{DynamicResource K}` where `K` is defined in `Styles.xaml`. The allowance list is in the test. Today the folder holds no XAML, so this is covered by unit cases only.
- `Styles.xaml` (third test, `Styles_TypeNoColourOfTheirOwn`): any colour property is a `DynamicResource`, a `TemplateBinding` or `Transparent` (026).
- Sizes (`Width`, `Height`, `Min*`, `Max*`): a binding, `Auto`, a star size, or a `StaticResource`/`DynamicResource` to a key defined in `Styles.xaml`. A reference to a key of the window's own, or any other markup extension (`x:Static`), counts as fixed, so a number cannot be hidden in a local resource.
- Extra keys (`DialogContent`, `FocusOutline`, `CheckBoxFocusOutline`) are read from `UI/Theme/`, not listed, so new keys need no change to the test.

**Review fixes (031 review):** sizes hidden in a local resource (`<sys:Double x:Key="W">` with `Width="{StaticResource W}"`, or `MinWidth="{DynamicResource W}"`) now fail; a `FieldRow` with a `Target` but no `Label` no longer labels a control; the read-only exemption no longer applies when the element sets `IsReadOnly` to something other than True; any `*Brush` property is a colour property in both the window check and `Styles_TypeNoColourOfTheirOwn`. Each has a breach case in the test.

**Stricter than the brief, on purpose:** the label rule needs a `FieldRow` that has a `Label` (or a `Label`/`FieldRow` whose `Target` names the control), not just any `FieldRow` ancestor (and a `FieldRow` without a `Label` does not label the control its `Target` names), since a label-less `FieldRow` is how 028 lays out notes. The test also forbids a `Color` attribute and any `*Brush` or `Color` element in a window, and any property ending in `Brush` (`CaretBrush`, `SelectionBrush`), so a brush cannot be typed into `Window.Resources` and used through a local key. It also forbids `Double`, `Thickness` and `GridLength` resource elements in a window. A size given as a property element (`<ColumnDefinition.Width>`) is treated as literal.

## Follow-ups

Things noticed but not done.

- **C# in `UI/Controls/` is not checked.** The shared pieces are C# classes whose look comes from templates in `Styles.xaml`, and today they set no sizes, colours or margins in code. The test reads XAML only, so a literal `new Thickness(4)` added to a control's code would not be caught.
- **`DynamicResource` keys are not checked to exist.** A misspelt `{DynamicResource ValeuText}` passes silently, since WPF falls back without an error. Checking them would need an allowance for keys `WindowHost` adds at run time (`HeaderIcon`).
- **(Review)** A size given as a binding with a typed fallback (`{Binding Path=X, FallbackValue=420}` or `{Binding Source=300}`) still passes. Only a deliberate trick would do this; close it if wanted by rejecting `FallbackValue=`, `TargetNullValue=` or `Source=` in a size binding.
