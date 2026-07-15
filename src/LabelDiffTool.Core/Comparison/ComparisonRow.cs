using LabelDiffTool.Core.Models;

namespace LabelDiffTool.Core.Comparison;

/// <summary>
/// One row of a comparison: a single label id and the entry (if any) that each
/// compared file holds for it. Designed for N files — a file simply has no cell
/// in this row when it lacks the label.
/// </summary>
public sealed class ComparisonRow
{
    private readonly Dictionary<string, LabelEntry> _byFileId;

    public ComparisonRow(string id, IReadOnlyList<LabelFile> files, Dictionary<string, LabelEntry> byFileId)
    {
        Id = id;
        Files = files;
        _byFileId = byFileId;
    }

    public string Id { get; }

    /// <summary>The files participating in the comparison, in display order.</summary>
    public IReadOnlyList<LabelFile> Files { get; }

    public LabelEntry? EntryFor(string fileId) => _byFileId.TryGetValue(fileId, out var e) ? e : null;

    public bool IsPresentIn(string fileId) => _byFileId.ContainsKey(fileId);

    public int PresentCount => _byFileId.Count;

    public int FileCount => Files.Count;

    /// <summary>True when every compared file defines this label.</summary>
    public bool IsComplete => PresentCount == FileCount;

    /// <summary>The ids of the files that are missing this label.</summary>
    public IEnumerable<string> MissingFileIds =>
        Files.Select(f => f.FileId).Where(id => !_byFileId.ContainsKey(id));
}
