namespace LabelDiffTool.Core.Translation;

/// <summary>A language option for UI selection: the D365 culture tag plus a display name.</summary>
public sealed record LanguageOption(string Tag, string DisplayName)
{
    public override string ToString() => $"{DisplayName} ({Tag})";
}

/// <summary>
/// A curated list of languages commonly used in D365 F&O deployments, for the
/// language dropdowns. Not exhaustive — the UI also allows free text / detection.
/// </summary>
public static class LanguageCatalog
{
    public static IReadOnlyList<LanguageOption> Common { get; } = new List<LanguageOption>
    {
        new("en", "English"),
        new("en-US", "English (US)"),
        new("en-GB", "English (UK)"),
        new("fr", "French"),
        new("fr-FR", "French (France)"),
        new("fr-CA", "French (Canada)"),
        new("de", "German"),
        new("de-DE", "German (Germany)"),
        new("es", "Spanish"),
        new("es-ES", "Spanish (Spain)"),
        new("es-MX", "Spanish (Mexico)"),
        new("it", "Italian"),
        new("it-IT", "Italian (Italy)"),
        new("nl", "Dutch"),
        new("nl-NL", "Dutch (Netherlands)"),
        new("pt", "Portuguese"),
        new("pt-BR", "Portuguese (Brazil)"),
        new("pt-PT", "Portuguese (Portugal)"),
        new("pl", "Polish"),
        new("pl-PL", "Polish (Poland)"),
        new("sv", "Swedish"),
        new("sv-SE", "Swedish (Sweden)"),
        new("da", "Danish"),
        new("da-DK", "Danish (Denmark)"),
        new("nb", "Norwegian (Bokmål)"),
        new("nb-NO", "Norwegian (Norway)"),
        new("fi", "Finnish"),
        new("fi-FI", "Finnish (Finland)"),
        new("cs", "Czech"),
        new("cs-CZ", "Czech (Czechia)"),
        new("ru", "Russian"),
        new("ru-RU", "Russian (Russia)"),
        new("tr", "Turkish"),
        new("tr-TR", "Turkish (Türkiye)"),
        new("ja", "Japanese"),
        new("ja-JP", "Japanese (Japan)"),
        new("ko", "Korean"),
        new("ko-KR", "Korean (Korea)"),
        new("zh", "Chinese"),
        new("zh-Hans", "Chinese (Simplified)"),
        new("zh-Hant", "Chinese (Traditional)"),
        new("ar", "Arabic"),
    };

    public static LanguageOption? FindByTag(string? tag) =>
        tag is null ? null : Common.FirstOrDefault(l => string.Equals(l.Tag, tag, StringComparison.OrdinalIgnoreCase));
}
