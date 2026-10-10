using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;

namespace InventorAddin.Core.Tests.Numbering;

public sealed class PartRenamerTests : IDisposable
{
    private const string OldPath = @"C:\Cad\AWB-10001,plate.ipt";
    private const string NewPath = @"C:\Cad\AWB-10001,base_plate.ipt";

    private static readonly RenameRequest Request = new(OldPath, NewPath, "base plate");

    private readonly string _root = Path.Combine(Path.GetTempPath(), "InventorAddinTests", Guid.NewGuid().ToString("N"));
    private readonly FakeTarget _target = new();

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private PartRenamer Renamer(RegisterStore? store = null) => new(_target, store, NullLog.Instance);

    // The steps in order

    [Fact]
    public void Run_RenamesUpdatesReferencesSavesOpenFilesWritesDescriptionThenDeletesTheOldFile()
    {
        _target.Referencing.Add(@"C:\Cad\asm.iam");
        _target.Referencing.Add(@"C:\Cad\plate.idw");
        _target.Repointed.Add(new RenameFileResult(@"C:\Cad\open.iam"));

        var result = Renamer().Run(Request, [@"C:\Cad\asm.iam", @"C:\Cad\other.iam", @"C:\Cad\plate.idw"]);

        Assert.True(result.Renamed);
        Assert.True(result.OldFileDeleted);
        Assert.True(result.DescriptionWritten);
        Assert.False(result.HasProblems);
        Assert.Equal([@"C:\Cad\asm.iam", @"C:\Cad\plate.idw", @"C:\Cad\open.iam"], result.Files.Select(f => f.FullFileName));
        Assert.All(result.Files, f => Assert.True(f.Updated));
        Assert.Empty(result.Notes);
        Assert.Equal(
        [
            $"SaveAs {NewPath}",
            @"Update C:\Cad\asm.iam",
            @"Update C:\Cad\other.iam",
            @"Update C:\Cad\plate.idw",
            $"SaveRepointed {NewPath}",
            "Description base plate",
            $"Delete {OldPath}",
        ], _target.Calls);
    }

    [Fact]
    public void Run_SkipsTheRenamedFileItself_AsOldOrNewPath()
    {
        var oldAsm = @"C:\Cad\AWB-10002,frame.iam";
        var newAsm = @"C:\Cad\AWB-10002,main_frame.iam";

        Renamer().Run(new RenameRequest(oldAsm, newAsm, "main frame"), [@"c:\cad\awb-10002,FRAME.iam", newAsm, @"C:\Cad\top.iam"]);

        Assert.Equal([@"Update C:\Cad\top.iam"], _target.Calls.Where(c => c.StartsWith("Update", StringComparison.Ordinal)));
    }

    [Fact]
    public void Run_ReportsProgressForEachFile_ThenSavingOpenFiles()
    {
        var progress = new List<string>();

        Renamer().Run(Request, [@"C:\Cad\a.iam", @"C:\Cad\b.idw"], progress: progress.Add);

        Assert.Equal(["Checking 1 of 2: a.iam", "Checking 2 of 2: b.idw", PartRenamer.SavingOpenFilesProgress], progress);
    }

    [Fact]
    public void Run_AnOpenFileAlreadyUpdatedIsListedOnce()
    {
        _target.Referencing.Add(@"C:\Cad\asm.iam");
        _target.Repointed.Add(new RenameFileResult(@"c:\cad\ASM.iam"));

        var result = Renamer().Run(Request, [@"C:\Cad\asm.iam"]);

        Assert.Single(result.Files);
    }

    // Failures

    [Fact]
    public void Run_SaveAsFails_NothingElseIsDone()
    {
        _target.FailSaveAs = "the folder is read-only";

        var result = Renamer().Run(Request, [@"C:\Cad\asm.iam"]);

        Assert.False(result.Renamed);
        Assert.False(result.OldFileDeleted);
        Assert.True(result.HasProblems);
        var note = Assert.Single(result.Notes);
        Assert.Equal(PartRenamer.RenameFailedMessage("the folder is read-only"), note.Text);
        Assert.True(note.IsProblem);
        Assert.Equal([$"SaveAs {NewPath}"], _target.Calls);
    }

    [Fact]
    public void Run_AFileThatCannotBeUpdated_IsListedWithTheReason_TheRestGoOn_AndTheOldFileIsKept()
    {
        _target.Referencing.Add(@"C:\Cad\a.iam");
        _target.Referencing.Add(@"C:\Cad\c.idw");
        _target.Failing[@"C:\Cad\b.iam"] = "it is read-only";

        var result = Renamer().Run(Request, [@"C:\Cad\a.iam", @"C:\Cad\b.iam", @"C:\Cad\c.idw"]);

        Assert.True(result.Renamed);
        Assert.False(result.OldFileDeleted);
        Assert.Equal(2, result.UpdatedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(new RenameFileResult(@"C:\Cad\b.iam", "it is read-only"), result.Files.Single(f => !f.Updated));
        Assert.Contains(result.Notes, n => n.Text == PartRenamer.OldFileKeptMessage(OldPath) && n.IsProblem);
        Assert.DoesNotContain(_target.Calls, c => c.StartsWith("Delete", StringComparison.Ordinal));
        Assert.Contains("Description base plate", _target.Calls);
    }

    [Fact]
    public void Run_AnOpenFileThatCannotBeSaved_KeepsTheOldFile()
    {
        _target.Repointed.Add(new RenameFileResult(@"C:\Cad\open.iam", "it is locked"));

        var result = Renamer().Run(Request, []);

        Assert.False(result.OldFileDeleted);
        Assert.Equal(1, result.FailedCount);
        Assert.Contains(result.Notes, n => n.Text == PartRenamer.OldFileKeptMessage(OldPath));
    }

    [Fact]
    public void Run_SavingOpenFilesThrows_IsANote_AndTheOldFileIsKept()
    {
        _target.FailSaveRepointed = "no documents";

        var result = Renamer().Run(Request, []);

        Assert.False(result.OldFileDeleted);
        Assert.Contains(result.Notes, n => n.Text == PartRenamer.OpenFilesFailedMessage("no documents"));
        Assert.Contains(result.Notes, n => n.Text == PartRenamer.OldFileKeptMessage(OldPath));
    }

    [Fact]
    public void Run_DescriptionFails_IsANote_AndTheOldFileIsStillDeleted()
    {
        _target.FailDescription = "property locked";

        var result = Renamer().Run(Request, []);

        Assert.False(result.DescriptionWritten);
        Assert.True(result.OldFileDeleted);
        Assert.Contains(result.Notes, n => n.Text == PartRenamer.DescriptionFailedMessage("property locked") && n.IsProblem);
    }

    [Fact]
    public void Run_TheOldFileCannotBeDeleted_IsANote()
    {
        _target.FailDelete = "in use";

        var result = Renamer().Run(Request, []);

        Assert.True(result.Renamed);
        Assert.False(result.OldFileDeleted);
        Assert.Contains(result.Notes, n => n.Text == PartRenamer.OldFileNotDeletedMessage(OldPath, "in use") && n.IsProblem);
    }

    [Fact]
    public void Run_ASkippedSearchFolder_KeepsTheOldFile_AndSaysWhichFolder()
    {
        _target.Referencing.Add(@"C:\Cad\asm.iam");
        var skipped = new SkippedFolder(@"B:\Dropped", SkippedFolder.NotReachableReason);

        var result = Renamer().Run(Request, [@"C:\Cad\asm.iam"], skippedFolders: [skipped]);

        Assert.True(result.Renamed);
        Assert.False(result.OldFileDeleted);
        Assert.Equal(1, result.UpdatedCount);
        Assert.True(result.HasProblems);
        var note = Assert.Single(result.Notes);
        Assert.Equal(PartRenamer.OldFileKeptForSkippedFoldersMessage(OldPath, [skipped]), note.Text);
        Assert.Contains(@"B:\Dropped", note.Text, StringComparison.Ordinal);
        Assert.True(note.IsProblem);
        Assert.DoesNotContain(_target.Calls, c => c.StartsWith("Delete", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_NoSkippedFolders_DeletesTheOldFile()
    {
        var result = Renamer().Run(Request, [], skippedFolders: []);

        Assert.True(result.OldFileDeleted);
    }

    // The register

    private RegisterStore Store(bool reachable, out string shared, out string local)
    {
        shared = Path.Combine(_root, "nas");
        local = Path.Combine(_root, "local");
        Directory.CreateDirectory(shared);
        Directory.CreateDirectory(local);
        var entry = new RegisterEntry(
            new PartNumber(10001), "plate", "P", DocumentKind.Part, OldPath,
            new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), "tester", "TESTPC");
        File.WriteAllText(Path.Combine(shared, RegisterStore.SharedFileName), RegisterCsv.Write([entry]));
        File.WriteAllText(Path.Combine(local, RegisterStore.LocalFileName), RegisterCsv.Write([entry]));
        return new RegisterStore(shared, local, new FakeEnvironment(reachable), NullLog.Instance, TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void Run_WithARegisteredNumber_UpdatesTheRegisterEntry()
    {
        var store = Store(reachable: true, out var shared, out _);

        var result = Renamer(store).Run(Request, [], new PartNumber(10001));

        var entry = Assert.Single(RegisterCsv.Read(File.ReadAllText(Path.Combine(shared, RegisterStore.SharedFileName))).Entries);
        Assert.Equal(NewPath, entry.FilePath);
        Assert.Equal("base plate", entry.Name);
        var note = Assert.Single(result.Notes);
        Assert.Equal("Register entry AWB-10001 updated.", note.Text);
        Assert.False(note.IsProblem);
    }

    [Fact]
    public void Run_WithARegisteredNumber_Offline_SaysItWillBeSynced()
    {
        var store = Store(reachable: false, out _, out var local);

        var result = Renamer(store).Run(Request, [], new PartNumber(10001));

        Assert.True(File.Exists(Path.Combine(local, RegisterStore.PendingUpdatesFileName)));
        Assert.Contains("offline", Assert.Single(result.Notes).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_RegisterBusy_QueuesTheUpdate_AndSaysSo_ThenTheNextSyncAppliesIt()
    {
        var store = Store(reachable: true, out var shared, out var local);
        var lockPath = Path.Combine(shared, RegisterStore.LockFileName);
        File.WriteAllText(lockPath, "other PC");
        File.SetLastWriteTimeUtc(lockPath, FakeEnvironment.Clock.UtcDateTime);

        var result = Renamer(store).Run(Request, [], new PartNumber(10001));

        var note = Assert.Single(result.Notes);
        Assert.True(note.IsProblem);
        Assert.Contains("applied at the next sync", note.Text, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(local, RegisterStore.PendingUpdatesFileName)));

        File.Delete(lockPath);
        store.Sync();
        var entry = Assert.Single(RegisterCsv.Read(File.ReadAllText(Path.Combine(shared, RegisterStore.SharedFileName))).Entries);
        Assert.Equal(NewPath, entry.FilePath);
    }

    [Fact]
    public void Run_WithoutANumber_DoesNotTouchTheRegister()
    {
        var store = Store(reachable: true, out var shared, out _);
        var before = File.ReadAllBytes(Path.Combine(shared, RegisterStore.SharedFileName));

        var result = Renamer(store).Run(Request, []);

        Assert.Empty(result.Notes);
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(shared, RegisterStore.SharedFileName)));
    }

    [Fact]
    public void Run_SaveAsFails_DoesNotTouchTheRegister()
    {
        var store = Store(reachable: true, out var shared, out _);
        var before = File.ReadAllBytes(Path.Combine(shared, RegisterStore.SharedFileName));
        _target.FailSaveAs = "no";

        Renamer(store).Run(Request, [], new PartNumber(10001));

        Assert.Equal(before, File.ReadAllBytes(Path.Combine(shared, RegisterStore.SharedFileName)));
    }

    private sealed class FakeTarget : IRenameTarget
    {
        public List<string> Calls { get; } = [];
        public HashSet<string> Referencing { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Failing { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<RenameFileResult> Repointed { get; } = [];
        public string? FailSaveAs { get; set; }
        public string? FailSaveRepointed { get; set; }
        public string? FailDescription { get; set; }
        public string? FailDelete { get; set; }

        public void SaveAs(string newFullFileName)
        {
            Calls.Add($"SaveAs {newFullFileName}");
            if (FailSaveAs is not null)
                throw new InvalidOperationException(FailSaveAs);
        }

        public bool UpdateReferences(string fullFileName, string oldFullFileName, string newFullFileName)
        {
            Calls.Add($"Update {fullFileName}");
            if (Failing.TryGetValue(fullFileName, out var reason))
                throw new IOException(reason);
            return Referencing.Contains(fullFileName);
        }

        public IReadOnlyList<RenameFileResult> SaveRepointedDocuments(string newFullFileName)
        {
            Calls.Add($"SaveRepointed {newFullFileName}");
            if (FailSaveRepointed is not null)
                throw new InvalidOperationException(FailSaveRepointed);
            return Repointed;
        }

        public void WriteDescription(string partName)
        {
            Calls.Add($"Description {partName}");
            if (FailDescription is not null)
                throw new InvalidOperationException(FailDescription);
        }

        public void DeleteFile(string fullFileName)
        {
            Calls.Add($"Delete {fullFileName}");
            if (FailDelete is not null)
                throw new IOException(FailDelete);
        }
    }

    private sealed class FakeEnvironment(bool reachable) : IRegisterEnvironment
    {
        public static readonly DateTimeOffset Clock = new(2026, 10, 10, 14, 3, 0, TimeSpan.Zero);

        public DateTimeOffset Now => Clock;

        public string AssignedBy => "tester";

        public string ComputerName => "TESTPC";

        public bool ProbeFolder(string folder, TimeSpan timeout) => reachable && Directory.Exists(folder);
    }
}
