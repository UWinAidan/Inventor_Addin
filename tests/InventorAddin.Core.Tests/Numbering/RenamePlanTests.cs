using InventorAddin.Core.Models;
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
    [InlineData(@"C:\Work\mounting_bracket,AWB-10001.ipt", "base plate", @"C:\Work\BASE_PLATE,AWB-10001.ipt")]
    [InlineData(@"C:\Work\mounting_bracket,awb-10001.ipt", "base plate", @"C:\Work\BASE_PLATE,AWB-10001.ipt")]
    [InlineData(@"C:\Work\frame,AWB-10002.iam", "  main   frame ", @"C:\Work\MAIN_FRAME,AWB-10002.iam")]
    [InlineData(@"C:\Work\frame,AWB-10002.IAM", "frame, welded", @"C:\Work\FRAME,_WELDED,AWB-10002.IAM")]
    [InlineData(@"C:\Old\Old_bracket.v2.ipt", "new bracket", @"C:\Old\NEW_BRACKET.ipt")]
    [InlineData(@"C:\Old\a,b.ipt", "c", @"C:\Old\C.ipt")]
    [InlineData(@"C:\Old\b,AWB-1.ipt", "c", @"C:\Old\C.ipt")]
    [InlineData(@"C:\Old\AWB-10005,BRACKET.ipt", "bracket", @"C:\Old\BRACKET.ipt")]
    [InlineData("/home/test/plate,AWB-10003.ipt", "big plate", "/home/test/BIG_PLATE,AWB-10003.ipt")]
    public void NewFileName_KeepsFolderExtensionAndNumber(string oldPath, string newName, string expected)
    {
        Assert.Equal(expected, RenamePlan.NewFileName(oldPath, newName));
    }

    // The number kept (spec 02: the registered Part Number first, then the number in the old file name)

    [Fact]
    public void NewFileName_AFileNumberedByGenerate_WhoseNameHasNoNumber_GetsItsRegisteredNumber()
    {
        Assert.Equal(
            @"C:\Old\NEW_BRACKET,AWB-10007.ipt",
            RenamePlan.NewFileName(@"C:\Old\old_bracket.ipt", "new bracket", new PartNumber(10007)));
    }

    [Fact]
    public void NewFileName_ANameCarryingANumber_WithABlankPartNumber_KeepsTheNumberInTheName()
    {
        // Part Number blank, so not registered: the caller passes null.
        Assert.Equal(
            @"C:\Work\BASE_PLATE,AWB-10001.ipt",
            RenamePlan.NewFileName(@"C:\Work\plate,AWB-10001.ipt", "base plate", registeredNumber: null));
    }

    [Fact]
    public void NewFileName_TheRegisteredNumberWins_OverADifferentNumberInTheName()
    {
        Assert.Equal(
            @"C:\Work\BASE_PLATE,AWB-10005.ipt",
            RenamePlan.NewFileName(@"C:\Work\plate,AWB-10001.ipt", "base plate", new PartNumber(10005)));
    }

    [Theory]
    [InlineData(DocumentKind.Assembly)]
    [InlineData(DocumentKind.WeldmentAssembly)]
    public void NewFileName_Assembly_GetsThePrefixOnce(DocumentKind kind)
    {
        Assert.Equal(
            @"C:\Work\ASSY_NEW_GEARBOX,AWB-10006.iam",
            RenamePlan.NewFileName(@"C:\Work\GEARBOX,AWB-10006.iam", "NEW GEARBOX", null, kind));
        Assert.Equal(
            @"C:\Work\ASSY_NEW_GEARBOX,AWB-10006.iam",
            RenamePlan.NewFileName(@"C:\Work\GEARBOX,AWB-10006.iam", "assy_new gearbox", null, kind));
    }

    [Fact]
    public void NewFileName_Part_IsNotPrefixed()
    {
        Assert.Equal(
            @"C:\Work\NEW_PLATE,AWB-10001.ipt",
            RenamePlan.NewFileName(@"C:\Work\PLATE,AWB-10001.ipt", "new plate", null, DocumentKind.Part));
    }

    [Fact]
    public void ValidateNewName_Assembly_OnlyThePrefix_IsRefused()
    {
        Assert.Equal(
            PartFileName.AssemblyNameMissingMessage,
            RenamePlan.ValidateNewName(@"C:\Work\GEARBOX,AWB-10006.iam", "ASSY_", null, DocumentKind.Assembly));
    }

    [Fact]
    public void ValidateNewName_Assembly_AddingThePrefix_IsARename()
    {
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Work\GEARBOX,AWB-10006.iam", "GEARBOX", null, DocumentKind.Assembly));
        Assert.Equal(
            RenamePlan.SameFileMessage,
            RenamePlan.ValidateNewName(@"C:\Work\ASSY_GEARBOX,AWB-10006.iam", "GEARBOX", null, DocumentKind.Assembly));
    }

    [Fact]
    public void NumberToKeep_RegisteredThenFileNameThenNone()
    {
        Assert.Equal(new PartNumber(10007), RenamePlan.NumberToKeep(@"C:\Old\plate.ipt", new PartNumber(10007)));
        Assert.Equal(new PartNumber(10007), RenamePlan.NumberToKeep(@"C:\W\plate,AWB-10001.ipt", new PartNumber(10007)));
        Assert.Equal(new PartNumber(10001), RenamePlan.NumberToKeep(@"C:\W\plate,AWB-10001.ipt", null));
        Assert.Null(RenamePlan.NumberToKeep(@"C:\Old\plate.ipt", null));
        Assert.Null(RenamePlan.NumberToKeep(@"C:\Old\a,b.ipt", null));
    }

    [Fact]
    public void NumberToKeep_AnOldNumberFirstFile_HasNoNumberInItsName()
    {
        Assert.Null(RenamePlan.NumberToKeep(@"C:\Old\AWB-10005,BRACKET.ipt", null));
        Assert.Equal(new PartNumber(10005), RenamePlan.NumberToKeep(@"C:\Old\AWB-10005,BRACKET.ipt", new PartNumber(10005)));
    }

    [Fact]
    public void NewFileName_AnOldNumberFirstFile_WithItsRegisteredNumber_GetsTheNewOrder()
    {
        Assert.Equal(
            @"C:\Old\BRACKET,AWB-10005.ipt",
            RenamePlan.NewFileName(@"C:\Old\AWB-10005,BRACKET.ipt", "BRACKET", new PartNumber(10005)));
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Old\AWB-10005,BRACKET.ipt", "BRACKET", new PartNumber(10005)));
    }

    [Fact]
    public void ValidateNewName_CommaAllowed_WithOrWithoutANumber()
    {
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Old\plate.ipt", "plate, big", new PartNumber(10007)));
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Old\plate.ipt", "plate, big", null));
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Old\plate.ipt", "A,B", null));
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Work\plate,AWB-10001.ipt", "plate, big", null));
    }

    [Fact]
    public void ValidateNewName_KeepingTheNameOfAGenerateNumberedFile_IsARename_ThatAddsTheNumber()
    {
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Old\PLATE.ipt", "plate", new PartNumber(10007)));
        Assert.Equal(RenamePlan.SameFileMessage, RenamePlan.ValidateNewName(@"C:\Old\PLATE.ipt", "plate", null));
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
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Work\plate,AWB-10001.ipt", "base plate"));
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
    public void ValidateNewName_ANameEndingInANumber_IsRefusedOnlyWhenTheFileNameHasNoNumber()
    {
        Assert.Equal(RenamePlan.NameEndsWithNumberMessage, RenamePlan.ValidateNewName(@"C:\Old\plate.ipt", "plate,AWB-10009"));
        Assert.Equal(RenamePlan.NameEndsWithNumberMessage, RenamePlan.ValidateNewName(@"C:\Old\a,b.ipt", "a,b,awb-10009"));
        Assert.Equal(RenamePlan.NameEndsWithNumberMessage, RenamePlan.ValidateNewName(@"C:\Old\AWB-10005,BRACKET.ipt", "BRACKET,AWB-10005"));
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Old\plate.ipt", "plate,AWB-10009", new PartNumber(10007)));
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Work\plate,AWB-10001.ipt", "plate,AWB-10009"));
    }

    [Theory]
    [InlineData("AWB-10009,plate")]
    [InlineData("plate, AWB-10009")]
    [InlineData("plate,AWB-1")]
    [InlineData("plate,big")]
    public void ValidateNewName_ACommaNotFollowedByANumber_IsAcceptedWithoutANumber(string typed)
    {
        Assert.Null(RenamePlan.ValidateNewName(@"C:\Old\plate.ipt", typed));
    }

    [Fact]
    public void NameEndsWithNumberMessage_SaysWhy()
    {
        Assert.Contains("comma and a part number", RenamePlan.NameEndsWithNumberMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("first comma", RenamePlan.NameEndsWithNumberMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(@"C:\Work\MOUNTING_BRACKET,AWB-10001.ipt", "mounting bracket")]
    [InlineData(@"C:\Work\MOUNTING_BRACKET,AWB-10001.ipt", "mounting_bracket")]
    [InlineData(@"C:\Work\MOUNTING_BRACKET,AWB-10001.ipt", "Mounting Bracket")]
    [InlineData(@"C:\Old\PLATE.ipt", " plate ")]
    public void ValidateNewName_ExactlyTheCurrentName_IsRefused(string oldPath, string typed)
    {
        Assert.Equal(RenamePlan.SameFileMessage, RenamePlan.ValidateNewName(oldPath, typed));
    }

    [Theory]
    [InlineData(@"C:\Work\mounting_bracket,AWB-10001.ipt", "mounting bracket")]
    [InlineData(@"C:\Work\mounting_bracket,AWB-10001.ipt", "mounting_bracket")]
    [InlineData(@"C:\Work\Mounting_Bracket,AWB-10001.ipt", "MOUNTING BRACKET")]
    [InlineData(@"C:\Work\MOUNTING_BRACKET,awb-10001.ipt", "mounting bracket")]
    [InlineData(@"C:\Old\plate.ipt", " plate ")]
    public void ValidateNewName_AChangeOfCaseOnly_IsARename(string oldPath, string typed)
    {
        Assert.Null(RenamePlan.ValidateNewName(oldPath, typed));
        Assert.True(RenamePlan.IsCaseOnlyChange(oldPath, RenamePlan.NewFileName(oldPath, typed)));
    }

    [Fact]
    public void SameFileMessage_SaysItIsTheCurrentName_AndNoLongerMentionsCase()
    {
        Assert.Contains("current name", RenamePlan.SameFileMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("case", RenamePlan.SameFileMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(@"C:\Cad\plate.ipt", @"C:\Cad\PLATE.ipt", true)]
    [InlineData(@"C:\Cad\plate.ipt", @" C:\Cad\PLATE.ipt ", true)]
    [InlineData(@"C:\Cad\plate.ipt", @"C:\Cad\plate.ipt", false)]
    [InlineData(@"C:\Cad\plate.ipt", @"C:\Cad\base_plate.ipt", false)]
    [InlineData(@"C:\Cad\plate.ipt", @"C:\Cad\plate.iam", false)]
    [InlineData("", "", false)]
    [InlineData(null, @"C:\Cad\PLATE.ipt", false)]
    public void IsCaseOnlyChange(string? oldPath, string? newPath, bool expected)
    {
        Assert.Equal(expected, RenamePlan.IsCaseOnlyChange(oldPath, newPath));
    }

    [Fact]
    public void TemporaryFileName_SameFolderAndExtension_AndNotAPartFileName()
    {
        var guid = new Guid("0123456789abcdef0123456789abcdef");

        var temp = RenamePlan.TemporaryFileName(@"\\nas\cad\Sub\mounting_bracket,AWB-10001.ipt", _ => false, () => guid);

        Assert.Equal(@"\\nas\cad\Sub\~RENAME-0123456789abcdef0123456789abcdef.ipt", temp);
        Assert.True(RenamePlan.IsTemporaryFileName(temp));
        Assert.False(PartFileName.TryReadNumber(temp, out _));
        Assert.DoesNotContain(',', RenamePlan.FileNameOnly(temp));
    }

    [Fact]
    public void TemporaryFileName_SkipsANameThatIsTaken()
    {
        var guids = new Queue<Guid>([new Guid("11111111111111111111111111111111"), new Guid("22222222222222222222222222222222")]);
        var asked = new List<string>();

        var temp = RenamePlan.TemporaryFileName(
            @"C:\Cad\plate.iam", p => { asked.Add(p); return p.Contains("1111", StringComparison.Ordinal); }, guids.Dequeue);

        Assert.Equal(@"C:\Cad\~RENAME-22222222222222222222222222222222.iam", temp);
        Assert.Equal(2, asked.Count);
    }

    [Fact]
    public void TemporaryFileName_EveryNameTaken_Throws()
    {
        var calls = 0;

        Assert.Throws<IOException>(() => RenamePlan.TemporaryFileName(@"C:\Cad\plate.ipt", _ => { calls++; return true; }));
        Assert.Equal(RenamePlan.TemporaryNameAttempts, calls);
    }

    [Fact]
    public void TemporaryFileName_RealGuids_AreDifferent()
    {
        Assert.NotEqual(
            RenamePlan.TemporaryFileName(@"C:\Cad\plate.ipt", _ => false),
            RenamePlan.TemporaryFileName(@"C:\Cad\plate.ipt", _ => false));
    }

    [Theory]
    [InlineData(@"C:\Cad\~RENAME-abc.ipt", true)]
    [InlineData(@"C:\Cad\~rename-abc.ipt", true)]
    [InlineData(@"C:\~RENAME-\plate.ipt", false)]
    [InlineData(@"C:\Cad\plate.ipt", false)]
    [InlineData(null, false)]
    public void IsTemporaryFileName(string? path, bool expected)
    {
        Assert.Equal(expected, RenamePlan.IsTemporaryFileName(path));
    }

    [Fact]
    public void ConfirmationMessage_ACaseOnlyChange_SaysItGoesThroughATemporaryName()
    {
        var search = new RenameSearchList([], [@"C:\Cad"], []);

        var caseOnly = RenamePlan.ConfirmationMessage(
            new RenameRequest(@"C:\Cad\plate.ipt", @"C:\Cad\PLATE.ipt", "PLATE"), search);
        var other = RenamePlan.ConfirmationMessage(
            new RenameRequest(@"C:\Cad\plate.ipt", @"C:\Cad\BASE_PLATE.ipt", "BASE PLATE"), search);

        Assert.Contains("Only the case of the name changes", caseOnly, StringComparison.Ordinal);
        Assert.Contains("temporary name", caseOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("temporary name", other, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateNewName_PathTooLong()
    {
        var folder = @"C:\" + new string('f', 230);
        var problem = RenamePlan.ValidateNewName(folder + @"\plate,AWB-10001.ipt", new string('n', 40));

        Assert.Equal(PartFileName.ValidatePath(folder + @"\", new string('n', 40) + ",AWB-10001.ipt"), problem);
        Assert.NotNull(problem);
    }

    [Fact]
    public void TargetExistsMessage_NamesTheFile()
    {
        Assert.Contains("plate,AWB-10001.ipt", RenamePlan.TargetExistsMessage("plate,AWB-10001.ipt"), StringComparison.Ordinal);
    }

    // SearchFolders

    [Fact]
    public void SearchFolders_WorkFolderThenOthers_TrimmedWithoutBlanks()
    {
        var folders = RenamePlan.SearchFolders(@" C:\Cad\ ", [@"D:\More", "", "  ", null, @"E:\Old\"], @"C:\Cad\plate,AWB-10001.ipt");

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

    [Fact]
    public void FilesToSearch_TheRenamedFilesFolderAndItsPrintsFolder_AreAlwaysSearched()
    {
        // Task 059: the model's own folder is always listed (or a folder holding it), with its subfolders, so the
        // drawings Rename renames, beside the model or in Prints, are always among the files found.
        var model = Path.Combine(_root, "job", "plate,AWB-10001.ipt");
        var beside = MakeFile("job", "plate,AWB-10001.idw");
        var print = MakeFile("job", "Prints", "plate,AWB-10001.dwg");
        var otherCad = Path.Combine(_root, "cad");
        Directory.CreateDirectory(otherCad);

        var folders = RenamePlan.SearchFolders(otherCad, [], model);
        var files = RenamePlan.FilesToSearch(folders);

        Assert.Equal([otherCad, Path.Combine(_root, "job")], folders);
        Assert.Equal(new[] { beside, print }.OrderBy(f => f, StringComparer.OrdinalIgnoreCase), files);
        Assert.Equal(2, RenamePlan.DrawingsOfModel(model, Path.Combine(_root, "job", "base,AWB-10001.ipt"), files).Count);
    }

    [Fact]
    public void SearchFolders_AModelInsideACadFolder_IsCoveredWithItsPrintsFolder()
    {
        var folders = RenamePlan.SearchFolders(@"C:\Cad", [], @"C:\Cad\Job\plate.ipt");

        Assert.Equal([@"C:\Cad"], folders);
    }

    // DrawingsOfModel (task 059)

    [Fact]
    public void DrawingsOfModel_PrintsBesideTheModelAndInPrints_GetTheNewNameWithTheirOwnExtension()
    {
        var drawings = RenamePlan.DrawingsOfModel(
            @"C:\Cad\plate,AWB-10001.ipt",
            @"C:\Cad\BASE_PLATE,AWB-10001.ipt",
            [
                @"C:\Cad\plate,AWB-10001.idw",
                @"C:\Cad\Prints\plate,AWB-10001.dwg",
                @"C:\Cad\PRINTS\PLATE,awb-10001.IDW",
                @"C:\Cad\asm.iam",
            ]);

        Assert.Equal(
            [
                new DrawingRename(@"C:\Cad\plate,AWB-10001.idw", @"C:\Cad\BASE_PLATE,AWB-10001.idw"),
                new DrawingRename(@"C:\Cad\Prints\plate,AWB-10001.dwg", @"C:\Cad\Prints\BASE_PLATE,AWB-10001.dwg"),
                new DrawingRename(@"C:\Cad\PRINTS\PLATE,awb-10001.IDW", @"C:\Cad\PRINTS\BASE_PLATE,AWB-10001.IDW"),
            ],
            drawings);
    }

    [Theory]
    [InlineData(@"C:\Cad\Other\plate,AWB-10001.dwg")]
    [InlineData(@"C:\Elsewhere\Prints\plate,AWB-10001.dwg")]
    [InlineData(@"C:\Cad\Prints\Sub\plate,AWB-10001.dwg")]
    [InlineData(@"C:\Cad\OldVersions\plate,AWB-10001.dwg")]
    [InlineData(@"C:\Cad\Prints\OldVersions\plate,AWB-10001.0001.dwg")]
    [InlineData(@"C:\Cad\plate,AWB-10001 copy.dwg")]
    [InlineData(@"C:\Cad\plate.dwg")]
    [InlineData(@"C:\Cad\plate,AWB-10001.iam")]
    [InlineData(@"C:\Cad\plate,AWB-10001.ipn")]
    [InlineData(@"C:\Cad\plate,AWB-10001.ipt")]
    [InlineData("")]
    [InlineData(null)]
    public void DrawingsOfModel_AnythingElse_IsNotADrawingOfTheModel(string? file)
    {
        Assert.Empty(RenamePlan.DrawingsOfModel(@"C:\Cad\plate,AWB-10001.ipt", @"C:\Cad\base,AWB-10001.ipt", [file]));
    }

    [Fact]
    public void DrawingsOfModel_EachDrawingOnce_InTheOrderGiven()
    {
        var drawings = RenamePlan.DrawingsOfModel(
            @"C:\Cad\plate.ipt",
            @"C:\Cad\base.ipt",
            [@"C:\Cad\Prints\plate.dwg", @"C:\Cad\plate.dwg", @"c:\cad\prints\PLATE.dwg"]);

        Assert.Equal([@"C:\Cad\Prints\plate.dwg", @"C:\Cad\plate.dwg"], drawings.Select(d => d.OldFullFileName));
    }

    [Fact]
    public void DrawingsOfModel_AnAssemblysDrawings_FollowItsNewName()
    {
        var drawing = Assert.Single(RenamePlan.DrawingsOfModel(
            @"\\nas\cad\ASSY_FRAME,AWB-10002.iam",
            @"\\nas\cad\ASSY_MAIN_FRAME,AWB-10002.iam",
            [@"\\nas\cad\Prints\ASSY_FRAME,AWB-10002.dwg"]));

        Assert.Equal(@"\\nas\cad\Prints\ASSY_MAIN_FRAME,AWB-10002.dwg", drawing.NewFullFileName);
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
        var request = new RenameRequest(@"C:\Cad\plate,AWB-10001.ipt", @"C:\Cad\base_plate,AWB-10001.ipt", "base plate");
        var search = new RenameSearchList(
            [@"C:\Cad\a.iam", @"C:\Cad\b.idw", @"D:\More\c.iam"],
            [@"C:\Cad", @"D:\More"],
            [new SkippedFolder(@"B:\Dropped", SkippedFolder.NotReachableReason)]);

        var text = RenamePlan.ConfirmationMessage(request, search);

        Assert.Contains("Rename plate,AWB-10001.ipt to base_plate,AWB-10001.ipt?", text, StringComparison.Ordinal);
        Assert.Contains("3 assemblies, drawings and presentations in 2 folders will be searched", text, StringComparison.Ordinal);
        Assert.Contains(@"- C:\Cad", text, StringComparison.Ordinal);
        Assert.Contains(@"- D:\More", text, StringComparison.Ordinal);
        Assert.Contains(@"- B:\Dropped: " + SkippedFolder.NotReachableReason, text, StringComparison.Ordinal);
        Assert.Contains("cannot be undone with Undo", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ConfirmationMessage_SaysTheDrawingsNamedAfterItAreRenamed_AndListsThem()
    {
        var request = new RenameRequest(@"C:\Cad\plate,AWB-10001.ipt", @"C:\Cad\BASE_PLATE,AWB-10001.ipt", "base plate");
        var withDrawing = new RenameSearchList(
            [@"C:\Cad\a.iam", @"C:\Cad\Prints\plate,AWB-10001.dwg"], [@"C:\Cad"], []);
        var without = new RenameSearchList([@"C:\Cad\a.iam"], [@"C:\Cad"], []);

        var text = RenamePlan.ConfirmationMessage(request, withDrawing);

        Assert.Contains("Drawings named after it that use it are renamed to match", text, StringComparison.Ordinal);
        Assert.Contains("- plate,AWB-10001.dwg to BASE_PLATE,AWB-10001.dwg", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Drawings named after it", RenamePlan.ConfirmationMessage(request, without), StringComparison.Ordinal);
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

    // RenamedOccurrence (task 058)

    private const string OldBracket = @"C:\Cad\BRACKET,AWB-10005.ipt";
    private const string NewBracket = @"C:\Cad\BRACKET_2,AWB-10005.ipt";

    [Theory]
    [InlineData("BRACKET,AWB-10005:2", "BRACKET_2,AWB-10005:2")]
    [InlineData("BRACKET,AWB-10005:1", "BRACKET_2,AWB-10005:1")]
    [InlineData("BRACKET,AWB-10005:12", "BRACKET_2,AWB-10005:12")]
    [InlineData("BRACKET,AWB-10005:007", "BRACKET_2,AWB-10005:007")]
    [InlineData("bracket,awb-10005:3", "BRACKET_2,AWB-10005:3")]
    public void RenamedOccurrence_TheOldFileNameAColonAndDigits_GetsTheNewNameAndTheSameDigits(string name, string expected)
    {
        Assert.Equal(expected, RenamePlan.RenamedOccurrence(name, OldBracket, NewBracket));
    }

    [Theory]
    [InlineData("BRACKET,AWB-10005:2a")]
    [InlineData("BRACKET,AWB-10005 copy:1")]
    [InlineData("BRACKET,AWB-10005:")]
    [InlineData("BRACKET,AWB-10005")]
    [InlineData("BRACKET,AWB-10005:-1")]
    [InlineData("BRACKET,AWB-10005: 1")]
    [InlineData("BRACKET,AWB-10005:1:2")]
    [InlineData("MY BRACKET")]
    [InlineData("XBRACKET,AWB-10005:1")]
    [InlineData("BRACKET:1")]
    [InlineData("")]
    [InlineData(null)]
    public void RenamedOccurrence_AnyOtherName_IsLeftAlone(string? name)
    {
        Assert.Null(RenamePlan.RenamedOccurrence(name, OldBracket, NewBracket));
    }

    [Fact]
    public void RenamedOccurrence_ANameWithSeveralCommasAndDots_UsesTheWholeFileNameWithoutTheExtension()
    {
        Assert.Equal(
            "BASE,PLATE.V2_NEW,AWB-10001:4",
            RenamePlan.RenamedOccurrence("base,plate.v2,AWB-10001:4", @"C:\Cad\base,plate.v2,AWB-10001.ipt", @"C:\Cad\BASE,PLATE.V2_NEW,AWB-10001.ipt"));
    }

    [Fact]
    public void RenamedOccurrence_AnAssembly_KeepsItsExtensionOutOfTheName()
    {
        Assert.Equal(
            "ASSY_MAIN_FRAME,AWB-10002:1",
            RenamePlan.RenamedOccurrence("frame,AWB-10002:1", "/cad/frame,AWB-10002.iam", "/cad/ASSY_MAIN_FRAME,AWB-10002.iam"));
    }

    [Theory]
    [InlineData(null, NewBracket)]
    [InlineData(OldBracket, null)]
    [InlineData("", NewBracket)]
    public void RenamedOccurrence_NoFileName_IsNull(string? oldPath, string? newPath)
    {
        Assert.Null(RenamePlan.RenamedOccurrence("BRACKET,AWB-10005:1", oldPath, newPath));
    }

    // FileOfDocumentName

    [Theory]
    [InlineData(@"C:\Cad\a.ipt", @"C:\Cad\a.ipt")]
    [InlineData(@"C:\Cad\a.ipt<Default>", @"C:\Cad\a.ipt")]
    [InlineData(@"C:\Cad\a.ipt<Model State1>", @"C:\Cad\a.ipt")]
    [InlineData(@" C:\Cad\a.ipt ", @"C:\Cad\a.ipt")]
    [InlineData(null, "")]
    public void FileOfDocumentName_DropsAModelState(string? name, string expected)
    {
        Assert.Equal(expected, RenamePlan.FileOfDocumentName(name));
    }
}
