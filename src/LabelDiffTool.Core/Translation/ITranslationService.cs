namespace LabelDiffTool.Core.Translation;

/// <summary>
/// Abstraction over a translation provider so the rest of the app never depends
/// on a concrete engine. Swap <c>GTranslateService</c> for a LibreTranslate-backed
/// implementation without touching callers.
/// </summary>
public interface ITranslationService
{
    /// <summary>Translates a single string. Empty/whitespace input is returned unchanged.</summary>
    Task<string> TranslateAsync(string text, string targetLanguage, string? sourceLanguage = null, CancellationToken ct = default);

    /// <summary>
    /// Translates many strings, internally batching them into requests to minimise calls
    /// against rate-limited free endpoints. Result order matches the input order.
    /// </summary>
    Task<IReadOnlyList<string>> TranslateBatchAsync(
        IReadOnlyList<string> texts,
        string targetLanguage,
        string? sourceLanguage = null,
        IProgress<BatchProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>Best-effort detection of the language of a piece of text; returns an ISO 639-1 code.</summary>
    Task<string> DetectLanguageAsync(string text, CancellationToken ct = default);
}
