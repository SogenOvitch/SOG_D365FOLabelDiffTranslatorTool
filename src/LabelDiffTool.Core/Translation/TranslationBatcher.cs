namespace LabelDiffTool.Core.Translation;

/// <summary>
/// Provider-agnostic batching for "full file" translation. Given many short texts it:
/// <list type="number">
///   <item>packs them into chunks whose joined length stays under <see cref="MaxChunkChars"/>;</item>
///   <item>sends each chunk as one newline-joined request (fewest calls);</item>
///   <item>verifies the response has the same number of lines, and if not, safely
///         falls back to translating that chunk one item at a time — so a mangled
///         delimiter can never misalign a translation onto the wrong label.</item>
/// </list>
/// Chunks run with bounded concurrency and per-request retry/back-off.
/// </summary>
public sealed class TranslationBatcher
{
    private const char Separator = '\n';

    /// <summary>Maximum characters per request (free Google endpoint sits around ~5000).</summary>
    public int MaxChunkChars { get; init; } = 3000;

    public int MaxConcurrency { get; init; } = 4;

    public int MaxRetries { get; init; } = 3;

    public TimeSpan BaseRetryDelay { get; init; } = TimeSpan.FromMilliseconds(500);

    /// <param name="translateSingle">
    /// Translates one opaque string (which may itself be a newline-joined chunk).
    /// This is the only provider-specific dependency.
    /// </param>
    public async Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> texts,
        Func<string, CancellationToken, Task<string>> translateSingle,
        IProgress<BatchProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(translateSingle);

        var results = new string[texts.Count];
        var chunks = BuildChunks(texts);
        var completed = 0;

        using var gate = new SemaphoreSlim(MaxConcurrency);
        var tasks = new List<Task>(chunks.Count);

        foreach (var chunk in chunks)
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    await ProcessChunkAsync(chunk, texts, results, translateSingle, ct).ConfigureAwait(false);
                    var done = Interlocked.Add(ref completed, chunk.Count);
                    progress?.Report(new BatchProgress(done, texts.Count));
                }
                finally
                {
                    gate.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }

    private async Task ProcessChunkAsync(
        Chunk chunk,
        IReadOnlyList<string> texts,
        string[] results,
        Func<string, CancellationToken, Task<string>> translateSingle,
        CancellationToken ct)
    {
        // Single-item or force-individual chunks: translate each on its own.
        if (chunk.Indices.Count == 1 || chunk.ForceIndividual)
        {
            foreach (var idx in chunk.Indices)
                results[idx] = await TranslateItemAsync(texts[idx], translateSingle, ct).ConfigureAwait(false);
            return;
        }

        // Fast path: one request for the whole chunk, joined by newline.
        var joined = string.Join(Separator, chunk.Indices.Select(i => texts[i]));
        var translated = await WithRetry(() => translateSingle(joined, ct), ct).ConfigureAwait(false);
        var parts = translated.Split(Separator);

        if (parts.Length == chunk.Indices.Count)
        {
            for (var k = 0; k < chunk.Indices.Count; k++)
                results[chunk.Indices[k]] = parts[k];
            return;
        }

        // Line count changed → don't trust the split. Re-translate item by item.
        foreach (var idx in chunk.Indices)
            results[idx] = await TranslateItemAsync(texts[idx], translateSingle, ct).ConfigureAwait(false);
    }

    private Task<string> TranslateItemAsync(
        string text,
        Func<string, CancellationToken, Task<string>> translateSingle,
        CancellationToken ct)
        => WithRetry(() => translateSingle(text, ct), ct);

    private async Task<string> WithRetry(Func<Task<string>> action, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (Exception) when (attempt < MaxRetries && !ct.IsCancellationRequested)
            {
                var delay = TimeSpan.FromMilliseconds(BaseRetryDelay.TotalMilliseconds * Math.Pow(2, attempt));
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }
    }

    private List<Chunk> BuildChunks(IReadOnlyList<string> texts)
    {
        var chunks = new List<Chunk>();
        var current = new Chunk();
        var currentLength = 0;

        for (var i = 0; i < texts.Count; i++)
        {
            var text = texts[i] ?? string.Empty;

            // A multi-line value or one at/over the limit can't share a newline-joined chunk safely.
            if (text.Contains(Separator) || text.Length >= MaxChunkChars)
            {
                FlushIfAny(chunks, ref current, ref currentLength);
                chunks.Add(new Chunk { ForceIndividual = true, Indices = { i } });
                continue;
            }

            var addedLength = current.Indices.Count == 0 ? text.Length : text.Length + 1; // +1 for the separator
            if (current.Indices.Count > 0 && currentLength + addedLength > MaxChunkChars)
                FlushIfAny(chunks, ref current, ref currentLength);

            current.Indices.Add(i);
            currentLength += current.Indices.Count == 1 ? text.Length : text.Length + 1;
        }

        FlushIfAny(chunks, ref current, ref currentLength);
        return chunks;
    }

    private static void FlushIfAny(List<Chunk> chunks, ref Chunk current, ref int currentLength)
    {
        if (current.Indices.Count > 0)
            chunks.Add(current);
        current = new Chunk();
        currentLength = 0;
    }

    private sealed class Chunk
    {
        public List<int> Indices { get; init; } = new();
        public bool ForceIndividual { get; set; }
        public int Count => Indices.Count;
    }
}
