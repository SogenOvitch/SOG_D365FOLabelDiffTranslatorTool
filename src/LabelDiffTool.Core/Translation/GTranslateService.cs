using GTranslate.Translators;

namespace LabelDiffTool.Core.Translation;

/// <summary>
/// <see cref="ITranslationService"/> backed by GTranslate's <see cref="AggregateTranslator"/>,
/// which uses the free web endpoints of Google/Bing/Yandex/Microsoft and falls back between
/// them. No API key required. Batching (incl. the 3000-char limit) is delegated to
/// <see cref="TranslationBatcher"/>.
/// </summary>
public sealed class GTranslateService : ITranslationService, IDisposable
{
    private readonly ITranslator _translator;
    private readonly TranslationBatcher _batcher;
    private readonly bool _ownsTranslator;

    public GTranslateService(ITranslator? translator = null, TranslationBatcher? batcher = null)
    {
        _ownsTranslator = translator is null;
        _translator = translator ?? new AggregateTranslator();
        _batcher = batcher ?? new TranslationBatcher { MaxChunkChars = 3000 };
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var target = NormalizeLanguage(targetLanguage)
            ?? throw new InvalidOperationException("A target language is required to translate.");
        var source = NormalizeLanguage(sourceLanguage);

        var result = source is null
            ? await _translator.TranslateAsync(text, target).ConfigureAwait(false)
            : await _translator.TranslateAsync(text, target, source).ConfigureAwait(false);

        return result.Translation;
    }

    public Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> texts,
        string targetLanguage,
        string? sourceLanguage = null,
        IProgress<BatchProgress>? progress = null,
        CancellationToken ct = default)
        => _batcher.TranslateAsync(
            texts,
            (chunk, token) => TranslateAsync(chunk, targetLanguage, sourceLanguage, token),
            progress,
            ct);

    public async Task<string> DetectLanguageAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "unknown";

        var language = await _translator.DetectLanguageAsync(text).ConfigureAwait(false);
        return language.ISO6391;
    }

    /// <summary>
    /// GTranslate expects ISO codes / language names, not full culture tags. Reduce
    /// "fr-FR" to "fr" so both file naming conventions and the engine are satisfied.
    /// </summary>
    private static string? NormalizeLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language) || language.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            return null;
        var dash = language.IndexOf('-');
        return dash > 0 ? language[..dash] : language;
    }

    public void Dispose()
    {
        if (_ownsTranslator && _translator is IDisposable d)
            d.Dispose();
    }
}
