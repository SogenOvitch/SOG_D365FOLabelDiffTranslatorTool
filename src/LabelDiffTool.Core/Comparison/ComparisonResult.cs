using LabelDiffTool.Core.Models;

namespace LabelDiffTool.Core.Comparison;

/// <summary>The outcome of comparing two or more label files.</summary>
public sealed class ComparisonResult
{
    public ComparisonResult(IReadOnlyList<LabelFile> files, IReadOnlyList<ComparisonRow> rows)
    {
        Files = files;
        Rows = rows;
    }

    public IReadOnlyList<LabelFile> Files { get; }

    /// <summary>All rows (the union of label ids across every file), sorted by id.</summary>
    public IReadOnlyList<ComparisonRow> Rows { get; }

    /// <summary>Rows where at least one file is missing the label.</summary>
    public IEnumerable<ComparisonRow> Gaps => Rows.Where(r => !r.IsComplete);

    /// <summary>Label ids that are absent from the given file but present in at least one other.</summary>
    public IReadOnlyList<string> MissingInFile(string fileId) =>
        Rows.Where(r => !r.IsPresentIn(fileId)).Select(r => r.Id).ToList();
}
