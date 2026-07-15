using LabelDiffTool.Core.Translation;
using Xunit;

namespace LabelDiffTool.Tests;

public class TranslationBatcherTests
{
    [Fact]
    public async Task Preserves_order_and_translates_every_item()
    {
        var batcher = new TranslationBatcher { MaxConcurrency = 2 };
        var input = Enumerable.Range(0, 50).Select(i => $"item{i}").ToList();

        // Fake engine: upper-cases text, preserving line structure (so batching succeeds).
        var result = await batcher.TranslateAsync(input, (t, _) => Task.FromResult(t.ToUpperInvariant()));

        Assert.Equal(input.Count, result.Count);
        for (var i = 0; i < input.Count; i++)
            Assert.Equal(input[i].ToUpperInvariant(), result[i]);
    }

    [Fact]
    public async Task Falls_back_to_per_item_when_line_count_mismatches()
    {
        var batcher = new TranslationBatcher();
        var input = new List<string> { "one", "two", "three" };

        // Adversarial engine: when handed a multi-line batch it drops the last line
        // (mismatch → must trigger per-item fallback); single lines get "!" appended.
        Func<string, CancellationToken, Task<string>> engine = (t, _) =>
        {
            var lines = t.Split('\n');
            if (lines.Length > 1)
                return Task.FromResult(string.Join('\n', lines.Take(lines.Length - 1).Select(l => l + "!")));
            return Task.FromResult(t + "!");
        };

        var result = await batcher.TranslateAsync(input, engine);

        // Despite the broken batch, every item is correctly translated via fallback.
        Assert.Equal(new[] { "one!", "two!", "three!" }, result);
    }

    [Fact]
    public async Task Respects_max_chunk_chars()
    {
        var maxSeen = 0;
        var batcher = new TranslationBatcher { MaxChunkChars = 3000, MaxConcurrency = 1 };

        // 100 strings of ~100 chars each → must be split into multiple requests.
        var input = Enumerable.Range(0, 100).Select(_ => new string('x', 100)).ToList();

        Func<string, CancellationToken, Task<string>> engine = (t, _) =>
        {
            maxSeen = Math.Max(maxSeen, t.Length);
            return Task.FromResult(t);
        };

        await batcher.TranslateAsync(input, engine);

        Assert.True(maxSeen <= 3000, $"A request of {maxSeen} chars exceeded the 3000 limit.");
    }

    [Fact]
    public async Task Multiline_values_are_translated_individually()
    {
        var batcher = new TranslationBatcher();
        var input = new List<string> { "single", "has\nnewline", "another" };

        var multilineWasCoBatched = false;
        Func<string, CancellationToken, Task<string>> engine = (t, _) =>
        {
            // The newline value must be sent alone, never joined with other items.
            if (t.Contains("has\nnewline") && t != "has\nnewline")
                multilineWasCoBatched = true;
            return Task.FromResult(t);
        };

        var result = await batcher.TranslateAsync(input, engine);

        Assert.False(multilineWasCoBatched);
        Assert.Equal(input, result);
    }
}
