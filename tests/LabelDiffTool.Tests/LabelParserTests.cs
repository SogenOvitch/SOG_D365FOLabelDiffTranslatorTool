using LabelDiffTool.Core.Parsing;
using Xunit;

namespace LabelDiffTool.Tests;

public class LabelParserTests
{
    [Theory]
    [InlineData("Foo.fr-FR.label.txt", "fr-FR")]
    [InlineData("MyLabels.en-US.label.txt", "en-US")]
    [InlineData("Bar.zh-Hans.label.txt", "zh-Hans")]
    [InlineData("Baz.txt", null)]
    [InlineData("NoLanguage.label.txt", null)]
    public void LanguageFromFileName_extracts_tag(string fileName, string? expected)
    {
        Assert.Equal(expected, LabelParser.LanguageFromFileName(fileName));
    }

    [Fact]
    public void Parse_binds_description_to_preceding_label()
    {
        var lines = new[]
        {
            ";Header comment",
            "CustAccount=Customer account",
            ";The account number of the customer",
            "ItemName=Item name",
        };

        var file = LabelParser.Parse("Foo.en-US.label.txt", "en-US", lines);

        Assert.Equal(2, file.Count);
        Assert.Single(file.HeaderComments);

        var cust = file.Find("CustAccount");
        Assert.NotNull(cust);
        Assert.Equal("Customer account", cust!.Text);
        Assert.Equal("The account number of the customer", cust.Description);

        var item = file.Find("ItemName");
        Assert.NotNull(item);
        Assert.Null(item!.Description);
    }

    [Fact]
    public void Parse_keeps_equals_sign_inside_text()
    {
        var file = LabelParser.Parse("f.en-US.label.txt", "en-US", new[] { "Formula=a=b+c" });
        Assert.Equal("a=b+c", file.Find("Formula")!.Text);
    }
}
