using InventorAddin.Core.Drawing;
using InventorAddin.Core.Settings;

namespace InventorAddin.Core.ViewModels;

/// <summary>The part marking window's model (spec 05, "Added for M5", item 2; task 198).</summary>
public sealed class PartMarkingViewModel : ObservableObject
{
    public const string NothingToPlaceText = "Choose a method or type a line to put on the note.";

    private PartMarkingMethod _method = PartMarkingMethod.LaserEngrave;
    private PartMarkingTarget _target = PartMarkingTarget.Leader;
    private string _extraLinesText = string.Empty;
    private bool _engraveFeature;

    /// <param name="units">The part's units.</param>
    /// <param name="isAssembly">True for an assembly: the engrave feature is not offered.</param>
    /// <param name="highlightColour">Settings' <c>PartMarkingColour</c>; invalid reads as the default.</param>
    public PartMarkingViewModel(UnitsSystem units, bool isAssembly, string? highlightColour = null)
    {
        Units = units;
        IsAssembly = isAssembly;
        HighlightColour = PartMarking.NormaliseColour(highlightColour);
    }

    public UnitsSystem Units { get; }

    public bool IsAssembly { get; }

    public string HighlightColour { get; }

    public PartMarkingMethod Method
    {
        get => _method;
        set
        {
            if (!SetProperty(ref _method, value))
                return;
            OnPropertyChanged(nameof(CanEngraveFeature));
            OnPropertyChanged(nameof(EngraveFeature));
            RaiseResultChanged();
        }
    }

    public PartMarkingTarget Target
    {
        get => _target;
        set
        {
            if (SetProperty(ref _target, value))
                RaiseResultChanged();
        }
    }

    /// <summary>The extra lines as typed, one per line.</summary>
    public string ExtraLinesText
    {
        get => _extraLinesText;
        set
        {
            if (SetProperty(ref _extraLinesText, value ?? string.Empty))
                RaiseResultChanged();
        }
    }

    /// <summary>Whether the engraved part number feature is on. Reads false when it is not offered.</summary>
    public bool EngraveFeature
    {
        get => _engraveFeature && CanEngraveFeature;
        set
        {
            if (_engraveFeature == value)
                return;
            _engraveFeature = value;
            OnPropertyChanged();
        }
    }

    /// <summary>False for an assembly, and when the method is None.</summary>
    public bool CanEngraveFeature => !IsAssembly && Method != PartMarkingMethod.None;

    public string CharacterHeightText => PartMarking.SizeText(PartMarking.CharacterHeight(Units), Units);

    public string EngraveDepthText => PartMarking.SizeText(PartMarking.EngraveDepth(Units), Units);

    public string ZoneSizeText => PartMarking.ZoneSizeText(Units);

    public bool IsZone => Target == PartMarkingTarget.Zone;

    /// <summary>The note text as it will be placed.</summary>
    public string NoteText => PartMarking.NoteText(Options);

    /// <summary>What the window previews: the note text, and for a leader the "&lt;part number&gt;" line where the note will have the number.</summary>
    public string PreviewText => Target == PartMarkingTarget.Leader ? PartMarking.LeaderPreviewText(Options) : NoteText;

    /// <summary>The options the window currently describes.</summary>
    public PartMarkingOptions Options => new()
    {
        Method = Method,
        ExtraLines = PartMarking.SplitExtraLines(ExtraLinesText),
        Target = Target,
        EngraveFeature = EngraveFeature,
        Units = Units,
        IsAssembly = IsAssembly,
        HighlightColour = HighlightColour,
    };

    /// <summary>Why OK is not offered, or null. With method None and no extra lines there is no note to place.</summary>
    public string? ProblemText => NoteText.Length == 0 ? NothingToPlaceText : null;

    public bool CanApply => ProblemText is null;

    private void RaiseResultChanged()
    {
        OnPropertyChanged(nameof(NoteText));
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(IsZone));
        OnPropertyChanged(nameof(ProblemText));
        OnPropertyChanged(nameof(CanApply));
    }
}
