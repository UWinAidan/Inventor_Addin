using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace InventorAddin.Core.Tests.Styling;

/// <summary>
/// Spec 08, "How it is built": windows contain layout and bindings only, and every colour, font and gap comes from
/// the shared dictionaries in <c>src/InventorAddin/UI/Theme/</c>. These tests read the add-in's XAML as plain XML
/// (no WPF, no add-in reference) so a window that types its own colour, font size, margin or fixed size fails
/// <c>dotnet test</c> in the cloud as well as on Windows.
/// </summary>
public sealed class WindowXamlRulesTests
{
    // ===== The rules =====

    /// <summary>Properties a window may not set at all: colours, type and spacing. <c>Color</c> is here so a
    /// brush typed inside a window is caught as well as one typed on an attribute. Any property ending in Brush
    /// (CaretBrush, SelectionBrush) is forbidden too; see IsForbidden.</summary>
    private static readonly HashSet<string> ForbiddenProperties = new(StringComparer.Ordinal)
    {
        "Background", "Foreground", "BorderBrush", "Fill", "Stroke", "Color",
        "FontSize", "FontWeight", "FontFamily",
        "Margin", "Padding",
    };

    /// <summary>Size properties: a binding, a resource reference, <c>Auto</c> or a star size is fine; a number is not.</summary>
    private static readonly HashSet<string> SizeProperties = new(StringComparer.Ordinal)
    {
        "Width", "Height", "MinWidth", "MaxWidth", "MinHeight", "MaxHeight",
    };

    /// <summary>Colour properties, for the Styles.xaml check. Any property ending in Brush counts as well.</summary>
    private static readonly HashSet<string> ColourProperties = new(StringComparer.Ordinal)
    {
        "Background", "Foreground", "Fill", "Stroke", "Color",
    };

    /// <summary>Value types a window may not define as its own resources, since a size property could then take a
    /// typed number through a local key.</summary>
    private static readonly HashSet<string> SizeValueTypes = new(StringComparer.Ordinal)
    {
        "Double", "Thickness", "GridLength",
    };

    /// <summary>Input controls that need a label.</summary>
    private static readonly HashSet<string> LabelledControls = new(StringComparer.Ordinal)
    {
        "TextBox", "ComboBox", "CheckBox",
    };

    /// <summary>
    /// UI/Controls allowance (task 031): the shared pieces may set the spacing table's sizes and gaps, but only by
    /// taking them from Styles.xaml with DynamicResource. Nothing else in the forbidden lists is allowed there.
    /// </summary>
    private static readonly HashSet<string> ControlsMayTakeFromStyles = new(StringComparer.Ordinal)
    {
        "Margin", "Padding", "Width", "Height", "MinWidth", "MaxWidth", "MinHeight", "MaxHeight",
    };

    /// <summary>
    /// The one colour value Styles.xaml may type itself: the spec's "transparent" for secondary and disabled fills
    /// (task 026 follow-up). Everything else is a DynamicResource to a colour token or a TemplateBinding.
    /// </summary>
    private const string AllowedLiteralColour = "Transparent";

    /// <summary>Spec 08, "Colours": the sixteen tokens. Each is defined in both colour sets as <c>&lt;Token&gt;Brush</c>.</summary>
    private static readonly string[] ColourTokens =
    {
        "WindowBackground", "TextPrimary", "TextStrong", "TextLabel", "TextMuted", "TextPlaceholder",
        "SectionHeading", "Divider", "InputBackground", "InputBorder", "IconTile", "DisabledText",
        "DisabledBorder", "Accent", "OnAccent", "Error",
    };

    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static readonly Regex StaticResourceRef =
        new(@"\{StaticResource\s+(?:ResourceKey\s*=\s*)?(\{x:Type\s+[^}]+\}|[^\s,}]+)", RegexOptions.CultureInvariant);

    private static readonly Regex DynamicResourceOnly =
        new(@"^\{DynamicResource\s+(?:ResourceKey\s*=\s*)?([^\s,}]+)\s*\}$", RegexOptions.CultureInvariant);

    private static readonly Regex ResourceRefOnly =
        new(@"^\{(?:Static|Dynamic)Resource\s+(?:ResourceKey\s*=\s*)?([^\s,}]+)\s*\}$", RegexOptions.CultureInvariant);

    private static readonly Regex ElementNameRef =
        new(@"^\{(?:Binding\s+ElementName\s*=\s*|x:Reference\s+(?:Name\s*=\s*)?)([^\s,}]+)", RegexOptions.CultureInvariant);

    // ===== Tests on the real files =====

    [Fact]
    public void WindowXaml_UsesOnlySharedLook()
    {
        var theme = ThemeInfo.Load(UiFolder());
        var files = WindowXamlFiles();
        Assert.True(files.Count > 0, $"No *.xaml files found under {UiFolder()} outside Theme/; the test is looking in the wrong place.");

        var failures = files
            .SelectMany(f => Check(XDocument.Load(f.FullPath, LoadOptions.SetLineInfo), f.RelativePath, f.IsControls, theme))
            .ToList();

        Assert.True(failures.Count == 0, "Window XAML breaks the spec 08 style rules:\n" + string.Join("\n", failures));
    }

    [Fact]
    public void ColourSets_DefineTheSameKeys_ExactlyTheSpecTokens()
    {
        var themeDir = Subfolder(UiFolder(), "Theme");
        var light = KeysOf(XDocument.Load(FileIn(themeDir, "Colors.Light.xaml")));
        var dark = KeysOf(XDocument.Load(FileIn(themeDir, "Colors.Dark.xaml")));
        var expected = ColourTokens.Select(t => t + "Brush").OrderBy(k => k, StringComparer.Ordinal).ToList();

        Assert.Equal(light.Count, light.Distinct().Count());
        Assert.Equal(dark.Count, dark.Distinct().Count());
        Assert.Equal(expected, light.OrderBy(k => k, StringComparer.Ordinal).ToList());
        Assert.Equal(expected, dark.OrderBy(k => k, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void Styles_TypeNoColourOfTheirOwn()
    {
        var path = FileIn(Subfolder(UiFolder(), "Theme"), "Styles.xaml");
        var failures = CheckStylesColours(XDocument.Load(path, LoadOptions.SetLineInfo), "UI/Theme/Styles.xaml");

        Assert.True(failures.Count == 0, "Styles.xaml types a colour of its own:\n" + string.Join("\n", failures));
    }

    // ===== Tests that the rules catch a breach =====

    public static TheoryData<string, string> Breaches => new()
    {
        { """<TextBlock Foreground="Red" />""", "sets Foreground" },
        { """<TextBlock Background="{DynamicResource WindowBackgroundBrush}" />""", "sets Background" },
        { """<TextBlock FontSize="14" />""", "sets FontSize" },
        { """<TextBlock FontWeight="Bold" />""", "sets FontWeight" },
        { """<TextBlock FontFamily="Arial" />""", "sets FontFamily" },
        { """<TextBlock TextElement.FontSize="14" />""", "sets FontSize" },
        { """<StackPanel Margin="4" />""", "sets Margin" },
        { """<Border Padding="4" />""", "sets Padding" },
        { """<Button><Button.Margin>4</Button.Margin></Button>""", "sets Margin" },
        { """<Rectangle Fill="Red" Stroke="Blue" />""", "sets Fill" },
        { """<Border><Border.BorderBrush><SolidColorBrush Color="Red" /></Border.BorderBrush></Border>""", "sets BorderBrush" },
        { """<StackPanel><StackPanel.Resources><SolidColorBrush x:Key="Mine" Color="Red" /></StackPanel.Resources></StackPanel>""", "defines a brush or colour" },
        { """<StackPanel><StackPanel.Resources><Style x:Key="S" TargetType="{x:Type TextBlock}"><Setter Property="Margin" Value="4" /></Style></StackPanel.Resources></StackPanel>""", "sets Margin" },
        { """<StackPanel><StackPanel.Resources><Style x:Key="S" TargetType="{x:Type TextBlock}"><Setter Property="Width" Value="40" /></Style></StackPanel.Resources></StackPanel>""", "fixed Width" },
        { """<StackPanel Width="420" />""", "fixed Width" },
        { """<StackPanel Height="300" />""", "fixed Height" },
        { """<StackPanel MinWidth="100" />""", "fixed MinWidth" },
        { """<StackPanel MaxHeight="100" />""", "fixed MaxHeight" },
        { """<Grid><Grid.ColumnDefinitions><ColumnDefinition Width="100" /></Grid.ColumnDefinitions></Grid>""", "fixed Width" },
        { """<TextBlock Style="{StaticResource ValueText}" />""", "StaticResource to theme key 'ValueText'" },
        { """<TextBlock Style="{StaticResource Elsewhere}" />""", "StaticResource to 'Elsewhere'" },
        { """<TextBox x:Name="Lonely" />""", "<TextBox Lonely>: has no label" },
        { """<controls:FieldRow><TextBox /></controls:FieldRow>""", "<TextBox>: has no label" },
        { """<ComboBox />""", "<ComboBox>: has no label" },
        { """<CheckBox />""", "<CheckBox>: has no label" },
        // Sizes hidden in a resource of the window's own (review of 031).
        { """<StackPanel><StackPanel.Resources><sys:Double x:Key="W">420</sys:Double></StackPanel.Resources></StackPanel>""", "defines a size value (Double)" },
        { """<StackPanel><StackPanel.Resources><Thickness x:Key="T">4</Thickness></StackPanel.Resources></StackPanel>""", "defines a size value (Thickness)" },
        { """<StackPanel><StackPanel.Resources><GridLength x:Key="G">100</GridLength></StackPanel.Resources></StackPanel>""", "defines a size value (GridLength)" },
        { """<StackPanel><StackPanel.Resources><x:Static x:Key="W" Member="sys:Double.MaxValue" /></StackPanel.Resources><StackPanel Width="{StaticResource W}" /></StackPanel>""", "fixed Width ({StaticResource W})" },
        { """<StackPanel MinWidth="{DynamicResource W}" />""", "fixed MinWidth ({DynamicResource W})" },
        { """<StackPanel Width="{x:Static sys:Double.MaxValue}" />""", "fixed Width" },
        // A FieldRow without a Label does not label the control its Target names.
        { """<StackPanel><controls:FieldRow Target="{Binding ElementName=Box}"><TextBox x:Name="Box" /></controls:FieldRow></StackPanel>""", "<TextBox Box>: has no label" },
        // A read-only style does not excuse a text box that makes itself editable again.
        { """<TextBox Style="{DynamicResource SelectableText}" IsReadOnly="False" />""", "<TextBox>: has no label" },
        // Any brush property is a colour.
        { """<TextBox CaretBrush="Red" />""", "sets CaretBrush" },
        { """<TextBox SelectionBrush="{DynamicResource AccentBrush}" />""", "sets SelectionBrush" },
    };

    [Theory]
    [MemberData(nameof(Breaches))]
    public void Check_ReportsBreach(string body, string expected)
    {
        var failures = Check(Window(body), "Test.xaml", isControls: false, TestTheme);

        Assert.Contains(failures, f => f.Contains(expected, StringComparison.Ordinal) && f.StartsWith("Test.xaml", StringComparison.Ordinal));
    }

    public static TheoryData<string> Allowed => new()
    {
        """<StackPanel Style="{DynamicResource DialogContent}" />""",
        """<StackPanel Width="{Binding Size}" Height="Auto" />""",
        """<StackPanel Width="{DynamicResource ControlHeight}" />""",
        """<StackPanel><StackPanel.Resources><sys:Boolean x:Key="Yes">True</sys:Boolean></StackPanel.Resources></StackPanel>""",
        """<TextBox Style="{DynamicResource SelectableText}" IsReadOnly="True" />""",
        """<Grid><Grid.ColumnDefinitions><ColumnDefinition Width="*" /><ColumnDefinition Width="2*" /><ColumnDefinition Width="Auto" /></Grid.ColumnDefinitions></Grid>""",
        """<controls:FieldRow Label="_Name"><TextBox /></controls:FieldRow>""",
        """<StackPanel><controls:FieldRow Label="_Code" Target="{Binding ElementName=CodeBox}"><StackPanel><ComboBox x:Name="CodeBox" /></StackPanel></controls:FieldRow></StackPanel>""",
        """<StackPanel><Label Content="_Name" Target="{Binding ElementName=NameBox}" /><TextBox x:Name="NameBox" /></StackPanel>""",
        """<CheckBox Content="Show it" />""",
        """<CheckBox><TextBlock Text="Show it" /></CheckBox>""",
        """<TextBox Style="{DynamicResource SelectableText}" />""",
        """<StackPanel><StackPanel.Resources><Style x:Key="Local" TargetType="{x:Type ContentControl}"><Setter Property="Focusable" Value="False" /></Style></StackPanel.Resources><ContentControl Style="{StaticResource Local}" /></StackPanel>""",
    };

    [Theory]
    [MemberData(nameof(Allowed))]
    public void Check_AllowsSharedLook(string body)
    {
        var failures = Check(Window(body), "Test.xaml", isControls: false, TestTheme);

        Assert.Empty(failures);
    }

    [Fact]
    public void Check_WindowRootAttributes_AreChecked()
    {
        var doc = XDocument.Parse(
            """<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="420" Background="White" SizeToContent="WidthAndHeight" />""",
            LoadOptions.SetLineInfo);

        var failures = Check(doc, "Test.xaml", isControls: false, TestTheme);

        Assert.Contains(failures, f => f.Contains("fixed Width", StringComparison.Ordinal));
        Assert.Contains(failures, f => f.Contains("sets Background", StringComparison.Ordinal));
        Assert.Equal(2, failures.Count);
    }

    [Fact]
    public void Check_Controls_MayTakeSpacingFromStyles_Only()
    {
        var fromStyles = Check(Window("""<Border Margin="{DynamicResource RowGap}" Width="{DynamicResource RowGap}" />"""), "Controls/X.xaml", isControls: true, TestTheme);
        var typed = Check(Window("""<Border Margin="9" />"""), "Controls/X.xaml", isControls: true, TestTheme);
        var colour = Check(Window("""<Border Background="{DynamicResource WindowBackgroundBrush}" />"""), "Controls/X.xaml", isControls: true, TestTheme);
        var inWindow = Check(Window("""<Border Margin="{DynamicResource RowGap}" />"""), "Test.xaml", isControls: false, TestTheme);

        Assert.Empty(fromStyles);
        Assert.Contains(typed, f => f.Contains("sets Margin", StringComparison.Ordinal));
        Assert.Contains(colour, f => f.Contains("sets Background", StringComparison.Ordinal));
        Assert.Contains(inWindow, f => f.Contains("sets Margin", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""<Border Background="Red" />""")]
    [InlineData("""<Border BorderBrush="#FF0000" />""")]
    [InlineData("""<Style><Setter Property="Foreground" Value="White" /></Style>""")]
    [InlineData("""<Border><Border.Background><SolidColorBrush Color="Red" /></Border.Background></Border>""")]
    [InlineData("""<TextBox CaretBrush="Red" />""")]
    [InlineData("""<Style><Setter Property="SelectionBrush" Value="Blue" /></Style>""")]
    public void CheckStylesColours_ReportsTypedColour(string body)
    {
        var failures = CheckStylesColours(Dictionary(body), "Styles.xaml");

        Assert.NotEmpty(failures);
    }

    [Theory]
    [InlineData("""<Border Background="Transparent" />""")]
    [InlineData("""<Border Background="{DynamicResource WindowBackgroundBrush}" />""")]
    [InlineData("""<Border BorderBrush="{TemplateBinding BorderBrush}" />""")]
    [InlineData("""<Style><Setter Property="Background" Value="Transparent" /></Style>""")]
    public void CheckStylesColours_AllowsTokensAndTransparent(string body)
    {
        Assert.Empty(CheckStylesColours(Dictionary(body), "Styles.xaml"));
    }

    // ===== Rule checks =====

    /// <summary>All failures in one window or control file, each naming the file, the element and the rule.</summary>
    private static List<string> Check(XDocument doc, string file, bool isControls, ThemeInfo theme)
    {
        var failures = new List<string>();
        var localKeys = KeysOf(doc).ToHashSet(StringComparer.Ordinal);

        foreach (var element in doc.Descendants())
        {
            var report = (string rule) => failures.Add($"{file}{LineOf(element)}: {Describe(element)}: {rule}");

            CheckNoBrushElements(element, report);
            CheckProperties(element, isControls, theme, report);
            CheckStaticResources(element, localKeys, theme, report);
            CheckLabel(element, doc, theme, report);
        }

        return failures;
    }

    /// <summary>A brush, colour or size object typed inside a window, for example a local SolidColorBrush or
    /// sys:Double resource, which a property could then use through a local key.</summary>
    private static void CheckNoBrushElements(XElement element, Action<string> report)
    {
        var name = element.Name.LocalName;
        if (name.Contains('.'))
            return;
        if (name.EndsWith("Brush", StringComparison.Ordinal) || name == "Color")
            report($"defines a brush or colour ({name}); colours come from UI/Theme");
        else if (SizeValueTypes.Contains(name))
            report($"defines a size value ({name}); sizes and gaps come from UI/Theme");
    }

    /// <summary>Forbidden and size properties, set as an attribute, a property element or a style Setter.</summary>
    private static void CheckProperties(XElement element, bool isControls, ThemeInfo theme, Action<string> report)
    {
        foreach (var (property, value) in PropertiesSetBy(element))
        {
            var takenFromStyles = isControls && ControlsMayTakeFromStyles.Contains(property) && value is not null
                && DynamicResourceOnly.Match(value) is { Success: true } m && theme.StyleKeys.Contains(m.Groups[1].Value);
            if (takenFromStyles)
                continue;

            if (IsForbidden(property))
                report($"sets {property}; use a shared style from UI/Theme");
            else if (SizeProperties.Contains(property) && !IsAllowedSize(value, theme))
                report($"fixed {property} ({value ?? "property element"}); windows size to their content or use a shared style");
        }
    }

    /// <summary>StaticResource may only name a key defined in the same file; theme keys must be DynamicResource,
    /// because the theme is merged into the window after it is built.</summary>
    private static void CheckStaticResources(XElement element, HashSet<string> localKeys, ThemeInfo theme, Action<string> report)
    {
        var keys = element.Attributes()
            .SelectMany(a => StaticResourceRef.Matches(a.Value).Select(m => NormaliseKey(m.Groups[1].Value)))
            .ToList();
        if (element.Name.LocalName is "StaticResource" or "StaticResourceExtension" && element.Attribute("ResourceKey") is { } rk)
            keys.Add(NormaliseKey(rk.Value));

        foreach (var key in keys)
        {
            if (theme.AllKeys.Contains(key))
                report($"StaticResource to theme key '{key}'; use DynamicResource");
            else if (!localKeys.Contains(key))
                report($"StaticResource to '{key}', which is not defined in this file");
        }
    }

    /// <summary>Every TextBox, ComboBox and CheckBox has a label: a FieldRow with a Label around it, a Label (or
    /// FieldRow with a Label) whose Target names it, or, for a CheckBox, its own content. A TextBox in a shared
    /// read-only style (selectable text) is a value on show, not an input, and needs none, unless it sets
    /// IsReadOnly to something other than True itself.</summary>
    private static void CheckLabel(XElement element, XDocument doc, ThemeInfo theme, Action<string> report)
    {
        var kind = element.Name.LocalName;
        if (!LabelledControls.Contains(kind))
            return;

        var keepsReadOnly = element.Attribute("IsReadOnly")?.Value is not { } readOnly
            || string.Equals(readOnly.Trim(), "True", StringComparison.OrdinalIgnoreCase);
        if (kind == "TextBox" && keepsReadOnly && element.Attribute("Style")?.Value is { } style
            && DynamicResourceOnly.Match(style) is { Success: true } m && theme.ReadOnlyStyleKeys.Contains(m.Groups[1].Value))
            return;

        var fieldRow = element.Ancestors().FirstOrDefault(a => a.Name.LocalName == "FieldRow");
        if (!string.IsNullOrWhiteSpace(fieldRow?.Attribute("Label")?.Value))
            return;

        var name = NameOf(element);
        if (name is not null && doc.Descendants()
                .Where(d => d.Name.LocalName == "Label"
                    || (d.Name.LocalName == "FieldRow" && !string.IsNullOrWhiteSpace(d.Attribute("Label")?.Value)))
                .Any(d => TargetName(d.Attribute("Target")?.Value) == name))
            return;

        if (kind == "CheckBox" && HasOwnContent(element))
            return;

        report("has no label (a FieldRow with a Label, a Label whose Target names it, or its own content for a CheckBox)");
    }

    /// <summary>Styles.xaml: any colour property is a DynamicResource, a TemplateBinding or Transparent.</summary>
    private static List<string> CheckStylesColours(XDocument doc, string file)
    {
        var failures = new List<string>();

        foreach (var element in doc.Descendants())
        {
            foreach (var (property, value) in PropertiesSetBy(element))
            {
                if (!IsColourProperty(property))
                    continue;
                var ok = value is not null && (value == AllowedLiteralColour
                    || value.StartsWith("{DynamicResource ", StringComparison.Ordinal)
                    || value.StartsWith("{TemplateBinding ", StringComparison.Ordinal));
                if (!ok)
                    failures.Add($"{file}{LineOf(element)}: {Describe(element)}: {property} is '{value ?? "property element"}'; use a colour token with DynamicResource");
            }
        }

        return failures;
    }

    // ===== Reading XAML =====

    /// <summary>
    /// The properties an element sets, by property name without any owner prefix: its attributes, a property element
    /// (value null), or for a Setter the property it sets. x: and xmlns attributes are not properties.
    /// </summary>
    private static IEnumerable<(string Property, string? Value)> PropertiesSetBy(XElement element)
    {
        var local = element.Name.LocalName;
        if (local.Contains('.'))
        {
            yield return (AfterLastDot(local), null);
            yield break;
        }

        if (local == "Setter" && element.Attribute("Property")?.Value is { } setterProperty)
        {
            yield return (AfterLastDot(setterProperty), element.Attribute("Value")?.Value);
            yield break;
        }

        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration || attribute.Name.Namespace == Xaml)
                continue;
            if (attribute.Name.Namespace != XNamespace.None)
                continue; // mc:Ignorable, po:Freeze and similar
            yield return (AfterLastDot(attribute.Name.LocalName), attribute.Value);
        }
    }

    /// <summary>
    /// A size a window may set: a binding, Auto, a star size, or a resource reference to a key in Styles.xaml. A
    /// number, a property element (an object typed in place), a reference to a key of the window's own (which could
    /// hold a typed number) and any other markup extension are not allowed.
    /// </summary>
    private static bool IsAllowedSize(string? value, ThemeInfo theme)
    {
        if (value is null)
            return false;
        var v = value.Trim();
        if (v.Equals("Auto", StringComparison.OrdinalIgnoreCase) || v.EndsWith('*'))
            return true;
        if (v.StartsWith("{Binding", StringComparison.Ordinal) || v.StartsWith("{TemplateBinding ", StringComparison.Ordinal))
            return true;
        return ResourceRefOnly.Match(v) is { Success: true } m && theme.StyleKeys.Contains(m.Groups[1].Value);
    }

    /// <summary>A forbidden property: one on the list, or any brush property (CaretBrush, SelectionBrush and so on).</summary>
    private static bool IsForbidden(string property) =>
        ForbiddenProperties.Contains(property) || property.EndsWith("Brush", StringComparison.Ordinal);

    private static bool IsColourProperty(string property) =>
        ColourProperties.Contains(property) || property.EndsWith("Brush", StringComparison.Ordinal);

    private static bool HasOwnContent(XElement checkBox) =>
        !string.IsNullOrWhiteSpace(checkBox.Attribute("Content")?.Value)
        || checkBox.Elements().Any(e => !e.Name.LocalName.Contains('.'))
        || checkBox.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value));

    private static string? TargetName(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return null;
        var m = ElementNameRef.Match(target.Trim());
        return m.Success ? m.Groups[1].Value : target.Trim();
    }

    private static string? NameOf(XElement element) =>
        element.Attribute(Xaml + "Name")?.Value ?? element.Attribute("Name")?.Value;

    private static string Describe(XElement element)
    {
        var name = NameOf(element);
        return $"<{element.Name.LocalName}{(name is null ? "" : " " + name)}>";
    }

    private static string LineOf(XElement element) =>
        element is System.Xml.IXmlLineInfo info && info.HasLineInfo() ? $" line {info.LineNumber}" : "";

    private static string AfterLastDot(string s) => s[(s.LastIndexOf('.') + 1)..];

    private static string NormaliseKey(string key) => Regex.Replace(key.Trim(), @"\s+", " ");

    /// <summary>Resource keys a file defines: every x:Key, and the type of each keyless (implicit) Style.</summary>
    private static List<string> KeysOf(XDocument doc)
    {
        var keys = new List<string>();
        foreach (var element in doc.Descendants())
        {
            if (element.Attribute(Xaml + "Key")?.Value is { } key)
                keys.Add(NormaliseKey(key));
            else if (element.Name.LocalName == "Style" && element.Attribute("TargetType")?.Value is { } type)
                keys.Add(NormaliseKey(type));
        }
        return keys;
    }

    // ===== Finding the files =====

    private sealed record XamlFile(string FullPath, string RelativePath, bool IsControls);

    /// <summary>Every *.xaml under UI/ except UI/Theme/, any case, with forward-slash relative paths for messages.</summary>
    private static List<XamlFile> WindowXamlFiles()
    {
        var ui = UiFolder();
        var options = new EnumerationOptions { RecurseSubdirectories = true, MatchCasing = MatchCasing.CaseInsensitive };
        return Directory.EnumerateFiles(ui, "*.xaml", options)
            .Select(full => (full, rel: Path.GetRelativePath(ui, full).Replace('\\', '/')))
            .Where(f => !FirstSegmentIs(f.rel, "Theme"))
            .OrderBy(f => f.rel, StringComparer.Ordinal)
            .Select(f => new XamlFile(f.full, "UI/" + f.rel, FirstSegmentIs(f.rel, "Controls")))
            .ToList();
    }

    private static bool FirstSegmentIs(string relativePath, string folder)
    {
        var slash = relativePath.IndexOf('/');
        return slash > 0 && string.Equals(relativePath[..slash], folder, StringComparison.OrdinalIgnoreCase);
    }

    private static string UiFolder() => Subfolder(Subfolder(Subfolder(RepoRoot(), "src"), "InventorAddin"), "UI");

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "InventorAddin.slnx")))
                return dir.FullName;
        }
        Assert.Fail($"Could not find the repository root (the folder holding InventorAddin.slnx) above {AppContext.BaseDirectory}.");
        return "";
    }

    /// <summary>A child folder by name, ignoring case, so the test works on case-sensitive file systems too.</summary>
    private static string Subfolder(string parent, string name)
    {
        var found = Directory.Exists(parent)
            ? Directory.EnumerateDirectories(parent).FirstOrDefault(d => string.Equals(Path.GetFileName(d), name, StringComparison.OrdinalIgnoreCase))
            : null;
        Assert.True(found is not null, $"Folder '{name}' not found in {parent}.");
        return found!;
    }

    private static string FileIn(string folder, string name)
    {
        var found = Directory.EnumerateFiles(folder).FirstOrDefault(f => string.Equals(Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
        Assert.True(found is not null, $"File '{name}' not found in {folder}.");
        return found!;
    }

    // ===== Theme keys =====

    /// <summary>The keys the theme defines, the keys in Styles.xaml alone, and the read-only text box styles.</summary>
    private sealed record ThemeInfo(HashSet<string> AllKeys, HashSet<string> StyleKeys, HashSet<string> ReadOnlyStyleKeys)
    {
        public static ThemeInfo Load(string uiFolder)
        {
            var themeDir = Subfolder(uiFolder, "Theme");
            var options = new EnumerationOptions { MatchCasing = MatchCasing.CaseInsensitive };
            var all = Directory.EnumerateFiles(themeDir, "*.xaml", options)
                .SelectMany(f => KeysOf(XDocument.Load(f)))
                .ToHashSet(StringComparer.Ordinal);
            var styles = XDocument.Load(FileIn(themeDir, "Styles.xaml"));
            return new ThemeInfo(all, KeysOf(styles).ToHashSet(StringComparer.Ordinal), ReadOnlyStyles(styles));
        }

        /// <summary>Styles that set IsReadOnly to True, and styles based on them.</summary>
        private static HashSet<string> ReadOnlyStyles(XDocument styles)
        {
            var byKey = styles.Descendants()
                .Where(e => e.Name.LocalName == "Style" && e.Attribute(Xaml + "Key") is not null)
                .ToDictionary(e => e.Attribute(Xaml + "Key")!.Value, StringComparer.Ordinal);

            var readOnly = byKey.Where(kv => kv.Value.Elements().Any(s => s.Name.LocalName == "Setter"
                    && s.Attribute("Property")?.Value == "IsReadOnly"
                    && string.Equals(s.Attribute("Value")?.Value, "True", StringComparison.OrdinalIgnoreCase)))
                .Select(kv => kv.Key)
                .ToHashSet(StringComparer.Ordinal);

            bool added;
            do
            {
                added = false;
                foreach (var (key, style) in byKey)
                {
                    var basedOn = style.Attribute("BasedOn")?.Value;
                    var m = basedOn is null ? null : StaticResourceRef.Match(basedOn);
                    if (m is { Success: true } && readOnly.Contains(m.Groups[1].Value) && readOnly.Add(key))
                        added = true;
                }
            } while (added);

            return readOnly;
        }
    }

    // ===== Test fixtures =====

    private static readonly ThemeInfo TestTheme = new(
        AllKeys: new(StringComparer.Ordinal) { "ValueText", "SelectableText", "DialogContent", "RowGap", "ControlHeight", "WindowBackgroundBrush", "{x:Type TextBox}" },
        StyleKeys: new(StringComparer.Ordinal) { "ValueText", "SelectableText", "DialogContent", "RowGap", "ControlHeight", "{x:Type TextBox}" },
        ReadOnlyStyleKeys: new(StringComparer.Ordinal) { "SelectableText" });

    private static XDocument Window(string body) => XDocument.Parse(
        $"""
        <Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:sys="clr-namespace:System;assembly=System.Runtime"
                xmlns:controls="clr-namespace:InventorAddin.UI.Controls">
            {body}
        </Window>
        """,
        LoadOptions.SetLineInfo);

    private static XDocument Dictionary(string body) => XDocument.Parse(
        $"""
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            {body}
        </ResourceDictionary>
        """,
        LoadOptions.SetLineInfo);
}
