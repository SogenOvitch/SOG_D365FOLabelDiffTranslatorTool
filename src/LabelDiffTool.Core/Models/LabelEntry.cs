namespace LabelDiffTool.Core.Models;

/// <summary>
/// A single label as it appears in a D365 F&O label file.
/// A label is a triple: an identifier, its translated text, and an optional
/// developer/translator description (the semicolon-prefixed line).
/// </summary>
public sealed class LabelEntry
{
    public LabelEntry(string id, string text, string? description = null,
        bool isMachineTranslated = false, bool isUnsaved = false)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Text = text ?? string.Empty;
        Description = description;
        IsMachineTranslated = isMachineTranslated;
        IsUnsaved = isUnsaved;
    }

    /// <summary>The label identifier, e.g. <c>CustAccount</c>. Case-sensitive.</summary>
    public string Id { get; }

    /// <summary>The user-facing text. This is the only part ever sent to a translator.</summary>
    public string Text { get; set; }

    /// <summary>
    /// The description note (content after the leading <c>;</c>), or <c>null</c> if none.
    /// Per the "mode A" copy rule this is always carried over verbatim, never translated.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>True when <see cref="Text"/> was produced by machine translation and not yet reviewed.</summary>
    public bool IsMachineTranslated { get; set; }

    /// <summary>
    /// True when this entry has been added/modified since the file was last saved. Distinct from
    /// <see cref="IsMachineTranslated"/> (which is permanent provenance): this is cleared on save.
    /// </summary>
    public bool IsUnsaved { get; set; }

    public LabelEntry Clone() => new(Id, Text, Description, IsMachineTranslated, IsUnsaved);
}
