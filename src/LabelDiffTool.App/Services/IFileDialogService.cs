namespace LabelDiffTool.App.Services;

/// <summary>The user's answer to a "you have unsaved changes" prompt.</summary>
public enum SaveChangesChoice
{
    Save,
    Discard,
    Cancel,
}

/// <summary>Abstracts the file/message dialogs so the view model stays testable.</summary>
public interface IFileDialogService
{
    /// <summary>Prompts for one or more label files to open; returns an empty list if cancelled.</summary>
    IReadOnlyList<string> PickOpenLabelFiles();

    /// <summary>Prompts for a save location; returns <c>null</c> if cancelled.</summary>
    string? PickSaveLabelFile(string suggestedFileName);

    /// <summary>A simple yes/no confirmation; returns true for "yes".</summary>
    bool Confirm(string message, string title);

    /// <summary>A three-way Save / Discard / Cancel prompt for pending unsaved changes.</summary>
    SaveChangesChoice AskSaveChanges(string message);
}
