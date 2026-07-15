using LabelDiffTool.Core.Models;

namespace LabelDiffTool.Core.Translation;

/// <summary>
/// Domain-level translation operations over label files. Encapsulates the "mode A" copy rule:
/// the label id is preserved exactly, only the text is translated, and the description is
/// carried over verbatim (never translated). Final on-disk ordering is owned by the writer,
/// which sorts by label id, so entries are simply added here.
/// </summary>
public sealed class LabelTranslationService
{
    private readonly ITranslationService _translator;

    public LabelTranslationService(ITranslationService translator)
        => _translator = translator ?? throw new ArgumentNullException(nameof(translator));

    /// <summary>
    /// Copies every label present in <paramref name="source"/> but missing from
    /// <paramref name="target"/>, translating the text into the target's language and
    /// carrying the description over unchanged. Returns the number of labels added.
    /// </summary>
    public async Task<int> TranslateMissingAsync(
        LabelFile source,
        LabelFile target,
        IProgress<BatchProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var missing = source.Entries.Where(e => !target.Contains(e.Id)).ToList();
        if (missing.Count == 0)
            return 0;

        var texts = missing.Select(e => e.Text).ToList();
        var translated = await _translator
            .TranslateBatchAsync(texts, target.Language, source.Language, progress, ct)
            .ConfigureAwait(false);

        for (var i = 0; i < missing.Count; i++)
            target.AddOrReplace(BuildTranslatedEntry(missing[i], translated[i]));

        return missing.Count;
    }

    /// <summary>
    /// Copies a specific set of label ids from <paramref name="source"/> into
    /// <paramref name="target"/> in one batched pass, translating each text. Ids that the source
    /// does not define are skipped. Returns the number of labels written.
    /// </summary>
    public async Task<int> TranslateLabelsAsync(
        LabelFile source,
        LabelFile target,
        IEnumerable<string> labelIds,
        IProgress<BatchProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        var entries = labelIds
            .Distinct(StringComparer.Ordinal)
            .Select(source.Find)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();
        if (entries.Count == 0)
            return 0;

        var texts = entries.Select(e => e.Text).ToList();
        var translated = await _translator
            .TranslateBatchAsync(texts, target.Language, source.Language, progress, ct)
            .ConfigureAwait(false);

        for (var i = 0; i < entries.Count; i++)
            target.AddOrReplace(BuildTranslatedEntry(entries[i], translated[i]));

        return entries.Count;
    }

    /// <summary>
    /// Copies a single label id from <paramref name="source"/> into <paramref name="target"/>,
    /// translating its text. No-op if the label is absent from the source.
    /// </summary>
    public async Task<bool> TranslateOneAsync(
        LabelFile source,
        LabelFile target,
        string labelId,
        CancellationToken ct = default)
    {
        if (!source.TryGet(labelId, out var sourceEntry))
            return false;

        var text = await _translator
            .TranslateAsync(sourceEntry.Text, target.Language, source.Language, ct)
            .ConfigureAwait(false);

        target.AddOrReplace(BuildTranslatedEntry(sourceEntry, text));
        return true;
    }

    private static LabelEntry BuildTranslatedEntry(LabelEntry source, string translatedText) =>
        new(source.Id, translatedText, source.Description, isMachineTranslated: true, isUnsaved: true);
}
