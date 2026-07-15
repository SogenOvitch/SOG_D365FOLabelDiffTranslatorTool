using LabelDiffTool.Core.Comparison;
using LabelDiffTool.Core.Models;
using LabelDiffTool.Core.Parsing;
using Xunit;

namespace LabelDiffTool.Tests;

public class LabelComparerAndWriterTests
{
    private static LabelFile FileWith(string id, string lang, params (string Id, string Text, string? Desc)[] entries)
    {
        var f = new LabelFile(id, lang);
        foreach (var e in entries)
            f.AddOrReplace(new LabelEntry(e.Id, e.Text, e.Desc));
        return f;
    }

    [Fact]
    public void Compare_detects_gaps_in_both_directions()
    {
        var a = FileWith("A.en-US.label.txt", "en-US",
            ("Shared", "Text", null),
            ("OnlyInA", "A only", null));
        var b = FileWith("B.fr-FR.label.txt", "fr-FR",
            ("Shared", "Texte", null),
            ("OnlyInB", "B only", null));

        var result = LabelComparer.Compare(new[] { a, b });

        Assert.Equal(3, result.Rows.Count);
        Assert.Equal(new[] { "OnlyInB" }, result.MissingInFile("A.en-US.label.txt"));
        Assert.Equal(new[] { "OnlyInA" }, result.MissingInFile("B.fr-FR.label.txt"));

        var shared = result.Rows.Single(r => r.Id == "Shared");
        Assert.True(shared.IsComplete);
    }

    [Fact]
    public void Writer_roundtrips_labels_and_descriptions()
    {
        var lines = new[]
        {
            ";File header",
            "CustAccount=Customer account",
            ";The customer account number",
            "ItemName=Item name",
        };
        var file = LabelParser.Parse("Foo.en-US.label.txt", "en-US", lines);

        var text = LabelWriter.Write(file);

        Assert.Contains(";File header", text);
        Assert.Contains("CustAccount=Customer account", text);
        Assert.Contains(";The customer account number", text);
        Assert.Contains("ItemName=Item name", text);

        // Re-parsing the written text yields the same entries.
        var reparsed = LabelParser.Parse("Foo.en-US.label.txt", "en-US", text.Split("\r\n"));
        Assert.Equal(2, reparsed.Count);
        Assert.Equal("The customer account number", reparsed.Find("CustAccount")!.Description);
    }
}
