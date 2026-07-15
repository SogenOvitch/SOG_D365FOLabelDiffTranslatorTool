using System.Text;
using System.Text.RegularExpressions;
using LabelDiffTool.Core.Models;

namespace LabelDiffTool.Core.Parsing;

/// <summary>
/// Reads D365 F&O label files. The format is line-oriented:
/// <code>
/// LabelId=Label text
/// ;Optional description bound to the label above
/// </code>
/// A semicolon line that appears before any label is treated as a file header comment.
/// </summary>
public static partial class LabelParser
{
    // A label definition: an id (no '=' or ';'), then '=', then the rest of the line.
    [GeneratedRegex(@"^(?<id>[^=;]+?)=(?<text>.*)$")]
    private static partial Regex LabelLine();

    // A language tag such as "en-US", "fr", "zh-Hans".
    [GeneratedRegex(@"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,4})*$")]
    private static partial Regex LanguageTag();

    public static LabelFile ParseFile(string path)
    {
        var language = LanguageFromFileName(Path.GetFileName(path)) ?? "unknown";
        var fileId = Path.GetFileName(path);
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var file = Parse(fileId, language, lines);
        file.FilePath = path;
        return file;
    }

    public static LabelFile Parse(string fileId, string language, IEnumerable<string> lines)
    {
        var file = new LabelFile(fileId, language);
        LabelEntry? current = null;
        var seenFirstLabel = false;

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd('\r', '\n');

            if (IsComment(line, out var commentBody))
            {
                if (current is not null && current.Description is null)
                {
                    // First comment line directly under a label becomes its description.
                    current.Description = commentBody;
                }
                else if (!seenFirstLabel)
                {
                    // Comments preceding any label are file-level header comments (kept verbatim).
                    file.HeaderComments.Add(line);
                }
                // Extra comment lines beyond the first description are ignored on purpose;
                // add a Description list here later if multi-line notes must survive round-trip.
                continue;
            }

            var m = LabelLine().Match(line);
            if (m.Success)
            {
                seenFirstLabel = true;
                current = new LabelEntry(m.Groups["id"].Value.Trim(), m.Groups["text"].Value);
                file.AddOrReplace(current);
            }
            // Blank / unrecognised lines are skipped; they don't break label/description binding.
        }

        return file;
    }

    private static bool IsComment(string line, out string body)
    {
        var trimmed = line.TrimStart();
        if (trimmed.StartsWith(';'))
        {
            body = trimmed[1..];
            return true;
        }
        body = string.Empty;
        return false;
    }

    /// <summary>
    /// Extracts the language tag from a name like <c>Foo.fr-FR.label.txt</c> → <c>fr-FR</c>.
    /// Returns <c>null</c> when the name doesn't carry a recognisable tag.
    /// </summary>
    public static string? LanguageFromFileName(string fileName)
    {
        const string suffix = ".label.txt";
        var name = fileName;
        if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            name = name[..^suffix.Length];
        else
            name = Path.GetFileNameWithoutExtension(name);

        var lastDot = name.LastIndexOf('.');
        if (lastDot < 0) return null;

        var candidate = name[(lastDot + 1)..];
        return LanguageTag().IsMatch(candidate) ? candidate : null;
    }
}
