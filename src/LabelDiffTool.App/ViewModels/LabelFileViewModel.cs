using CommunityToolkit.Mvvm.ComponentModel;
using LabelDiffTool.Core.Models;

namespace LabelDiffTool.App.ViewModels;

/// <summary>
/// Wraps a loaded <see cref="LabelFile"/> for the UI: editable language, live label count,
/// and a "dirty" flag that tracks whether the file has unsaved translations.
/// </summary>
public partial class LabelFileViewModel : ObservableObject
{
    public LabelFileViewModel(LabelFile file)
    {
        File = file;
        _language = file.Language == "unknown" ? null : file.Language;
    }

    public LabelFile File { get; }

    public string DisplayName => File.FileId;

    // The ComboBox selection box falls back to ToString(); make that the file name.
    public override string ToString() => DisplayName;

    public int Count => File.Count;

    public string Summary => $"{DisplayName}  ·  {Count} labels";

    [ObservableProperty] private string? _language;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DirtyMarker))]
    private bool _isDirty;

    /// <summary>Small glyph shown in the file list when there are unsaved changes.</summary>
    public string DirtyMarker => IsDirty ? "●" : string.Empty;

    partial void OnLanguageChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            File.Language = value;
    }

    /// <summary>Call after the file's entries change so count-derived labels refresh.</summary>
    public void NotifyContentChanged()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(Summary));
    }
}
