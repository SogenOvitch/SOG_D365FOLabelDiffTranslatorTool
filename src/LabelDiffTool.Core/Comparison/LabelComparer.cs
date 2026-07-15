using LabelDiffTool.Core.Models;

namespace LabelDiffTool.Core.Comparison;

/// <summary>
/// Compares any number of label files by label id and produces a merged,
/// row-per-id view of where the gaps are.
/// </summary>
public static class LabelComparer
{
    public static ComparisonResult Compare(IReadOnlyList<LabelFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        // Union of all ids across every file. Identity stays case-sensitive (label ids are),
        // but ordering is case-insensitive so 'A' and 'a' sort together; the ordinal tie-break
        // keeps the order deterministic when two ids differ only by case.
        var ids = files
            .SelectMany(f => f.Entries.Select(e => e.Id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, LabelOrder.ById)
            .ToList();

        var rows = new List<ComparisonRow>(ids.Count);
        foreach (var id in ids)
        {
            var cells = new Dictionary<string, LabelEntry>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                if (file.TryGet(id, out var entry))
                    cells[file.FileId] = entry;
            }
            rows.Add(new ComparisonRow(id, files, cells));
        }

        return new ComparisonResult(files, rows);
    }
}
