using System.Text;
using LabelDiffTool.Core.Models;

namespace LabelDiffTool.Core.Parsing;

/// <summary>
/// Serialises a <see cref="LabelFile"/> back to the D365 label file format,
/// preserving entry order, header comments and each label's description line.
/// </summary>
public static class LabelWriter
{
    // D365 label files are UTF-8; emit without a BOM and with CRLF line endings.
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private const string NewLine = "\r\n";

    public static void WriteFile(LabelFile file, string? path = null)
    {
        var target = path ?? file.FilePath
            ?? throw new InvalidOperationException("No path supplied and the file has no FilePath.");
        File.WriteAllText(target, Write(file), Utf8NoBom);
    }

    public static string Write(LabelFile file)
    {
        var sb = new StringBuilder();

        foreach (var header in file.HeaderComments)
            sb.Append(header).Append(NewLine);

        // Rewrite the whole file with labels sorted by id (same order as the comparison grid),
        // so labels filled in later are never just appended at the end.
        foreach (var entry in file.Entries.OrderBy(e => e.Id, LabelOrder.ById))
        {
            sb.Append(entry.Id).Append('=').Append(entry.Text).Append(NewLine);
            if (entry.Description is not null)
                sb.Append(';').Append(entry.Description).Append(NewLine);
        }

        return sb.ToString();
    }
}
