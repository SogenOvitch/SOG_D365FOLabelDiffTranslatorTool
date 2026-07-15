using LabelDiffTool.Core.Comparison;

namespace LabelDiffTool.App.ViewModels;

/// <summary>Row state from the perspective of the currently chosen source/target pair.</summary>
public enum RowState
{
    Both,
    MissingInSource,
    MissingInTarget,
}

/// <summary>
/// A flattened view of a <see cref="ComparisonRow"/> for the side-by-side grid: the left column
/// is the chosen source file, the right column the chosen target file. The core stays N-file
/// capable; this adapter just projects the pair currently displayed.
/// </summary>
public sealed class RowViewModel
{
    public RowViewModel(ComparisonRow row, string sourceFileId, string targetFileId)
    {
        Id = row.Id;

        var s = row.EntryFor(sourceFileId);
        var t = row.EntryFor(targetFileId);

        SourceText = s?.Text;
        TargetText = t?.Text;
        SourceDescription = s?.Description;
        TargetDescription = t?.Description;

        PresentInSource = s is not null;
        PresentInTarget = t is not null;
        TargetIsMachineTranslated = t?.IsMachineTranslated ?? false;
        NeedsSaving = (s?.IsUnsaved ?? false) || (t?.IsUnsaved ?? false);

        State = (PresentInSource, PresentInTarget) switch
        {
            (false, true) => RowState.MissingInSource,
            (true, false) => RowState.MissingInTarget,
            _ => RowState.Both,
        };
    }

    public string Id { get; }

    public string? SourceText { get; }
    public string? TargetText { get; }
    public string? SourceDescription { get; }
    public string? TargetDescription { get; }

    public bool PresentInSource { get; }
    public bool PresentInTarget { get; }
    public bool TargetIsMachineTranslated { get; }

    /// <summary>True when either side's entry has been translated but not yet saved.</summary>
    public bool NeedsSaving { get; }

    public bool IsComplete => PresentInSource && PresentInTarget;

    public RowState State { get; }

    public string StatusText => State switch
    {
        RowState.MissingInSource => "Missing in source",
        RowState.MissingInTarget => "Missing in target",
        _ when NeedsSaving => "OK · needs saving",
        _ => "OK",
    };
}
