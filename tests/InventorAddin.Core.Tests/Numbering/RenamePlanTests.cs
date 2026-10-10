using InventorAddin.Core.Numbering;

namespace InventorAddin.Core.Tests.Numbering;

public sealed class RenamePlanTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "InventorAddinTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string MakeFile(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "test");
        return path;
    }

    // NewFileName

    [Theory]
    [InlineData(@"C:\Work\AWB-10001,mounting_bracket.ipt", "base plate", @"C:\Work\AWB-10001,base_plate.ipt")]
    [InlineData(@"C:\Work\awb-10001,mounting_bracket.ipt", "base plate", @"C:\Work\AWB-10001,base_plate.ipt")]
    [InlineData(@"C:\Work\AWB-10002,frame.iam", "  main   frame ", @"C:\Work\AWB-10002,main_frame.iam")]
    [InlineData(@"C:\Work\AWB-10002,frame.IAM", "frame, welded", @"C:\Work\AWB-10002,frame,_welded.IAM")]
    [InlineData(@"C:\Old\Old_bracket.v2.ipt", "new bracket", @"C:\Old\new_bracket.ipt")]
    [InlineData(@"C:\Old\a,b.ipt", "c", @"C:\Old\c.ipt")]
    [InlineData(@"C:\Old\AWB-1,b.ipt", "c", @"C:\Old\c.ipt")]
    [InlineData("/home/test/AWB-10003,plate.ipt", "big plate", "/home/test/AWB-10003,big_plate.ipt")]
    public void NewFileName_KeepsFolderExtensionAndNumber(string oldPath, string newName, string expected)
    {
        Assert.Equal(expected, RenamePlan.NewFileName(oldPath, newName));
    }

    // The number kept (spec 02: the registered Part Number first, then the number in the old file name)

    [Fact]
    public void NewFileName_AFileNumberedByGenerate_WhoseNameHasNoNumber_GetsItsRegisteredNumber()
    {
        Assert.Equal(
            @"C:\Old\AWB-10007,new_bracket.ipt",
            RenamePlan.NewFileName(@"C:\Old\old_bracket.ipt", "new bracket", new PartNumber(10007)));
    }

    [Fact]
    public void NewFileName_ANameCarryingANumber_WithABlankPartNumber_KeepsTheNumberInTheName()
    {
        // Part Number blank, so not registered: the caller passes null.
        Assert.Equal(
            @"C:\Work\AWB-10001,base_plate.ipt",
            RenamePlan.NewFileName(@"C:\Work\AWB-10001,plate.ipt", "base plate", registeredNumber: null));
    }

    [Fact]
    public void NewFileName_TheRegisteredNumberWins_OverADifferentNumberInTheName()
    {
        Assert.Equal(
            @"C:\Work\AWB-10005,base_plate.ipt",
            RenamePlan.NewFileName(@"C:\Work\AWB-10001,plate.ipt", "base plate", new PartNumber(10005)));
    }

    [Fact]
    public void NumberToKeep_RegisteredThenFileNameThenNone()
    {
        Assert.Equal(new PartNumber(10007), RenamePlan.NumberToKeep(@"C:\Old\plate.ipt", new PartNumber(10007)));
        Assert.Equal(new PartNumber(10007), RenamePlan.NumberToKeep(@"C:\W\AWB-10001,plate.ipt", new PartNumber(10007)));
        Assert.Equal(new PartNumber(10001), RenamePlan.NumberToKeep(@"C:\W\AWB-10001,plate.ipt", null));
        Assert.Null(RenamePlan.NumberToKeep(@"C:\Old\plate.ipt", null));
        Assert.Null(RenamePlan.NumberToKeep(@"C:\Old\a,b.ipt", null));
    }

    [Fact]
    public void ValidateNewName_CommaAllowedForAGenerateNumberedFile_RefusedWhenTheResultHasNoNumber()
    {
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Old\plate.ipt", "plate, big", new PartNumber(10007)));
        Assert.Equal(RenamePlan.CommaWithoutNumberMessage, RenamePlan.ValidateNewName(@"C:\Old\plate.ipt", "plate, big", null));
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Work\AWB-10001,plate.ipt", "plate, big", null));
    }

    [Fact]
    public void ValidateNewName_KeepingTheNameOfAGenerateNumberedFile_IsARename_ThatAddsTheNumber()
    {
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Old\plate.ipt", "plate", new PartNumber(10007)));
        Assert.Equal(RenamePlan.SameFileMessage, RenamePlan.ValidateNewName(@"C:\Old\plate.ipt", "plate", null));
    }

    [Theory]
    [InlineData(@"C:\Work\plate.ipt", "a/b")]
    [InlineData(@"C:\Work\plate.ipt", "  ")]
    [InlineData(@"C:\Work\plate", "new")]
    [InlineData(" ", "new")]
    public void NewFileName_RefusesWhatCannotBeBuilt(string oldPath, string newName)
    {
        Assert.Throws<ArgumentException>(() => RenamePlan.NewFileName(oldPath, newName));
    }

    // ValidateNewName

    [Fact]
    public void ValidateNewName_AcceptsANewName()
    {
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Work\AWB-10001,plate.ipt", "base plate"));
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Old\plate.ipt", "base plate"));
    }

    [Fact]
    public void ValidateNewName_NotSaved()
    {
        Assert.Equal(RenamePlan.NotSavedMessage, RenamePlan.ValidateNewName("", "plate"));
        Assert.Equal(RenamePlan.NotSavedMessage, RenamePlan.ValidateNewName(null, "plate"));
    }

    [Fact]
    public void ValidateNewName_NameProblemsComeFromPartFileName()
    {
        Assert.Equal(PartFileName.ValidateName("a:b"), RenamePlan.ValidateNewName(@"C:\Work\plate.ipt", "a:b"));
        Assert.Equal(PartFileName.ValidateName(""), RenamePlan.ValidateNewName(@"C:\Work\plate.ipt", ""));
        Assert.Equal(PartFileName.ValidateName("CON"), RenamePlan.ValidateNewName(@"C:\Work\plate.ipt", "CON"));
    }

    [Fact]
    public void ValidateNewName_CommaRefusedOnlyWhenTheFileNameHasNoNumber()
    {
        Assert.Equal(RenamePlan.CommaWithoutNumberMessage, RenamePlan.ValidateNewName(@"C:\Old\plate.ipt", "plate, big"));
        Assert.Equal(RenamePlan.CommaWithoutNumberMessage, RenamePlan.ValidateNewName(@"C:\Old\a,b.ipt", "plate, big"));
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Work\AWB-10001,plate.ipt", "plate, big"));
    }

    [Theory]
    [InlineData(@"C:\Work\AWB-10001,mounting_bracket.ipt", "mounting bracket")]
    [InlineData(@"C:\Work\AWB-10001,mounting_bracket.ipt", "mounting_bracket")]
    [InlineData(@"C:\Work\AWB-10001,mounting_bracket.ipt", "Mounting Bracket")]
    [InlineData(@"C:\Old\plate.ipt", " plate ")]
    public void ValidateNewName_SameFileIsRefused(string oldPath, string typed)
    {
        Assert.Equal(RenamePlan.SameFileMessage, RenamePlan.ValidateNewName(oldPath, typed));
    }

    [Fact]
    public void ValidateNewName_PathTooLong()
    {
        var folder = @"C:\" + new string('f', 230);
        var problem = RenamePlan.ValidateNewName(folder + @"\AWB-10001,plate.ipt", new string('n', 40));

        Assert.Equal(PartFileName.ValidatePath(folder + @"\", "AWB-10001," + new string('n', 40) + ".ipt"), problem);
        Assert.NotNull(problem);
    }

    [Fact]
    public void TargetExistsMessage_NamesTheFile()
    {
        Assert.Contains("AWB-10001,plate.ipt", RenamePlan.TargetExistsMessage("AWB-10001,plate.ipt"), StringComparison.Ordinal);
    }

    // SearchFolders

    [Fact]
    public void SearchFolders_WorkFolderThenOthers_TrimmedWithoutBlanks()
    {
        var folders = RenamePlan.SearchFolders(@" C:\Cad\ ", [@"D:\More", "", "  ", null, @"E:\Old\"], @"C:\Cad\AWB-10001,plate.ipt");

        Assert.Equal([@"C:\Cad", @"D:\More", @"E:\Old"], folders);
    }

    [Fact]
    public void SearchFolders_DuplicatesIgnoringCaseAndSlashes_AreRemoved()
    {
        var folders = RenamePlan.SearchFolders(@"C:\Cad", [@"c:\CAD\", "C:/Cad", @"D:\More", @"d:\more"], @"C:\Cad\plate.ipt");

        Assert.Equal([@"C:\Cad", @"D:\More"], folders);
    }

    [Fact]
    public void SearchFolders_AFolderInsideAnotherListedFolder_IsRemoved_WhereverItIsListed()
    {
        var folders = RenamePlan.SearchFolders(@"C:\Cad\Jobs", [@"D:\More\Sub", @"C:\Cad", @"D:\More"], @"C:\Cad\plate.ipt");

        Assert.Equal([@"C:\Cad", @"D:\More"], folders);
    }

    [Fact]
    public void SearchFolders_ASiblingWithTheSamePrefix_IsNotInside()
    {
        var folders = RenamePlan.SearchFolders(@"C:\Cad", [@"C:\Cad2", @"C:\Cad 2"], @"C:\Cad\plate.ipt");

        Assert.Equal([@"C:\Cad", @"C:\Cad2", @"C:\Cad 2"], folders);
    }

    [Fact]
    public void SearchFolders_TheRenamedFilesFolderIsAdded_OnlyWhenItLiesInNone()
    {
        Assert.Equal(
            [@"C:\Cad", @"F:\Elsewhere\Job"],
            RenamePlan.SearchFolders(@"C:\Cad", [], @"F:\Elsewhere\Job\plate.ipt"));
        Assert.Equal(
            [@"C:\Cad"],
            RenamePlan.SearchFolders(@"C:\Cad", [], @"c:\cad\jobs\deep\plate.ipt"));
    }

    [Fact]
    public void SearchFolders_NoSettings_IsTheRenamedFilesFolderOnly()
    {
        Assert.Equal([@"C:\Job"], RenamePlan.SearchFolders(null, null, @"C:\Job\plate.ipt"));
        Assert.Equal([@"C:\Job"], RenamePlan.SearchFolders("", [""], @"C:\Job\plate.ipt"));
        Assert.Empty(RenamePlan.SearchFolders(null, null, null));
    }

    [Fact]
    public void SearchFolders_ADriveRootKeepsItsSeparator_AndHoldsEverythingOnTheDrive()
    {
        Assert.Equal([@"D:\"], RenamePlan.SearchFolders(@"D:\", [@"D:\Cad"], @"D:\Cad\plate.ipt"));
    }

    // CandidateFiles

    [Fact]
    public void CandidateFiles_AssembliesDrawingsPresentations_OnceEach_Sorted_SkippingOldVersions()
    {
        var files = RenamePlan.CandidateFiles(
        [
            @"C:\Cad\z.iam",
            @"C:\Cad\a.IDW",
            @"C:\Cad\b.dwg",
            @"C:\Cad\c.ipn",
            @"C:\Cad\d.ipt",
            @"C:\Cad\e.iam.bak",
            @"C:\Cad\OldVersions\f.iam",
            @"C:\Cad\Sub\oldversions\g.idw",
            @"C:\Cad\OldVersions.iam",
            @"c:\cad\Z.IAM",
            null,
            "",
        ]);

        Assert.Equal([@"C:\Cad\a.IDW", @"C:\Cad\b.dwg", @"C:\Cad\c.ipn", @"C:\Cad\OldVersions.iam", @"C:\Cad\z.iam"], files);
    }

    // FilesToSearch

    [Fact]
    public void FilesToSearch_ListsCandidatesUnderEachFolder_IncludingSubfolders_SkippingOldVersionsAndMissingFolders()
    {
        var asm = MakeFile("cad", "asm.iam");
        var drawing = MakeFile("cad", "sub", "drawing.idw");
        var dwg = MakeFile("cad", "sub", "deeper", "print.dwg");
        var ipn = MakeFile("more", "show.ipn");
        MakeFile("cad", "part.ipt");
        MakeFile("cad", "OldVersions", "asm.0001.iam");
        MakeFile("cad", "sub", "oldversions", "drawing.0001.idw");

        var files = RenamePlan.FilesToSearch(
        [
            Path.Combine(_root, "cad"),
            Path.Combine(_root, "more"),
            Path.Combine(_root, "missing"),
            Path.Combine(_root, "cad"),
        ]);

        Assert.Equal(new[] { asm, drawing, dwg, ipn }.OrderBy(f => f, StringComparer.OrdinalIgnoreCase), files);
    }

    // ListFilesToSearch

    [Fact]
    public void ListFilesToSearch_RealFolders_ListsThemAndSkipsAMissingOne()
    {
        var asm = MakeFile("cad", "asm.iam");
        var missing = Path.Combine(_root, "missing");

        var search = RenamePlan.ListFilesToSearch([Path.Combine(_root, "cad"), missing]);

        Assert.Equal([asm], search.Files);
        Assert.Equal([Path.Combine(_root, "cad")], search.Folders);
        var skipped = Assert.Single(search.Skipped);
        Assert.Equal(missing, skipped.Folder);
        Assert.Equal(SkippedFolder.NotReachableReason, skipped.Reason);
    }

    [Fact]
    public void ListFilesToSearch_AFolderThatDoesNotAnswer_IsSkippedAfterTheProbeTimeout()
    {
        using var hold = new ManualResetEventSlim();
        try
        {
            var search = RenamePlan.ListFilesToSearch(
                [@"B:\dropped", @"C:\Cad"],
                // Long enough that the folder that answers at once is never late on a busy test machine.
                probeTimeout: TimeSpan.FromSeconds(1),
                folderExists: f => f == @"C:\Cad" || hold.Wait(TimeSpan.FromSeconds(10)),
                listFolder: _ => [@"C:\Cad\asm.iam"]);

            Assert.Equal([@"C:\Cad\asm.iam"], search.Files);
            Assert.Equal([@"C:\Cad"], search.Folders);
            Assert.Equal([new SkippedFolder(@"B:\dropped", SkippedFolder.NotReachableReason)], search.Skipped);
        }
        finally
        {
            hold.Set();
        }
    }

    [Fact]
    public void ListFilesToSearch_ASlowListing_IsSkippedAfterTheListTimeout()
    {
        using var hold = new ManualResetEventSlim();
        try
        {
            var timeout = TimeSpan.FromMilliseconds(50);
            var search = RenamePlan.ListFilesToSearch(
                [@"C:\Huge"],
                listTimeout: timeout,
                folderExists: _ => true,
                listFolder: _ =>
                {
                    hold.Wait(TimeSpan.FromSeconds(10));
                    return [@"C:\Huge\asm.iam"];
                });

            Assert.Empty(search.Files);
            Assert.Equal([new SkippedFolder(@"C:\Huge", SkippedFolder.TooSlowReason(timeout))], search.Skipped);
        }
        finally
        {
            hold.Set();
        }
    }

    [Fact]
    public void ListFilesToSearch_AListingThatFails_IsSkippedWithTheReason()
    {
        var search = RenamePlan.ListFilesToSearch(
            [@"C:\Locked"],
            folderExists: _ => true,
            listFolder: _ => throw new UnauthorizedAccessException("access denied"));

        var skipped = Assert.Single(search.Skipped);
        Assert.Contains("access denied", skipped.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ListFilesToSearch_FiltersAndMergesWhatTheFoldersGive()
    {
        var search = RenamePlan.ListFilesToSearch(
            [@"C:\A", @"C:\B", " "],
            folderExists: _ => true,
            listFolder: f => f == @"C:\A"
                ? [@"C:\A\x.iam", @"C:\A\p.ipt"]
                : [@"c:\a\X.IAM", @"C:\B\y.idw"]);

        Assert.Equal([@"C:\A\x.iam", @"C:\B\y.idw"], search.Files);
        Assert.Equal([@"C:\A", @"C:\B"], search.Folders);
    }

    // Confirmation and progress

    [Fact]
    public void ConfirmationMessage_NamesBothFiles_TheCounts_TheFolders_AndThatUndoCannotRevertIt()
    {
        var request = new RenameRequest(@"C:\Cad\AWB-10001,plate.ipt", @"C:\Cad\AWB-10001,base_plate.ipt", "base plate");
        var search = new RenameSearchList(
            [@"C:\Cad\a.iam", @"C:\Cad\b.idw", @"D:\More\c.iam"],
            [@"C:\Cad", @"D:\More"],
            [new SkippedFolder(@"B:\Dropped", SkippedFolder.NotReachableReason)]);

        var text = RenamePlan.ConfirmationMessage(request, search);

        Assert.Contains("Rename AWB-10001,plate.ipt to AWB-10001,base_plate.ipt?", text, StringComparison.Ordinal);
        Assert.Contains("3 assemblies, drawings and presentations in 2 folders will be searched", text, StringComparison.Ordinal);
        Assert.Contains(@"- C:\Cad", text, StringComparison.Ordinal);
        Assert.Contains(@"- D:\More", text, StringComparison.Ordinal);
        Assert.Contains(@"- B:\Dropped: " + SkippedFolder.NotReachableReason, text, StringComparison.Ordinal);
        Assert.Contains("cannot be undone with Undo", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfirmationMessage_OneFileInOneFolder_AndNone()
    {
        var request = new RenameRequest(@"C:\Cad\plate.ipt", @"C:\Cad\base.ipt", "base");

        var one = RenamePlan.ConfirmationMessage(request, new RenameSearchList([@"C:\Cad\a.iam"], [@"C:\Cad"], []));
        var none = RenamePlan.ConfirmationMessage(request, new RenameSearchList([], [@"C:\Cad"], []));

        Assert.Contains("1 assembly, drawing or presentation in 1 folder will be searched", one, StringComparison.Ordinal);
        Assert.Contains("only the file is renamed", none, StringComparison.Ordinal);
        Assert.DoesNotContain("Not searched", one, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsavedOpenFilesMessage_NamesEachFile()
    {
        var text = RenamePlan.UnsavedOpenFilesMessage([@"C:\Cad\top.iam", @"C:\Cad\plate.idw"]);

        Assert.Contains("save or close them first", text, StringComparison.Ordinal);
        Assert.EndsWith(Environment.NewLine + "- top.iam" + Environment.NewLine + "- plate.idw", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProgressText_CountsAndNamesTheFile()
    {
        Assert.Equal("Checking 3 of 12: asm.iam", RenamePlan.ProgressText(3, 12, @"C:\Cad\asm.iam"));
    }

    [Theory]
    [InlineData(@"C:\Cad\asm.iam", "asm.iam")]
    [InlineData("/cad/asm.iam", "asm.iam")]
    [InlineData("asm.iam", "asm.iam")]
    [InlineData(null, "")]
    public void FileNameOnly(string? path, string expected)
    {
        Assert.Equal(expected, RenamePlan.FileNameOnly(path));
    }
}
