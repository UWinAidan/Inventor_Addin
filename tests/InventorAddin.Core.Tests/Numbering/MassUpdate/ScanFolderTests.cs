using InventorAddin.Core.Numbering.MassUpdate;

namespace InventorAddin.Core.Tests.Numbering.MassUpdate;

public sealed class ScanFolderTests : IDisposable
{
    private readonly string _root;

    public ScanFolderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "InventorAddinTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string Touch(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "");
        return path;
    }

    [Fact]
    public void ListsPartsAndAssembliesIncludingSubfoldersSortedByPath()
    {
        var b = Touch("b_plate.ipt");
        var a = Touch("a_frame.iam");
        var sub = Touch("Sub", "c_pin.ipt");
        var deeper = Touch("Sub", "Deeper", "d_cap.ipt");

        var files = ScanFolder.FilesToScan(_root);

        Assert.Equal(new[] { a, b, deeper, sub }.OrderBy(p => p, StringComparer.OrdinalIgnoreCase), files);
    }

    [Fact]
    public void SkipsOtherFileTypes()
    {
        var part = Touch("plate.ipt");
        Touch("plate.idw");
        Touch("plate.dwg");
        Touch("notes.txt");
        Touch("plate.ipt.bak");

        Assert.Equal([part], ScanFolder.FilesToScan(_root));
    }

    [Fact]
    public void MatchesExtensionsIgnoringCase()
    {
        var upper = Touch("PLATE.IPT");
        var mixed = Touch("Frame.Iam");

        Assert.Equal(2, ScanFolder.FilesToScan(_root).Count);
        Assert.Contains(upper, ScanFolder.FilesToScan(_root));
        Assert.Contains(mixed, ScanFolder.FilesToScan(_root));
    }

    [Theory]
    [InlineData("OldVersions")]
    [InlineData("oldversions")]
    [InlineData("OLDVERSIONS")]
    public void SkipsOldVersionsFoldersIgnoringCase(string folderName)
    {
        var kept = Touch("plate.ipt");
        Touch(folderName, "plate.0001.ipt");
        Touch("Sub", folderName, "pin.0003.ipt");
        Touch(folderName, "Nested", "deep.ipt");

        Assert.Equal([kept], ScanFolder.FilesToScan(_root));
    }

    [Fact]
    public void KeepsFoldersWhoseNameOnlyContainsOldVersions()
    {
        var kept = Touch("OldVersionsArchive", "plate.ipt");

        Assert.Equal([kept], ScanFolder.FilesToScan(_root));
    }

    [Fact]
    public void EmptyFolderGivesNoFiles()
    {
        Assert.Empty(ScanFolder.FilesToScan(_root));
    }

    [Fact]
    public void MissingFolderThrows()
    {
        Assert.Throws<DirectoryNotFoundException>(() => ScanFolder.FilesToScan(Path.Combine(_root, "missing")));
    }
}
