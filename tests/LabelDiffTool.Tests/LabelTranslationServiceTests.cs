using LabelDiffTool.Core.Models;
using LabelDiffTool.Core.Parsing;
using LabelDiffTool.Core.Translation;
using Xunit;

namespace LabelDiffTool.Tests;

public class LabelTranslationServiceTests
{
    // A fake engine that just prefixes the text, so results are deterministic and offline.
    private sealed class FakeTranslator : ITranslationService
    {
        public Task<string> TranslateAsync(string text, string target, string? source = null, CancellationToken ct = default)
            => Task.FromResult($"[{target}]{text}");

        public async Task<IReadOnlyList<string>> TranslateBatchAsync(
            IReadOnlyList<string> texts, string target, string? source = null,
            IProgress<BatchProgress>? progress = null, CancellationToken ct = default)
        {
            var result = new List<string>();
            foreach (var t in texts)
                result.Add(await TranslateAsync(t, target, source, ct));
            return result;
        }

        public Task<string> DetectLanguageAsync(string text, CancellationToken ct = default)
            => Task.FromResult("en");
    }

    private static LabelFile FileWith(string lang, params (string Id, string Text, string? Desc)[] entries)
    {
        var f = new LabelFile($"f.{lang}.label.txt", lang);
        foreach (var e in entries)
            f.AddOrReplace(new LabelEntry(e.Id, e.Text, e.Desc));
        return f;
    }

    // Reads back the label ids from written text, in file order.
    private static List<string> WrittenIds(LabelFile file) =>
        LabelWriter.Write(file)
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Where(l => l.Contains('=') && !l.StartsWith(';'))
            .Select(l => l[..l.IndexOf('=')])
            .ToList();

    [Fact]
    public async Task Filled_labels_are_saved_sorted_by_id_not_appended()
    {
        // Target initially out of order; a missing label ("Bravo") is filled in.
        var source = FileWith("en",
            ("Alpha", "Alpha", null),
            ("Bravo", "Bravo", "note"),
            ("Charlie", "Charlie", null),
            ("Delta", "Delta", null));
        var target = FileWith("fr",
            ("Delta", "Delta-fr", null),
            ("Alpha", "Alpha-fr", null),
            ("Charlie", "Charlie-fr", null));

        var service = new LabelTranslationService(new FakeTranslator());
        var added = await service.TranslateMissingAsync(source, target);

        Assert.Equal(1, added);

        // The saved file is fully sorted by id, not "Delta, Alpha, Charlie, Bravo(appended)".
        Assert.Equal(new[] { "Alpha", "Bravo", "Charlie", "Delta" }, WrittenIds(target));

        // Copy rule: text translated, description carried over verbatim, flagged machine-translated.
        var bravo = target.Find("Bravo")!;
        Assert.Equal("[fr]Bravo", bravo.Text);
        Assert.Equal("note", bravo.Description);
        Assert.True(bravo.IsMachineTranslated);
    }

    [Fact]
    public async Task TranslateLabels_translates_only_the_requested_ids()
    {
        var source = FileWith("en",
            ("Alpha", "Alpha", "a-note"),
            ("Bravo", "Bravo", null),
            ("Charlie", "Charlie", null));
        var target = FileWith("fr");

        var service = new LabelTranslationService(new FakeTranslator());
        // Request Alpha and Charlie (and a non-existent id, which is ignored).
        var count = await service.TranslateLabelsAsync(source, target, new[] { "Alpha", "Charlie", "Ghost" });

        Assert.Equal(2, count);
        Assert.Equal(new[] { "Alpha", "Charlie" }, WrittenIds(target));
        Assert.False(target.Contains("Bravo"));

        var alpha = target.Find("Alpha")!;
        Assert.Equal("[fr]Alpha", alpha.Text);
        Assert.Equal("a-note", alpha.Description); // description carried verbatim
        Assert.True(alpha.IsMachineTranslated);
    }

    [Fact]
    public async Task Sort_is_case_insensitive_when_saved()
    {
        var source = FileWith("en", ("apple", "apple", null), ("Banana", "Banana", null), ("cherry", "cherry", null));
        var target = FileWith("fr", ("Banana", "Banane", null));

        var service = new LabelTranslationService(new FakeTranslator());
        await service.TranslateMissingAsync(source, target);

        // 'apple' before 'Banana' before 'cherry' despite mixed case.
        Assert.Equal(new[] { "apple", "Banana", "cherry" }, WrittenIds(target));
    }
}
