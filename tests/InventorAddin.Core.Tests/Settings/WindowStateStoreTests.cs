using InventorAddin.Core.Settings;

namespace InventorAddin.Core.Tests.Settings;

public sealed class WindowStateStoreTests : IDisposable
{
    private readonly string _root;
    private readonly string _folder;

    public WindowStateStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "InventorAddinTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _folder = Path.Combine(_root, "data");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void FileName_IsWindowsJson_BesideTheSettings()
    {
        var store = new WindowStateStore(_folder);

        Assert.Equal(Path.Combine(_folder, "windows.json"), store.FilePath);
    }

    [Fact]
    public void Load_NoFile_IsEmpty_WithNoWarning()
    {
        var state = new WindowStateStore(_folder).Load(out var warning);

        Assert.Empty(state.Windows);
        Assert.Null(warning);
        Assert.Null(state.For("Settings").Placement);
        Assert.Empty(state.For("Settings").CollapsedSections);
    }

    [Fact]
    public void Save_ThenLoad_GivesBackPlacementAndCollapsedSections()
    {
        var store = new WindowStateStore(_folder);
        store.Save("Settings", new RememberedWindow
        {
            Placement = new WindowPlacement(10.5, 20, 640, 700, Maximized: true),
            CollapsedSections = new List<string> { "FoldersSection", "DeveloperSection" },
        });

        var loaded = store.Load(out var warning).For("Settings");

        Assert.Null(warning);
        Assert.Equal(new WindowPlacement(10.5, 20, 640, 700, true), loaded.Placement);
        Assert.Equal(new[] { "FoldersSection", "DeveloperSection" }, loaded.CollapsedSections);
    }

    [Fact]
    public void Save_KeepsOtherWindowsEntries()
    {
        var store = new WindowStateStore(_folder);
        store.Save("Settings", new RememberedWindow { CollapsedSections = new List<string> { "A" } });
        store.Save("Other", new RememberedWindow { CollapsedSections = new List<string> { "B" } });

        var state = store.Load(out _);

        Assert.Equal(new[] { "A" }, state.For("Settings").CollapsedSections);
        Assert.Equal(new[] { "B" }, state.For("Other").CollapsedSections);
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    [Fact]
    public void Load_InvalidFile_IsEmpty_WithAWarning()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "windows.json"), "{ not json");

        var state = new WindowStateStore(_folder).Load(out var warning);

        Assert.Empty(state.Windows);
        Assert.NotNull(warning);
        Assert.Contains("windows.json", warning);
    }

    [Fact]
    public void Save_OverAnInvalidFile_ReplacesIt()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "windows.json"), "[1, 2]");
        var store = new WindowStateStore(_folder);

        store.Save("Settings", new RememberedWindow { CollapsedSections = new List<string> { "A" } });

        Assert.Equal(new[] { "A" }, store.Load(out var warning).For("Settings").CollapsedSections);
        Assert.Null(warning);
    }

    [Fact]
    public void Load_NullEntries_ReadAsEmpty()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "windows.json"), """{ "windows": { "Settings": null } }""");

        var state = new WindowStateStore(_folder).Load(out var warning);

        Assert.Null(warning);
        Assert.Empty(state.For("Settings").CollapsedSections);
    }
}
