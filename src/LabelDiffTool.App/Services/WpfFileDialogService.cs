using System.Windows;
using Microsoft.Win32;

namespace LabelDiffTool.App.Services;

/// <summary>WPF/Win32 implementation of <see cref="IFileDialogService"/>.</summary>
public sealed class WpfFileDialogService : IFileDialogService
{
    private const string Filter = "D365 label files (*.label.txt)|*.label.txt|Text files (*.txt)|*.txt|All files (*.*)|*.*";

    public IReadOnlyList<string> PickOpenLabelFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add label files",
            Filter = Filter,
            CheckFileExists = true,
            Multiselect = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileNames : Array.Empty<string>();
    }

    public string? PickSaveLabelFile(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save label file",
            Filter = Filter,
            FileName = suggestedFileName,
            OverwritePrompt = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public bool Confirm(string message, string title)
        => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;

    public SaveChangesChoice AskSaveChanges(string message)
        => MessageBox.Show(message, "Unsaved changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning) switch
        {
            MessageBoxResult.Yes => SaveChangesChoice.Save,
            MessageBoxResult.No => SaveChangesChoice.Discard,
            _ => SaveChangesChoice.Cancel,
        };
}
