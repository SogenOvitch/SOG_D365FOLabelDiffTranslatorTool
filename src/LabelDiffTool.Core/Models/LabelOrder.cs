namespace LabelDiffTool.Core.Models;

/// <summary>
/// The canonical ordering for labels: by id, case-insensitively (so 'A' and 'a' sort together),
/// with an ordinal tie-break so the order stays deterministic when two ids differ only by case.
/// Shared by the comparison grid and the file writer so what you see is what gets saved.
/// </summary>
public static class LabelOrder
{
    public static readonly IComparer<string> ById = Comparer<string>.Create((a, b) =>
    {
        var c = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
        return c != 0 ? c : string.Compare(a, b, StringComparison.Ordinal);
    });
}
