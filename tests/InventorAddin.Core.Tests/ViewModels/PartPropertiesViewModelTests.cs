using System.ComponentModel;
using System.Globalization;
using InventorAddin.Core.Logging;
using InventorAddin.Core.Models;
using InventorAddin.Core.PartProperties;
using InventorAddin.Core.ViewModels;

namespace InventorAddin.Core.Tests.ViewModels;

public sealed class PartPropertiesViewModelTests
{
    private const string DefaultDesigner = "Test Designer";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    private sealed class FakeTarget : IPropertyWriteTarget
    {
        public List<IReadOnlyList<PropertyWrite>> Calls { get; } = new();
        public Exception? ThrowOnApply { get; set; }

        public void Apply(IReadOnlyList<PropertyWrite> writes)
        {
            Calls.Add(writes);
            if (ThrowOnApply is not null)
                throw ThrowOnApply;
        }
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Infos { get; } = new();
        public List<(string Message, Exception? Ex)> Errors { get; } = new();

        public void Info(string message) => Infos.Add(message);
        public void Warn(string message) { }
        public void Error(string message, Exception? ex = null) => Errors.Add((message, ex));
    }

    /// <summary>A part whose every field already matches what the window shows: no pending changes.</summary>
    private static PartPropertiesSnapshot Complete() => new()
    {
        FullFileName = @"C:\Work\Bracket.ipt",
        DocumentKind = DocumentKind.Part,
        PartNumber = "PN-0001",
        Description = "Bracket",
        PartType = "M",
        Designer = "Alex Example",
        Detailer = "Sam Sample",
        Cost = 12.5m,
        Material = "Test Steel",
        Finish = "Test Paint",
        WeightDisplay = "1.234 kg",
    };

    /// <summary>A part with every editable field blank.</summary>
    private static PartPropertiesSnapshot Blank(DocumentKind kind = DocumentKind.Part) => new()
    {
        FullFileName = @"C:\Work\Bracket.ipt",
        DocumentKind = kind,
        Description = "Bracket",
    };

    private static PartPropertiesViewModel Create(
        PartPropertiesSnapshot snapshot,
        FakeTarget? target = null,
        string? defaultDesigner = DefaultDesigner,
        CultureInfo? culture = null,
        ILog? log = null) =>
        new(snapshot, target ?? new FakeTarget(), defaultDesigner, culture ?? Invariant, log);

    private static List<string> RecordPropertyChanges(INotifyPropertyChanged source)
    {
        var names = new List<string>();
        source.PropertyChanged += (_, e) => names.Add(e.PropertyName!);
        return names;
    }

    // Construction and display

    [Fact]
    public void Constructor_RejectsNulls()
    {
        var target = new FakeTarget();
        Assert.Throws<ArgumentNullException>(() => new PartPropertiesViewModel(null!, target, null, Invariant));
        Assert.Throws<ArgumentNullException>(() => new PartPropertiesViewModel(Complete(), null!, null, Invariant));
        Assert.Throws<ArgumentNullException>(() => new PartPropertiesViewModel(Complete(), target, null, null!));
    }

    [Fact]
    public void ShowsReadOnlyValues()
    {
        var vm = Create(Complete());

        Assert.Equal("Bracket.ipt", vm.FileName);
        Assert.Equal("PN-0001", vm.PartNumber);
        Assert.Equal("Bracket", vm.PartName);
        Assert.Equal("Test Steel", vm.Material);
        Assert.Equal("Test Paint", vm.Finish);
        Assert.Equal("1.234 kg", vm.Weight);
    }

    [Fact]
    public void ShowsFileValuesInEditableFields()
    {
        var vm = Create(Complete());

        Assert.Equal("M", vm.PartType);
        Assert.Equal("Manufactured", vm.PartTypeFullName);
        Assert.Equal("Alex Example", vm.Designer);
        Assert.Equal("Sam Sample", vm.Detailer);
        Assert.Equal("12.50", vm.CostText);
        Assert.True(vm.IsEditable);
    }

    [Fact]
    public void BlankValuesShowAsEmptyStrings_MissingWeightShowsDash()
    {
        var vm = Create(new PartPropertiesSnapshot { EditBlock = EditBlock.ReadOnlyFile });

        Assert.Equal(PartPropertiesViewModel.NotSavedFileName, vm.FileName);
        Assert.Equal("Not saved yet", vm.FileName);
        Assert.Equal(string.Empty, vm.PartNumber);
        Assert.Equal(string.Empty, vm.PartName);
        Assert.Equal(string.Empty, vm.Material);
        Assert.Equal(string.Empty, vm.Finish);
        Assert.Equal("-", vm.Weight);
        Assert.Equal(string.Empty, vm.PartType);
        Assert.Equal(string.Empty, vm.Designer);
        Assert.Equal(string.Empty, vm.Detailer);
        Assert.Equal(string.Empty, vm.CostText);
    }

    [Theory]
    [InlineData("/home/work/Bracket.v2.ipt", "Bracket.v2.ipt")]
    [InlineData(@"C:\Work\Frame.iam", "Frame.iam")]
    [InlineData("   ", "Not saved yet")]
    public void FileName_IsTheNameWithExtension(string fullFileName, string expected)
    {
        var vm = Create(Blank() with { FullFileName = fullFileName });

        Assert.Equal(expected, vm.FileName);
    }

    [Fact]
    public void PartTypeOptions_BlankThenTheList()
    {
        var vm = Create(Complete());

        Assert.Equal(PartTypes.All.Count + 1, vm.PartTypeOptions.Count);
        Assert.Equal(PartTypeOption.Blank, vm.PartTypeOptions[0]);
        Assert.Equal(string.Empty, vm.PartTypeOptions[0].Code);
        Assert.Equal(string.Empty, vm.PartTypeOptions[0].DisplayText);
        Assert.Equal(PartTypes.All.Select(t => t.Code), vm.PartTypeOptions.Skip(1).Select(o => o.Code));
        Assert.Equal("PM: Purchased, modified", vm.PartTypeOptions.Single(o => o.Code == "PM").DisplayText);
    }

    [Fact]
    public void UnknownPartTypeCode_IsKeptAsIs_AndAddedAsAnOption()
    {
        var target = new FakeTarget();
        var vm = Create(Complete() with { PartType = "XQ" }, target);

        Assert.Equal("XQ", vm.PartType);
        Assert.Equal(PartTypes.UnknownCodeText, vm.PartTypeFullName);
        Assert.Equal(PartTypes.All.Count + 2, vm.PartTypeOptions.Count);
        var extra = vm.PartTypeOptions[^1];
        Assert.Equal("XQ", extra.Code);
        Assert.Equal("XQ: Unknown code", extra.DisplayText);
        Assert.False(vm.HasChanges);

        vm.OkCommand.Execute(null);
        Assert.Empty(target.Calls);
    }

    [Fact]
    public void UnknownPartTypeCode_CanBeChangedAndChosenAgain()
    {
        var vm = Create(Complete() with { PartType = "XQ" });

        vm.PartType = "P";
        Assert.True(vm.HasChanges);
        Assert.Equal("Purchased", vm.PartTypeFullName);

        vm.PartType = "XQ";
        Assert.False(vm.HasChanges);
        Assert.Equal("XQ", vm.PartTypeOptions[^1].Code);
    }

    [Fact]
    public void PartTypeInADifferentCase_ShowsTheListedCode_AndIsWrittenInTheListedSpelling()
    {
        var target = new FakeTarget();
        var vm = Create(Complete() with { PartType = " m " }, target);

        Assert.Equal("M", vm.PartType);
        Assert.Equal("Manufactured", vm.PartTypeFullName);
        Assert.Equal(PartTypes.All.Count + 1, vm.PartTypeOptions.Count);
        Assert.True(vm.HasChanges);
        Assert.Equal(PartPropertiesViewModel.PreFillNotice, vm.StatusText);

        vm.ApplyCommand.Execute(null);

        var write = Assert.Single(Assert.Single(target.Calls));
        Assert.Equal(PartPropertyStorage.PartType, write.Location);
        Assert.Equal(PropertyWriteAction.Set, write.Action);
        Assert.Equal("M", write.TextValue);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void PartTypeWithOnlySurroundingWhitespace_IsNotAChange()
    {
        var vm = Create(Complete() with { PartType = " PM " });

        Assert.Equal("PM", vm.PartType);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void PartTypeSetter_UsesTheListedSpelling()
    {
        var vm = Create(Complete());

        vm.PartType = "pm";
        Assert.Equal("PM", vm.PartType);

        vm.PartType = null!;
        Assert.Equal(string.Empty, vm.PartType);
        Assert.Equal(string.Empty, vm.PartTypeFullName);
    }

    [Fact]
    public void PartType_RaisesPartTypeAndFullName()
    {
        var vm = Create(Complete());
        var changes = RecordPropertyChanges(vm);

        vm.PartType = "F";

        Assert.Contains(nameof(PartPropertiesViewModel.PartType), changes);
        Assert.Contains(nameof(PartPropertiesViewModel.PartTypeFullName), changes);
        Assert.Contains(nameof(PartPropertiesViewModel.HasChanges), changes);
        Assert.Equal("Fastener", vm.PartTypeFullName);
    }

    [Theory]
    [InlineData(nameof(PartPropertiesViewModel.Designer))]
    [InlineData(nameof(PartPropertiesViewModel.Detailer))]
    [InlineData(nameof(PartPropertiesViewModel.CostText))]
    public void EditableFields_RaisePropertyChangedAndState(string property)
    {
        var vm = Create(Complete());
        var changes = RecordPropertyChanges(vm);
        var applyChanged = 0;
        var okChanged = 0;
        vm.ApplyCommand.CanExecuteChanged += (_, _) => applyChanged++;
        vm.OkCommand.CanExecuteChanged += (_, _) => okChanged++;

        typeof(PartPropertiesViewModel).GetProperty(property)!.SetValue(vm, "7");

        Assert.Contains(property, changes);
        Assert.Contains(nameof(PartPropertiesViewModel.HasChanges), changes);
        Assert.Contains(nameof(PartPropertiesViewModel.StatusText), changes);
        Assert.Contains(nameof(PartPropertiesViewModel.IsValid), changes);
        Assert.True(applyChanged > 0);
        Assert.True(okChanged > 0);
        Assert.True(vm.HasChanges);
    }

    [Fact]
    public void SettingTheSameValue_RaisesNothing()
    {
        var vm = Create(Complete());
        var changes = RecordPropertyChanges(vm);

        vm.Designer = "Alex Example";
        vm.CostText = "12.50";
        vm.PartType = "M";

        Assert.Empty(changes);
    }

    // Pre-fills

    [Fact]
    public void BlankDesigner_IsPreFilledWithTheTrimmedDefault()
    {
        var vm = Create(Complete() with { Designer = "  " }, defaultDesigner: "  Test Designer ");

        Assert.Equal("Test Designer", vm.Designer);
        Assert.True(vm.HasChanges);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankDesigner_WithNoDefault_StaysBlank(string? defaultDesigner)
    {
        var vm = Create(Complete() with { Designer = null }, defaultDesigner: defaultDesigner);

        Assert.Equal(string.Empty, vm.Designer);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void FileDesigner_IsNotReplacedByTheDefault()
    {
        var vm = Create(Complete());

        Assert.Equal("Alex Example", vm.Designer);
    }

    [Theory]
    [InlineData(DocumentKind.Assembly, "A", "Assembly")]
    [InlineData(DocumentKind.WeldmentAssembly, "W", "Weldment")]
    public void BlankPartType_IsPreFilledForAssembliesAndWeldments(DocumentKind kind, string code, string fullName)
    {
        var vm = Create(Complete() with { DocumentKind = kind, PartType = null });

        Assert.Equal(code, vm.PartType);
        Assert.Equal(fullName, vm.PartTypeFullName);
        Assert.True(vm.HasChanges);
        Assert.Equal(PartPropertiesViewModel.PreFillNotice, vm.StatusText);
    }

    [Theory]
    [InlineData(DocumentKind.Part)]
    [InlineData(DocumentKind.SheetMetalPart)]
    public void BlankPartType_StaysBlankForParts(DocumentKind kind)
    {
        var vm = Create(Complete() with { DocumentKind = kind, PartType = "" });

        Assert.Equal(string.Empty, vm.PartType);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void PreFilledPartType_CanBeChanged()
    {
        var vm = Create(Complete() with { DocumentKind = DocumentKind.Assembly, PartType = null });

        vm.PartType = "P";

        Assert.Equal("P", vm.PartType);
        Assert.True(vm.HasChanges);
    }

    [Fact]
    public void BlankDetailer_FollowsDesignerThroughSeveralEdits_ThenStops()
    {
        var vm = Create(Blank());

        Assert.True(vm.DetailerFollowsDesigner);
        Assert.Equal(DefaultDesigner, vm.Detailer);

        vm.Designer = "First Person";
        Assert.Equal("First Person", vm.Detailer);

        vm.Designer = "Second Person";
        Assert.Equal("Second Person", vm.Detailer);

        vm.Designer = string.Empty;
        Assert.Equal(string.Empty, vm.Detailer);

        vm.Designer = "Third Person";
        Assert.Equal("Third Person", vm.Detailer);
        Assert.True(vm.DetailerFollowsDesigner);

        vm.Detailer = "Own Detailer";
        Assert.False(vm.DetailerFollowsDesigner);

        vm.Designer = "Fourth Person";
        Assert.Equal("Own Detailer", vm.Detailer);

        // Typing the Designer's value back in does not start following again.
        vm.Detailer = "Fourth Person";
        vm.Designer = "Fifth Person";
        Assert.Equal("Fourth Person", vm.Detailer);
        Assert.False(vm.DetailerFollowsDesigner);
    }

    [Fact]
    public void FollowingDetailer_SettingItToTheDesignerKeepsFollowing()
    {
        var vm = Create(Blank());

        vm.Detailer = DefaultDesigner;
        vm.Designer = "Other Person";

        Assert.True(vm.DetailerFollowsDesigner);
        Assert.Equal("Other Person", vm.Detailer);
    }

    [Fact]
    public void FollowingDetailer_RaisesDetailerWhenDesignerChanges()
    {
        var vm = Create(Blank());
        var changes = RecordPropertyChanges(vm);

        vm.Designer = "Other Person";

        Assert.Contains(nameof(PartPropertiesViewModel.Detailer), changes);
    }

    [Fact]
    public void FileWithADetailer_NeverFollows()
    {
        var vm = Create(Complete());

        Assert.False(vm.DetailerFollowsDesigner);
        vm.Designer = "Other Person";
        Assert.Equal("Sam Sample", vm.Detailer);
    }

    [Fact]
    public void BlankDetailer_WithBlankDesignerAndNoDefault_FollowsWithoutAChange()
    {
        var vm = Create(Blank() with { Designer = null }, defaultDesigner: null);

        Assert.True(vm.DetailerFollowsDesigner);
        Assert.Equal(string.Empty, vm.Detailer);
        Assert.False(vm.HasChanges);
        Assert.Equal(string.Empty, vm.StatusText);

        vm.Designer = "New Person";
        Assert.Equal("New Person", vm.Detailer);
        Assert.True(vm.HasChanges);
    }

    [Fact]
    public void FollowedDetailerAlone_IsAPendingChange()
    {
        var vm = Create(Complete() with { Detailer = null });

        Assert.Equal("Alex Example", vm.Detailer);
        Assert.True(vm.HasChanges);
        Assert.Equal(PartPropertiesViewModel.PreFillNotice, vm.StatusText);
        Assert.True(vm.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void DescriptionSyncAlone_IsAPendingChange()
    {
        var target = new FakeTarget();
        var vm = Create(Complete() with { Description = "Old name" }, target);

        Assert.True(vm.HasChanges);
        Assert.Equal("Pre-filled values will be written when you apply.", vm.StatusText);
        Assert.True(vm.ApplyCommand.CanExecute(null));

        vm.ApplyCommand.Execute(null);

        var write = Assert.Single(Assert.Single(target.Calls));
        Assert.Equal(PartPropertyStorage.Description, write.Location);
        Assert.Equal("Bracket", write.TextValue);
    }

    [Fact]
    public void BlankFile_PreFillsAreWrittenOnApply()
    {
        var target = new FakeTarget();
        var vm = Create(Blank(DocumentKind.Assembly) with { Description = null }, target);

        Assert.True(vm.HasChanges);
        Assert.False(vm.HasError);
        vm.ApplyCommand.Execute(null);

        var writes = Assert.Single(target.Calls);
        Assert.Collection(
            writes,
            w => Assert.Equal((PartPropertyStorage.Description, "Bracket"), (w.Location, w.TextValue)),
            w => Assert.Equal((PartPropertyStorage.PartType, "A"), (w.Location, w.TextValue)),
            w => Assert.Equal((PartPropertyStorage.Designer, DefaultDesigner), (w.Location, w.TextValue)),
            w => Assert.Equal((PartPropertyStorage.Detailer, DefaultDesigner), (w.Location, w.TextValue)));
    }

    // State and commands

    [Fact]
    public void FileThatAlreadyMatches_HasNoPendingChanges_AndOkWritesNothing()
    {
        var target = new FakeTarget();
        var vm = Create(Complete(), target);
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        Assert.False(vm.HasChanges);
        Assert.True(vm.IsValid);
        Assert.Null(vm.ValidationMessage);
        Assert.Equal(string.Empty, vm.StatusText);
        Assert.False(vm.HasError);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.True(vm.OkCommand.CanExecute(null));

        vm.OkCommand.Execute(null);

        Assert.Empty(target.Calls);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void EditingBackToTheOriginal_ClearsHasChanges()
    {
        var vm = Create(Complete());

        vm.Designer = "Other Person";
        Assert.True(vm.HasChanges);
        Assert.True(vm.ApplyCommand.CanExecute(null));

        vm.Designer = "Alex Example";
        Assert.False(vm.HasChanges);
        Assert.False(vm.ApplyCommand.CanExecute(null));

        vm.CostText = "12.5000";
        Assert.False(vm.HasChanges);

        vm.Detailer = "  Sam Sample  ";
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void StoredZeroCost_ShowsBlank_AndBlankIsNotAChange()
    {
        var vm = Create(Complete() with { Cost = 0m });

        Assert.Equal(string.Empty, vm.CostText);
        Assert.False(vm.HasChanges);

        vm.CostText = "0";
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void Cost_UsesTheGivenCulture()
    {
        var target = new FakeTarget();
        var vm = Create(Complete(), target, culture: CultureInfo.GetCultureInfo("de-DE"));

        Assert.Equal("12,50", vm.CostText);

        vm.CostText = "1.234,5";
        vm.ApplyCommand.Execute(null);

        var write = Assert.Single(Assert.Single(target.Calls));
        Assert.Equal(1234.5m, write.CurrencyValue);
        Assert.Equal("1.234,50", vm.CostText);
    }

    [Theory]
    [InlineData("abc", PropertyValues.CostNotNumberMessage)]
    [InlineData("-1", PropertyValues.CostNegativeMessage)]
    [InlineData("1.23456", PropertyValues.CostTooManyDecimalsMessage)]
    public void InvalidCost_DisablesApplyAndOk(string cost, string message)
    {
        var target = new FakeTarget();
        var vm = Create(Complete(), target);
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        vm.CostText = cost;

        Assert.False(vm.IsValid);
        Assert.Equal(message, vm.ValidationMessage);
        Assert.Equal(message, vm.StatusText);
        Assert.True(vm.HasError);
        Assert.True(vm.HasChanges);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.False(vm.OkCommand.CanExecute(null));

        vm.ApplyCommand.Execute(null);
        vm.OkCommand.Execute(null);
        Assert.Empty(target.Calls);
        Assert.Equal(0, closed);

        vm.CostText = "3";
        Assert.True(vm.IsValid);
        Assert.False(vm.HasError);
        Assert.True(vm.ApplyCommand.CanExecute(null));
    }

    [Fact]
    public void InvalidText_UsesTheFieldName_DesignerFirst()
    {
        var vm = Create(Complete());

        vm.Detailer = "Line one\nline two";
        Assert.StartsWith("Detailer", vm.ValidationMessage);

        vm.Designer = new string('x', PropertyValues.MaxTextLength + 1);
        Assert.StartsWith("Designer", vm.ValidationMessage);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.False(vm.OkCommand.CanExecute(null));

        vm.CostText = "abc";
        Assert.StartsWith("Designer", vm.ValidationMessage);
    }

    [Fact]
    public void ValidationMessage_TakesPriorityOverThePreFillNotice()
    {
        var vm = Create(Blank());
        Assert.Equal(PartPropertiesViewModel.PreFillNotice, vm.StatusText);

        vm.CostText = "abc";
        Assert.Equal(PropertyValues.CostNotNumberMessage, vm.StatusText);

        vm.CostText = string.Empty;
        Assert.Equal(PartPropertiesViewModel.PreFillNotice, vm.StatusText);
    }

    [Fact]
    public void PreFillNotice_GoesWhenThePreFillsAreUndone()
    {
        var vm = Create(Complete() with { Designer = null });
        Assert.Equal(PartPropertiesViewModel.PreFillNotice, vm.StatusText);

        vm.Designer = string.Empty;

        Assert.False(vm.HasChanges);
        Assert.Equal(string.Empty, vm.StatusText);
    }

    [Fact]
    public void Apply_CallsTheTargetOnceWithTheWholePlan_ThenRebases()
    {
        var target = new FakeTarget();
        var vm = Create(Complete(), target);
        var changes = RecordPropertyChanges(vm);

        vm.PartType = "";
        vm.Designer = "Other Person";
        vm.Detailer = "";
        vm.CostText = " 7.5 ";
        vm.ApplyCommand.Execute(null);

        var writes = Assert.Single(target.Calls);
        Assert.Collection(
            writes,
            w => Assert.Equal((PropertyWriteAction.Remove, PartPropertyStorage.PartType), (w.Action, w.Location)),
            w => Assert.Equal((PartPropertyStorage.Designer, "Other Person"), (w.Location, w.TextValue)),
            w => Assert.Equal((PropertyWriteAction.Remove, PartPropertyStorage.Detailer), (w.Action, w.Location)),
            w => Assert.Equal((PartPropertyStorage.Cost, (decimal?)7.5m), (w.Location, w.CurrencyValue)));

        Assert.False(vm.HasChanges);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.Equal(PartPropertiesViewModel.SavedNotice, vm.StatusText);
        Assert.Equal("Saved to the file. Save the document to keep the changes.", vm.StatusText);
        Assert.False(vm.HasError);
        Assert.Equal("7.50", vm.CostText);
        Assert.Contains(nameof(PartPropertiesViewModel.CostText), changes);

        // A second apply with nothing new does nothing.
        vm.ApplyCommand.Execute(null);
        Assert.Single(target.Calls);

        // Editing after the apply compares against what was written.
        vm.Designer = "Alex Example";
        Assert.True(vm.HasChanges);
        vm.ApplyCommand.Execute(null);
        Assert.Equal(2, target.Calls.Count);
        var write = Assert.Single(target.Calls[1]);
        Assert.Equal("Alex Example", write.TextValue);
    }

    [Fact]
    public void Apply_ClearingCost_WritesZero_AndShowsBlank()
    {
        var target = new FakeTarget();
        var vm = Create(Complete(), target);

        vm.CostText = "";
        vm.ApplyCommand.Execute(null);

        var write = Assert.Single(Assert.Single(target.Calls));
        Assert.Equal(0m, write.CurrencyValue);
        Assert.Equal(string.Empty, vm.CostText);
        Assert.False(vm.HasChanges);
    }

    [Fact]
    public void Apply_PreFillNoticeIsReplacedAndDoesNotReturn()
    {
        var vm = Create(Blank());

        vm.ApplyCommand.Execute(null);
        Assert.Equal(PartPropertiesViewModel.SavedNotice, vm.StatusText);

        vm.Designer = "Other Person";
        Assert.Equal(PartPropertiesViewModel.SavedNotice, vm.StatusText);
    }

    [Fact]
    public void Apply_FollowingDetailerKeepsFollowingAfterwards()
    {
        var target = new FakeTarget();
        var vm = Create(Blank(), target);

        vm.ApplyCommand.Execute(null);
        vm.Designer = "Other Person";

        Assert.Equal("Other Person", vm.Detailer);
        vm.ApplyCommand.Execute(null);
        Assert.Equal(
            new[] { PartPropertyStorage.Designer, PartPropertyStorage.Detailer },
            target.Calls[1].Select(w => w.Location));
    }

    [Fact]
    public void Apply_LogsPropertyNamesAndCount_NeverValues()
    {
        var log = new RecordingLog();
        var vm = Create(Complete(), log: log);

        vm.Designer = "Secret Person";
        vm.Detailer = "";
        vm.CostText = "99.99";
        vm.ApplyCommand.Execute(null);

        var line = Assert.Single(log.Infos);
        Assert.Contains("3", line);
        Assert.Contains("Designer", line);
        Assert.Contains("Detailer", line);
        Assert.Contains("Cost", line);
        Assert.DoesNotContain("Secret Person", line);
        Assert.DoesNotContain("Sam Sample", line);
        Assert.DoesNotContain("99", line);
        Assert.Empty(log.Errors);
    }

    [Fact]
    public void ApplyFailure_KeepsChanges_LogsError_AndShowsTheMessage()
    {
        var target = new FakeTarget { ThrowOnApply = new InvalidOperationException("disk on fire") };
        var log = new RecordingLog();
        var vm = Create(Complete(), target, log: log);

        vm.Designer = "Other Person";
        vm.CostText = "7.5";
        vm.ApplyCommand.Execute(null);

        Assert.Single(target.Calls);
        Assert.True(vm.HasChanges);
        Assert.True(vm.ApplyCommand.CanExecute(null));
        Assert.Equal("Could not write the properties: disk on fire", vm.StatusText);
        Assert.True(vm.HasError);
        Assert.Equal("7.5", vm.CostText);
        var error = Assert.Single(log.Errors);
        Assert.Same(target.ThrowOnApply, error.Ex);
        Assert.Empty(log.Infos);

        // A retry that succeeds writes the same plan and replaces the error.
        target.ThrowOnApply = null;
        vm.ApplyCommand.Execute(null);
        Assert.Equal(2, target.Calls.Count);
        Assert.Equal(target.Calls[0], target.Calls[1]);
        Assert.Equal(PartPropertiesViewModel.SavedNotice, vm.StatusText);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void ValidationMessage_TakesPriorityOverAWriteError()
    {
        var target = new FakeTarget { ThrowOnApply = new InvalidOperationException("nope") };
        var vm = Create(Complete(), target);

        vm.Designer = "Other Person";
        vm.ApplyCommand.Execute(null);
        vm.CostText = "abc";

        Assert.Equal(PropertyValues.CostNotNumberMessage, vm.StatusText);
        Assert.True(vm.HasError);
    }

    [Fact]
    public void Ok_AppliesThenCloses()
    {
        var target = new FakeTarget();
        var vm = Create(Complete(), target);
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        vm.Designer = "Other Person";
        vm.OkCommand.Execute(null);

        Assert.Single(target.Calls);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Ok_WritesPendingPreFills()
    {
        var target = new FakeTarget();
        var vm = Create(Blank(), target);
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        vm.OkCommand.Execute(null);

        Assert.Single(target.Calls);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void Ok_WhenTheApplyFails_DoesNotClose()
    {
        var target = new FakeTarget { ThrowOnApply = new InvalidOperationException("nope") };
        var vm = Create(Complete(), target);
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        vm.Designer = "Other Person";
        vm.OkCommand.Execute(null);

        Assert.Single(target.Calls);
        Assert.Equal(0, closed);
        Assert.Equal("Could not write the properties: nope", vm.StatusText);
    }

    // Files that cannot be edited

    [Theory]
    [InlineData(EditBlock.ReadOnlyFile, PartPropertiesSnapshot.ReadOnlyFileMessage)]
    [InlineData(EditBlock.NotModifiable, PartPropertiesSnapshot.NotModifiableMessage)]
    public void ReadOnlySnapshot_DisablesEditing_AndOkOnlyCloses(EditBlock block, string message)
    {
        var target = new FakeTarget();
        var snapshot = Blank(DocumentKind.Assembly) with
        {
            Description = "Old name",
            PartType = "m",
            EditBlock = block,
        };
        var vm = Create(snapshot, target);
        var closed = 0;
        vm.CloseRequested += (_, _) => closed++;

        Assert.False(vm.IsEditable);
        Assert.Equal(message, vm.EditBlockMessage);
        Assert.Equal(message, vm.StatusText);
        Assert.False(vm.HasError);
        Assert.False(vm.HasChanges);
        Assert.False(vm.ApplyCommand.CanExecute(null));
        Assert.True(vm.OkCommand.CanExecute(null));

        // No pre-fills: the window shows what the file holds (the listed spelling of the code aside).
        Assert.Equal("M", vm.PartType);
        Assert.Equal(string.Empty, vm.Designer);
        Assert.Equal(string.Empty, vm.Detailer);
        Assert.False(vm.DetailerFollowsDesigner);

        vm.ApplyCommand.Execute(null);
        vm.OkCommand.Execute(null);

        Assert.Empty(target.Calls);
        Assert.Equal(1, closed);
    }

    [Fact]
    public void ReadOnlySnapshot_WithBlankType_IsNotPreFilled()
    {
        var vm = Create(Blank(DocumentKind.WeldmentAssembly) with { EditBlock = EditBlock.ReadOnlyFile });

        Assert.Equal(string.Empty, vm.PartType);
    }

    [Fact]
    public void ReadOnlySnapshot_EditBlockMessageOutranksValidation()
    {
        var vm = Create(Complete() with { EditBlock = EditBlock.ReadOnlyFile, Designer = "Line\nbreak" });

        Assert.False(vm.IsValid);
        Assert.Equal(PartPropertiesSnapshot.ReadOnlyFileMessage, vm.StatusText);
        Assert.False(vm.HasError);
        Assert.True(vm.OkCommand.CanExecute(null));
    }

    [Fact]
    public void EditableSnapshot_HasNoEditBlockMessage()
    {
        var vm = Create(Complete());

        Assert.Null(vm.EditBlockMessage);
    }
}
