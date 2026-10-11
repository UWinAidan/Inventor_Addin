using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.Numbering;

namespace InventorAddin.Core.Tests.Numbering;

public sealed class PartRenamerTests : IDisposable
{
    private const string OldPath = @"C:\Cad\plate,AWB-10001.ipt";
    private const string NewPath = @"C:\Cad\base_plate,AWB-10001.ipt";

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
        _target.Open.Add(new FakeDoc(@"C:\Cad\open.iam", OldPath));

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
            $"CheckOpen {OldPath}",
            $"Delete {OldPath}",
        ], _target.Calls);
    }

    [Fact]
    public void Run_SkipsTheRenamedFileItself_AsOldOrNewPath()
    {
        var oldAsm = @"C:\Cad\frame,AWB-10002.iam";
        var newAsm = @"C:\Cad\main_frame,AWB-10002.iam";

        Renamer().Run(new RenameRequest(oldAsm, newAsm, "main frame"), [@"c:\cad\FRAME,awb-10002.iam", newAsm, @"C:\Cad\top.iam"]);

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
        var open = new FakeDoc(@"c:\cad\ASM.iam", OldPath);
        _target.Open.Add(open);

        var result = Renamer().Run(Request, [@"C:\Cad\asm.iam"]);

        Assert.Single(result.Files);
        Assert.Equal(1, open.Saves);
        Assert.Equal(NewPath, open.OnDisk);
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
        _target.Open.Add(new FakeDoc(@"C:\Cad\open.iam", OldPath) { FailSave = "it is locked" });

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

    // Never breaking a reference (task 058, spike 052)

    [Fact]
    public void Run_AnOpenDocumentRepointedButNotDirty_OutsideTheSearch_IsSavedWithTheNewReference()
    {
        var drawing = new FakeDoc(@"D:\Elsewhere\plate.dwg", OldPath);
        _target.Open.Add(drawing);

        var result = Renamer().Run(Request, []);

        Assert.Equal(NewPath, drawing.OnDisk);
        Assert.False(drawing.Dirty);
        Assert.Equal(@"D:\Elsewhere\plate.dwg", Assert.Single(result.Files).FullFileName);
        Assert.True(result.OldFileDeleted);
    }

    [Fact]
    public void Run_AnOpenDocumentInTheSearch_RepointedButNotDirty_IsUpdatedAndSaved()
    {
        var drawing = new FakeDoc(@"C:\Cad\Prints\plate.dwg", OldPath);
        _target.Open.Add(drawing);

        var result = Renamer().Run(Request, [@"C:\Cad\Prints\plate.dwg"]);

        Assert.Equal(NewPath, drawing.OnDisk);
        Assert.Equal(1, drawing.Saves);
        Assert.Single(result.Files);
        Assert.True(result.OldFileDeleted);
    }

    [Fact]
    public void Run_AClosedFileWithTheOldReference_IsUpdated()
    {
        _target.Referencing.Add(@"C:\Cad\frame.iam");

        var result = Renamer().Run(Request, [@"C:\Cad\frame.iam"]);

        Assert.Equal(NewPath, _target.ClosedOnDisk[@"C:\Cad\frame.iam"]);
        Assert.Equal(1, result.UpdatedCount);
        Assert.True(result.OldFileDeleted);
    }

    [Fact]
    public void Run_AFileThatDoesNotUseIt_IsNotListed()
    {
        _target.Open.Add(new FakeDoc(@"C:\Cad\unrelated.iam", @"C:\Cad\other.ipt"));

        var result = Renamer().Run(Request, [@"C:\Cad\unrelated.iam", @"C:\Cad\closed.iam"]);

        Assert.Empty(result.Files);
        Assert.Equal(0, _target.Open[0].Saves);
    }

    [Fact]
    public void Run_AnOpenDocumentStillReferencingTheOldFile_KeepsTheOldFile_AndNamesIt()
    {
        _target.StillUsingOld.Add(@"C:\Cad\broken.idw");

        var result = Renamer().Run(Request, []);

        Assert.False(result.OldFileDeleted);
        Assert.True(result.HasProblems);
        var note = Assert.Single(result.Notes);
        Assert.Equal(PartRenamer.OldFileStillUsedMessage(OldPath, [@"C:\Cad\broken.idw"]), note.Text);
        Assert.Contains("broken.idw", note.Text, StringComparison.Ordinal);
        Assert.True(note.IsProblem);
        Assert.DoesNotContain(_target.Calls, c => c.StartsWith("Delete", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_TheOpenFilesCannotBeChecked_KeepsTheOldFile()
    {
        _target.FailOpenCheck = "Inventor is busy";

        var result = Renamer().Run(Request, []);

        Assert.False(result.OldFileDeleted);
        Assert.Contains(result.Notes, n => n.Text == PartRenamer.OldFileCheckFailedMessage(OldPath, "Inventor is busy") && n.IsProblem);
        Assert.DoesNotContain(_target.Calls, c => c.StartsWith("Delete", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_AFileFailed_DoesNotAskAboutOpenFiles_TheOldFileIsKeptAnyway()
    {
        _target.Failing[@"C:\Cad\b.iam"] = "read-only";

        Renamer().Run(Request, [@"C:\Cad\b.iam"]);

        Assert.DoesNotContain(_target.Calls, c => c.StartsWith("CheckOpen", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_PassesEverySearchedFileAsHandled_SoAnOpenOneIsNotSavedTwice()
    {
        var open = new FakeDoc(@"C:\Cad\asm.iam", OldPath);
        _target.Open.Add(open);
        var outside = new FakeDoc(@"D:\Other\top.iam", OldPath);
        _target.Open.Add(outside);

        var result = Renamer().Run(Request, [@"C:\Cad\asm.iam"]);

        Assert.Equal(1, open.Saves);
        Assert.Equal(1, outside.Saves);
        Assert.Equal([@"C:\Cad\asm.iam", @"D:\Other\top.iam"], result.Files.Select(f => f.FullFileName));
    }

    // Occurrences

    [Fact]
    public void Run_CountsTheOccurrencesRenamed_InSearchedAndOpenFiles()
    {
        _target.Referencing.Add(@"C:\Cad\asm.iam");
        _target.Occurrences[@"C:\Cad\asm.iam"] = new RenameFileUpdate(2);
        _target.Open.Add(new FakeDoc(@"D:\Other\top.iam", OldPath));
        _target.Occurrences[@"D:\Other\top.iam"] = new RenameFileUpdate(1);

        var result = Renamer().Run(Request, [@"C:\Cad\asm.iam"]);

        Assert.Equal(3, result.OccurrencesRenamed);
        Assert.Equal(2, result.Files.Single(f => f.FullFileName == @"C:\Cad\asm.iam").OccurrencesRenamed);
        Assert.False(result.HasProblems);
    }

    [Fact]
    public void Run_AnOccurrenceThatCannotBeRenamed_IsANote_AndTheRenameGoesOn()
    {
        _target.Referencing.Add(@"C:\Cad\asm.iam");
        var problem = new OccurrenceProblem("plate,AWB-10001:3", "the name is taken");
        _target.Occurrences[@"C:\Cad\asm.iam"] = new RenameFileUpdate(1, [problem]);

        var result = Renamer().Run(Request, [@"C:\Cad\asm.iam"]);

        Assert.True(result.OldFileDeleted);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(1, result.OccurrencesRenamed);
        var note = Assert.Single(result.Notes);
        Assert.Equal(PartRenamer.OccurrenceNotRenamedMessage(@"C:\Cad\asm.iam", problem), note.Text);
        Assert.Contains("plate,AWB-10001:3", note.Text, StringComparison.Ordinal);
        Assert.True(note.IsProblem);
    }

    // The log

    [Fact]
    public void Run_TheLastLogLine_SaysFilesUpdatedOccurrencesRenamedAndWhetherTheOldFileWasDeleted()
    {
        _target.Referencing.Add(@"C:\Cad\asm.iam");
        _target.Occurrences[@"C:\Cad\asm.iam"] = new RenameFileUpdate(2);
        var log = new ListLog();

        new PartRenamer(_target, null, log).Run(Request, [@"C:\Cad\asm.iam"]);

        var last = log.Lines[^1];
        Assert.Contains("1 updated", last, StringComparison.Ordinal);
        Assert.Contains("2 occurrences renamed", last, StringComparison.Ordinal);
        Assert.Contains("old file deleted", last, StringComparison.Ordinal);
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

    /// <summary>A document open in the fake Inventor: its direct reference in memory and what its file holds on disk.</summary>
    private sealed class FakeDoc(string path, string reference)
    {
        public string Path { get; } = path;
        public string Reference { get; set; } = reference;
        public string OnDisk { get; set; } = reference;
        public bool Dirty { get; set; }
        public int Saves { get; set; }
        public string? FailSave { get; init; }

        /// <summary>Inventor's Save (spike 052): writes nothing for a document that is not dirty.</summary>
        public void Save()
        {
            if (FailSave is not null)
                throw new IOException(FailSave);
            Saves++;
            if (Dirty)
            {
                OnDisk = Reference;
                Dirty = false;
            }
        }
    }

    /// <summary>
    /// Inventor as spike 052 found it: <see cref="SaveAs"/> repoints each open document that used the old file in
    /// memory and leaves it not dirty; each update marks the document dirty before it saves.
    /// </summary>
    private sealed class FakeTarget : IRenameTarget
    {
        public List<string> Calls { get; } = [];

        /// <summary>Closed files that reference the old file on disk.</summary>
        public HashSet<string> Referencing { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The reference each closed file holds on disk after it was updated.</summary>
        public Dictionary<string, string> ClosedOnDisk { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Failing { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<FakeDoc> Open { get; } = [];

        /// <summary>What renaming occurrences gives in each file; none renamed when a file is not listed.</summary>
        public Dictionary<string, RenameFileUpdate> Occurrences { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Open documents that still reference the old file whatever Rename does (for example a broken one).</summary>
        public List<string> StillUsingOld { get; } = [];

        public string? FailSaveAs { get; set; }
        public string? FailSaveRepointed { get; set; }
        public string? FailOpenCheck { get; set; }
        public string? FailDescription { get; set; }
        public string? FailDelete { get; set; }

        public void SaveAs(string newFullFileName)
        {
            Calls.Add($"SaveAs {newFullFileName}");
            if (FailSaveAs is not null)
                throw new InvalidOperationException(FailSaveAs);

            foreach (var doc in Open.Where(d => Same(d.Reference, OldPath)))
                doc.Reference = newFullFileName;
        }

        public RenameFileUpdate? UpdateReferences(string fullFileName, string oldFullFileName, string newFullFileName)
        {
            Calls.Add($"Update {fullFileName}");
            if (Failing.TryGetValue(fullFileName, out var reason))
                throw new IOException(reason);

            var open = Open.FirstOrDefault(d => Same(d.Path, fullFileName));
            if (open is not null)
                return Update(open, oldFullFileName, newFullFileName);

            if (!Referencing.Contains(fullFileName))
                return null;

            ClosedOnDisk[fullFileName] = newFullFileName;
            return OccurrencesOf(fullFileName);
        }

        public IReadOnlyList<RenameFileResult> SaveRepointedDocuments(
            string oldFullFileName,
            string newFullFileName,
            IReadOnlyCollection<string> alreadyHandled)
        {
            Calls.Add($"SaveRepointed {newFullFileName}");
            if (FailSaveRepointed is not null)
                throw new InvalidOperationException(FailSaveRepointed);

            var results = new List<RenameFileResult>();
            foreach (var doc in Open.Where(d => !alreadyHandled.Contains(d.Path, StringComparer.OrdinalIgnoreCase)))
            {
                try
                {
                    if (Update(doc, oldFullFileName, newFullFileName) is { } u)
                        results.Add(new RenameFileResult(doc.Path, null, u.OccurrencesRenamed, u.OccurrenceProblems));
                }
                catch (IOException ex)
                {
                    results.Add(new RenameFileResult(doc.Path, ex.Message));
                }
            }

            return results;
        }

        public IReadOnlyList<string> OpenDocumentsReferencing(string oldFullFileName)
        {
            Calls.Add($"CheckOpen {oldFullFileName}");
            if (FailOpenCheck is not null)
                throw new InvalidOperationException(FailOpenCheck);

            return Open.Where(d => Same(d.Reference, oldFullFileName)).Select(d => d.Path).Concat(StillUsingOld).ToList();
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

        private RenameFileUpdate? Update(FakeDoc doc, string oldFullFileName, string newFullFileName)
        {
            if (!Same(doc.Reference, oldFullFileName) && !Same(doc.Reference, newFullFileName))
                return null;

            doc.Reference = newFullFileName;
            doc.Dirty = true;
            doc.Save();
            return OccurrencesOf(doc.Path);
        }

        private RenameFileUpdate OccurrencesOf(string file) =>
            Occurrences.TryGetValue(file, out var update) ? update : new RenameFileUpdate();

        private static bool Same(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ListLog : ILog
    {
        public List<string> Lines { get; } = [];
        public void Info(string message) => Lines.Add(message);
        public void Warn(string message) => Lines.Add(message);
        public void Error(string message, Exception? ex = null) => Lines.Add(message);
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
