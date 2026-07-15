using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelDiffTool.App.Services;
using LabelDiffTool.Core.Comparison;
using LabelDiffTool.Core.Parsing;
using LabelDiffTool.Core.Translation;

namespace LabelDiffTool.App.ViewModels;

/// <summary>
/// Drives the main window: opening any number of label files, choosing a source and target pair
/// to diff side by side, translating gaps (into the target or into every other file at once), and
/// tracking which files still need saving.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly IFileDialogService _dialogs;
    private readonly ITranslationService _translator;
    private readonly LabelTranslationService _labelTranslator;

    public MainViewModel(IFileDialogService dialogs, ITranslationService translator)
    {
        _dialogs = dialogs;
        _translator = translator;
        _labelTranslator = new LabelTranslationService(translator);

        RowsView = CollectionViewSource.GetDefaultView(Rows);
        RowsView.Filter = FilterRow;
        UpdateSaveState();
    }

    /// <summary>All opened files (N).</summary>
    public ObservableCollection<LabelFileViewModel> Files { get; } = new();

    public ObservableCollection<RowViewModel> Rows { get; } = new();

    public ICollectionView RowsView { get; }

    public IReadOnlyList<LanguageOption> Languages { get; } = LanguageCatalog.Common;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TranslateIntoTargetCommand))]
    [NotifyCanExecuteChangedFor(nameof(TranslateIntoAllCommand))]
    private LabelFileViewModel? _selectedSource;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TranslateIntoTargetCommand))]
    private LabelFileViewModel? _selectedTarget;

    // The rows currently selected in the grid (supports Ctrl/Shift multi-select). Pushed in from
    // the view's SelectionChanged because DataGrid.SelectedItems isn't bindable.
    private IReadOnlyList<RowViewModel> _selectedRows = Array.Empty<RowViewModel>();

    public void SetSelectedRows(IEnumerable<RowViewModel> rows)
    {
        _selectedRows = rows.ToList();
        TranslateSelectedCommand.NotifyCanExecuteChanged();
        TranslateSelectedIntoAllCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty] private bool _showGapsOnly;
    [ObservableProperty] private bool _showNeedsSavingOnly;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool _isBusy;

    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _statusMessage = "Open label files to compare.";

    [ObservableProperty] private string _saveStateMessage = string.Empty;
    [ObservableProperty] private bool _hasUnsavedChanges;

    public bool IsIdle => !IsBusy;

    partial void OnShowGapsOnlyChanged(bool value) => RowsView.Refresh();
    partial void OnShowNeedsSavingOnlyChanged(bool value) => RowsView.Refresh();
    partial void OnSelectedSourceChanged(LabelFileViewModel? value) => RebuildComparison();
    partial void OnSelectedTargetChanged(LabelFileViewModel? value) => RebuildComparison();

    private bool FilterRow(object item)
    {
        if (item is not RowViewModel row) return true;
        if (ShowGapsOnly && row.IsComplete) return false;
        if (ShowNeedsSavingOnly && !row.NeedsSaving) return false;
        return true;
    }

    /// <summary>Swap which files are source and target (and thus the diff columns).</summary>
    [RelayCommand]
    private void SwapSourceTarget()
        => (SelectedSource, SelectedTarget) = (SelectedTarget, SelectedSource);

    // ---- Opening / removing files ------------------------------------------

    [RelayCommand]
    private void AddFiles()
    {
        var paths = _dialogs.PickOpenLabelFiles();
        if (paths.Count == 0) return;

        LabelFileViewModel? last = null;
        var added = 0;
        var skipped = 0;
        foreach (var path in paths)
        {
            // A file is identified by its full path; don't open the same one twice.
            if (Files.Any(f => PathEquals(f.File.FilePath, path)))
            {
                skipped++;
                continue;
            }

            var vm = new LabelFileViewModel(LabelParser.ParseFile(path));
            Files.Add(vm);
            last = vm;
            added++;

            // Sensibly seed the source/target selectors as files come in.
            if (SelectedSource is null)
                SelectedSource = vm;
            else if (SelectedTarget is null && vm != SelectedSource)
                SelectedTarget = vm;
        }

        RefreshCommandStates();
        StatusMessage = (added, skipped) switch
        {
            (0, _) => "That file is already open.",
            (_, > 0) => $"Loaded {added} file(s); skipped {skipped} already open.",
            (1, 0) => $"Loaded {last!.DisplayName} ({last.Count} labels).",
            _ => $"Loaded {added} files.",
        };
    }

    [RelayCommand]
    private void RemoveFile(LabelFileViewModel? file)
    {
        if (file is null) return;
        if (!ConfirmDiscardIfDirty(file, "close")) return;

        var wasSource = SelectedSource == file;
        var wasTarget = SelectedTarget == file;

        Files.Remove(file);
        if (wasSource) SelectedSource = Files.FirstOrDefault();
        if (wasTarget) SelectedTarget = Files.FirstOrDefault(f => f != SelectedSource);

        RefreshCommandStates();
        RebuildComparison();
        UpdateSaveState();
    }

    [RelayCommand]
    private void ReloadFile(LabelFileViewModel? file)
    {
        if (file?.File.FilePath is not { } path) return;
        if (!ConfirmDiscardIfDirty(file, "reload")) return;

        try
        {
            file.File.ReplaceWith(LabelParser.ParseFile(path));
            foreach (var entry in file.File.Entries)
                entry.IsUnsaved = false;
            file.IsDirty = false;
            file.Language = file.File.Language == "unknown" ? file.Language : file.File.Language;
            file.NotifyContentChanged();
            UpdateSaveState();
            RebuildComparison();
            StatusMessage = $"Reloaded {file.DisplayName} from disk.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Reload failed: {ex.Message}";
        }
    }

    private static bool PathEquals(string? a, string? b)
        => a is not null && b is not null
           && string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    // ---- Language detection -------------------------------------------------

    [RelayCommand]
    private Task DetectLanguage(LabelFileViewModel? file)
    {
        if (file is null || file.Count == 0) return Task.CompletedTask;
        return RunBusyAsync($"Detecting language of {file.DisplayName}…", async () =>
        {
            var sample = string.Join(' ', file.File.Entries.Take(20).Select(e => e.Text));
            file.Language = await _translator.DetectLanguageAsync(sample);
            StatusMessage = $"Detected {file.DisplayName}: {file.Language}";
        });
    }

    // ---- Translation --------------------------------------------------------

    private bool CanTranslateIntoTarget =>
        SelectedSource is not null && SelectedTarget is not null && SelectedSource != SelectedTarget;

    private bool CanTranslateIntoAll =>
        SelectedSource is not null && Files.Count > 1;

    /// <summary>Fill labels missing in the target, using the source.</summary>
    [RelayCommand(CanExecute = nameof(CanTranslateIntoTarget))]
    private Task TranslateIntoTargetAsync() =>
        RunBusyAsync($"Translating into {SelectedTarget!.DisplayName}…", async () =>
        {
            var count = await TranslateOneTargetAsync(SelectedSource!, SelectedTarget!);
            RebuildComparison();
            StatusMessage = count == 0
                ? $"No missing labels to translate into {SelectedTarget!.DisplayName}."
                : $"Added {count} label(s) to {SelectedTarget!.DisplayName}.";
        });

    /// <summary>Fill every other opened file from the source in one pass.</summary>
    [RelayCommand(CanExecute = nameof(CanTranslateIntoAll))]
    private Task TranslateIntoAllAsync() =>
        RunBusyAsync("Translating into all other files…", async () =>
        {
            var targets = Files.Where(f => f != SelectedSource).ToList();
            var total = 0;
            foreach (var target in targets)
                total += await TranslateOneTargetAsync(SelectedSource!, target);

            RebuildComparison();
            StatusMessage = $"Added {total} label(s) across {targets.Count} file(s).";
        });

    private bool CanTranslateSelected =>
        SelectedSource is not null && SelectedTarget is not null
        && _selectedRows.Any(r => !r.IsComplete);

    private bool CanTranslateSelectedIntoAll =>
        Files.Count > 1 && _selectedRows.Count > 0;

    /// <summary>Translate the selected label(s) into whichever side is missing each one.</summary>
    [RelayCommand(CanExecute = nameof(CanTranslateSelected))]
    private Task TranslateSelectedAsync()
    {
        if (SelectedSource is null || SelectedTarget is null)
            return Task.CompletedTask;

        var intoTarget = _selectedRows.Where(r => r.State == RowState.MissingInTarget).Select(r => r.Id).ToList();
        var intoSource = _selectedRows.Where(r => r.State == RowState.MissingInSource).Select(r => r.Id).ToList();
        if (intoTarget.Count == 0 && intoSource.Count == 0)
            return Task.CompletedTask;

        return RunBusyAsync("Translating selected label(s)…", async () =>
        {
            var n = 0;
            if (intoTarget.Count > 0)
            {
                n += await _labelTranslator.TranslateLabelsAsync(
                    SelectedSource.File, SelectedTarget.File, intoTarget, SimpleProgress("Translating selected"));
                MarkDirty(SelectedTarget);
            }
            if (intoSource.Count > 0)
            {
                n += await _labelTranslator.TranslateLabelsAsync(
                    SelectedTarget.File, SelectedSource.File, intoSource, SimpleProgress("Translating selected"));
                MarkDirty(SelectedSource);
            }

            RebuildComparison();
            StatusMessage = $"Translated {n} selected label(s).";
        });
    }

    /// <summary>Translate the selected label(s) into every other opened file that lacks them.</summary>
    [RelayCommand(CanExecute = nameof(CanTranslateSelectedIntoAll))]
    private Task TranslateSelectedIntoAllAsync()
    {
        if (Files.Count < 2 || _selectedRows.Count == 0)
            return Task.CompletedTask;

        var ids = _selectedRows.Select(r => r.Id).Distinct(StringComparer.Ordinal).ToList();

        return RunBusyAsync("Translating selected label(s) into all files…", async () =>
        {
            var total = 0;
            foreach (var target in Files.ToList())
            {
                var missing = ids.Where(id => !target.File.Contains(id)).ToList();
                if (missing.Count == 0)
                    continue;

                // A selected label may live in different files; group by its origin so each
                // origin→target pair is translated in one batched request.
                foreach (var group in missing
                             .Select(id => (id, origin: OriginFor(id, target)))
                             .Where(x => x.origin is not null)
                             .GroupBy(x => x.origin!))
                {
                    total += await _labelTranslator.TranslateLabelsAsync(
                        group.Key.File, target.File, group.Select(x => x.id), SimpleProgress($"Translating into {target.DisplayName}"));
                    MarkDirty(target);
                }
            }

            RebuildComparison();
            StatusMessage = total == 0
                ? "Selected label(s) already present in all other files."
                : $"Added {total} translation(s) across the other files.";
        });
    }

    /// <summary>Finds a file (other than <paramref name="exclude"/>) that defines the label.</summary>
    private LabelFileViewModel? OriginFor(string id, LabelFileViewModel exclude)
        => (SelectedSource is { } s && s != exclude && s.File.Contains(id) ? s : null)
           ?? (SelectedTarget is { } t && t != exclude && t.File.Contains(id) ? t : null)
           ?? Files.FirstOrDefault(f => f != exclude && f.File.Contains(id));

    private IProgress<BatchProgress> SimpleProgress(string verb)
        => new Progress<BatchProgress>(p =>
        {
            ProgressValue = p.Fraction * 100;
            StatusMessage = $"{verb}: {p.Completed}/{p.Total}";
        });

    private async Task<int> TranslateOneTargetAsync(LabelFileViewModel source, LabelFileViewModel target)
    {
        var progress = new Progress<BatchProgress>(p =>
        {
            ProgressValue = p.Fraction * 100;
            StatusMessage = $"Translating into {target.DisplayName}: {p.Completed}/{p.Total}";
        });

        var count = await _labelTranslator.TranslateMissingAsync(source.File, target.File, progress);
        if (count > 0)
            MarkDirty(target);
        return count;
    }

    // ---- Saving -------------------------------------------------------------

    [RelayCommand]
    private void SaveFile(LabelFileViewModel? file)
    {
        if (file is null) return;
        var path = _dialogs.PickSaveLabelFile(SuggestedName(file));
        if (path is null) return;

        WriteAndClear(file, path);
        RebuildComparison();
        StatusMessage = $"Saved {file.DisplayName} → {path}";
    }

    [RelayCommand]
    private void SaveAll()
    {
        var dirty = Files.Where(f => f.IsDirty).ToList();
        if (dirty.Count == 0)
        {
            StatusMessage = "Nothing to save.";
            return;
        }

        var saved = 0;
        foreach (var file in dirty)
        {
            // Always confirm each path, even for files already on disk.
            var path = _dialogs.PickSaveLabelFile(SuggestedName(file));
            if (path is null) continue; // user cancelled this one; leave it dirty
            WriteAndClear(file, path);
            saved++;
        }

        RebuildComparison();
        StatusMessage = saved == dirty.Count
            ? $"Saved {saved} file(s)."
            : $"Saved {saved} of {dirty.Count} file(s); the rest were cancelled.";
    }

    private static string SuggestedName(LabelFileViewModel file)
        => file.File.FilePath is { } p ? System.IO.Path.GetFileName(p) : file.DisplayName;

    private void WriteAndClear(LabelFileViewModel file, string path)
    {
        LabelWriter.WriteFile(file.File, path);
        file.File.FilePath = path;
        foreach (var entry in file.File.Entries)
            entry.IsUnsaved = false;
        file.IsDirty = false;
        UpdateSaveState();
    }

    // ---- Helpers ------------------------------------------------------------

    /// <summary>
    /// If the file has unsaved changes, ask the user to confirm discarding them before an action
    /// (reload/close). Returns true if it's OK to proceed.
    /// </summary>
    private bool ConfirmDiscardIfDirty(LabelFileViewModel file, string action)
        => !file.IsDirty
           || _dialogs.Confirm(
               $"{file.DisplayName} has unsaved changes.\n\nDiscard them and {action} the file?",
               "Unsaved changes");

    /// <summary>
    /// Called by the window before closing. Prompts to save/discard when files are unsaved.
    /// Returns true if the app may close.
    /// </summary>
    public bool ConfirmClose()
    {
        var dirty = Files.Where(f => f.IsDirty).Select(f => f.DisplayName).ToList();
        if (dirty.Count == 0)
            return true;

        switch (_dialogs.AskSaveChanges(
                    $"You have unsaved changes in:\n  {string.Join("\n  ", dirty)}\n\nSave before closing?"))
        {
            case SaveChangesChoice.Save:
                SaveAll();
                return true;
            case SaveChangesChoice.Discard:
                return true;
            default:
                return false;
        }
    }

    private void RefreshCommandStates()
    {
        TranslateIntoTargetCommand.NotifyCanExecuteChanged();
        TranslateIntoAllCommand.NotifyCanExecuteChanged();
        TranslateSelectedCommand.NotifyCanExecuteChanged();
        TranslateSelectedIntoAllCommand.NotifyCanExecuteChanged();
    }

    private void MarkDirty(LabelFileViewModel file)
    {
        file.IsDirty = true;
        file.NotifyContentChanged();
        UpdateSaveState();
    }

    private void UpdateSaveState()
    {
        var dirty = Files.Where(f => f.IsDirty).Select(f => f.DisplayName).ToList();
        HasUnsavedChanges = dirty.Count > 0;
        SaveStateMessage = dirty.Count == 0
            ? "OK — all changes saved"
            : $"OK — needs saving: {string.Join(", ", dirty)}";
    }

    private void RebuildComparison()
    {
        Rows.Clear();

        var source = SelectedSource;
        var target = SelectedTarget;
        if (source is null || target is null || source == target)
        {
            RowsView.Refresh();
            return;
        }

        var result = LabelComparer.Compare(new[] { source.File, target.File });
        foreach (var row in result.Rows)
            Rows.Add(new RowViewModel(row, source.File.FileId, target.File.FileId));

        RowsView.Refresh();
    }

    private async Task RunBusyAsync(string message, Func<Task> work)
    {
        try
        {
            IsBusy = true;
            ProgressValue = 0;
            StatusMessage = message;
            await work();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            ProgressValue = 0;
        }
    }
}
