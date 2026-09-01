using System.Globalization;

namespace optimizerDuck.Domain.UI;

/// <summary>Single source of truth for supported UI languages. Easy to extend.</summary>
public static class SupportedLanguages
{
    public static IReadOnlyList<LanguageOption> All { get; } =
    [
        new() { DisplayName = "English", Culture = new CultureInfo("en-US") },
        new() { DisplayName = "Deutsch", Culture = new CultureInfo("de-DE") },
        new() { DisplayName = "Français", Culture = new CultureInfo("fr-FR") },
        new() { DisplayName = "Tiếng Việt", Culture = new CultureInfo("vi-VN") },
        new() { DisplayName = "正體中文", Culture = new CultureInfo("zh-TW") },
        new() { DisplayName = "简体中文", Culture = new CultureInfo("zh-CN") },
        new() { DisplayName = "Русский", Culture = new CultureInfo("ru-RU") },
        new() { DisplayName = "한국어", Culture = new CultureInfo("ko-KR") },
        new() { DisplayName = "日本語", Culture = new CultureInfo("ja-JP") },
        new() { DisplayName = "Polski", Culture = new CultureInfo("pl-PL") },
        new() { DisplayName = "Español", Culture = new CultureInfo("es-ES") },
        new() { DisplayName = "Português (BR)", Culture = new CultureInfo("pt-BR") },
        new() { DisplayName = "Türkçe", Culture = new CultureInfo("tr-TR") },
        new() { DisplayName = "עברית", Culture = new CultureInfo("he-IL") },
        new() { DisplayName = "العربية", Culture = new CultureInfo("ar-SA") },
        new() { DisplayName = "Italiano", Culture = new CultureInfo("it-IT") },
        new() { DisplayName = "Bahasa Indonesia", Culture = new CultureInfo("id-ID") },
    ];

    public static LanguageOption Default => All[0];
}
