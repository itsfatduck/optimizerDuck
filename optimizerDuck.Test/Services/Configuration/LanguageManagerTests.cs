using System.Collections;
using System.ComponentModel;
using System.Globalization;
using optimizerDuck.Domain.UI;
using optimizerDuck.Resources.Languages;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System.Primitives;

namespace optimizerDuck.Test.Services.Configuration;

public class LanguageManagerTests : IDisposable
{
    private readonly CultureInfo _originalCulture;

    public LanguageManagerTests()
    {
        _originalCulture = Loc.CurrentCulture;
    }

    public void Dispose()
    {
        // Reset culture after each test to avoid polluting other tests
        Loc.Instance.ChangeCulture(_originalCulture);
    }

    [Fact]
    public void ChangeCulture_SetsCurrentCulture()
    {
        var newCulture = new CultureInfo("vi");
        Loc.Instance.ChangeCulture(newCulture);

        Assert.Equal(newCulture, Loc.CurrentCulture);
    }

    [Fact]
    public void ChangeCulture_RaisesLanguageChangedEvent()
    {
        var newCulture = new CultureInfo("ja");
        CultureInfo? capturedCulture = null;
        EventHandler<LanguageChangedEventArgs> handler = (_, e) => capturedCulture = e.NewCulture;

        Loc.Instance.LanguageChanged += handler;
        try
        {
            Loc.Instance.ChangeCulture(newCulture);
            Assert.Equal(newCulture, capturedCulture);
        }
        finally
        {
            Loc.Instance.LanguageChanged -= handler;
        }
    }

    [Fact]
    public void ChangeCulture_RaisesItemIndexerPropertyChanged()
    {
        var newCulture = new CultureInfo("vi");
        var raisedProperties = new List<string?>();
        PropertyChangedEventHandler handler = (_, e) => raisedProperties.Add(e.PropertyName);

        Loc.Instance.PropertyChanged += handler;
        try
        {
            Loc.Instance.ChangeCulture(newCulture);
        }
        finally
        {
            Loc.Instance.PropertyChanged -= handler;
        }

        Assert.Contains("Item[]", raisedProperties);
        Assert.Contains(nameof(Loc.IsRtl), raisedProperties);
        Assert.Contains(nameof(Loc.Direction), raisedProperties);
    }

    [Fact]
    public void ChangeCulture_RaisesAllPropertiesChanged()
    {
        var raisedProperties = new List<string?>();
        PropertyChangedEventHandler handler = (_, e) => raisedProperties.Add(e.PropertyName);

        Loc.Instance.PropertyChanged += handler;
        try
        {
            Loc.Instance.ChangeCulture(new CultureInfo("vi"));
        }
        finally
        {
            Loc.Instance.PropertyChanged -= handler;
        }

        // Empty property name means "all properties changed". Pathless bindings to
        // Loc.Instance (the multi-arg {ext:Loc Key, Binding} markup) re-evaluate only on
        // this notification, so runtime language switching depends on it.
        Assert.Contains(string.Empty, raisedProperties);
    }

    [Fact]
    public void ChangeCulture_RaisesIsRtlAndDirection()
    {
        var raisedProperties = new List<string?>();
        PropertyChangedEventHandler handler = (_, e) => raisedProperties.Add(e.PropertyName);

        Loc.Instance.PropertyChanged += handler;
        try
        {
            Loc.Instance.ChangeCulture(new CultureInfo("vi"));
        }
        finally
        {
            Loc.Instance.PropertyChanged -= handler;
        }

        Assert.Contains(nameof(Loc.IsRtl), raisedProperties);
        Assert.Contains(nameof(Loc.Direction), raisedProperties);
    }

    [Fact]
    public void Indexer_ReturnsLocalizedStringForCurrentCulture()
    {
        Loc.Instance.ChangeCulture(new CultureInfo("en"));
        var english = Loc.Instance["Button.Ok"];

        Loc.Instance.ChangeCulture(new CultureInfo("vi"));
        var vietnamese = Loc.Instance["Button.Ok"];

        Assert.False(string.IsNullOrEmpty(english));
        Assert.False(string.IsNullOrEmpty(vietnamese));
    }

    [Fact]
    public void Indexer_ReturnsKeyWhenNotFound()
    {
        const string nonexistentKey = "NonExistent.Key.That.Does.Not.Exist";
        var result = Loc.Instance[nonexistentKey];
        Assert.Equal(nonexistentKey, result);
    }

    [Fact]
    public void Indexer_RefreshesAfterCultureChange()
    {
        Loc.Instance.ChangeCulture(new CultureInfo("en"));
        var beforeChange = Loc.Instance["Button.Ok"];

        Loc.Instance.ChangeCulture(new CultureInfo("vi"));
        var afterChange = Loc.Instance["Button.Ok"];

        // Strings should be present in both languages; assert non-empty
        Assert.False(string.IsNullOrEmpty(beforeChange));
        Assert.False(string.IsNullOrEmpty(afterChange));
    }

    [Fact]
    public void Invariant_ReturnsNeutralEnglishRegardlessOfCurrentCulture()
    {
        Loc.Instance.ChangeCulture(new CultureInfo("en"));
        var english = Loc.Invariant["Customize.Title"];

        Loc.Instance.ChangeCulture(new CultureInfo("vi-VN"));
        var vietnameseUi = Loc.Instance["Customize.Title"];
        var invariant = Loc.Invariant["Customize.Title"];

        // The UI-facing indexer follows the current culture...
        Assert.Equal("Tùy chỉnh", vietnameseUi);

        // ...while the invariant accessor keeps resolving the neutral English string.
        Assert.Equal("Customize", english);
        Assert.Equal(english, invariant);
    }

    [Fact]
    public void Invariant_FormatsArguments()
    {
        Assert.Equal(
            "Delete registry key Foo",
            ServiceStrings.Format(ServiceStrings.RegistryDescriptionDeleteKey, "Foo")
        );
    }

    [Fact]
    public void Invariant_ReturnsKeyWhenNotFound()
    {
        const string nonexistentKey = "NonExistent.Invariant.Key";
        Assert.Equal(nonexistentKey, Loc.Invariant[nonexistentKey]);
    }

    [Fact]
    public void EveryNeutralKey_ResolvesInEverySupportedLanguage()
    {
        var neutralKeys = Translations
            .ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, false)!
            .Cast<DictionaryEntry>()
            .Select(e => (string)e.Key)
            .ToList();

        // tryParents:false reads each satellite alone: ResourceManager fallback
        // would otherwise mask missing keys by returning the English value.
        var missing = new List<string>();
        foreach (var lang in SupportedLanguages.All)
        {
            var satelliteKeys =
                Translations
                    .ResourceManager.GetResourceSet(lang.Culture, true, false)
                    ?.Cast<DictionaryEntry>()
                    .Select(e => (string)e.Key)
                    .ToHashSet()
                ?? [];
            foreach (var key in neutralKeys)
                if (!satelliteKeys.Contains(key))
                    missing.Add($"{lang.Culture.Name}:{key}");
        }

        Assert.True(missing.Count == 0, "Missing translations:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void EveryLocale_PreservesNeutralPlaceholderSets()
    {
        var neutral = Translations
            .ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, false)!
            .Cast<DictionaryEntry>()
            .ToDictionary(e => (string)e.Key, e => (string?)e.Value);
        var indexPattern = new System.Text.RegularExpressions.Regex(@"\{(\d+)\}");
        var bad = new List<string>();
        foreach (var lang in SupportedLanguages.All)
        {
            var satellite =
                Translations
                    .ResourceManager.GetResourceSet(lang.Culture, true, false)
                    ?.Cast<DictionaryEntry>()
                    .ToDictionary(e => (string)e.Key, e => (string?)e.Value)
                ?? new Dictionary<string, string?>();
            foreach (var (key, neutralValue) in neutral)
            {
                var expected = indexPattern
                    .Matches(neutralValue ?? string.Empty)
                    .Select(m => m.Groups[1].Value)
                    .OrderBy(i => i)
                    .ToList();
                if (expected.Count == 0)
                    continue;
                if (!satellite.TryGetValue(key, out var localizedValue))
                    continue;
                var actual = indexPattern
                    .Matches(localizedValue ?? string.Empty)
                    .Select(m => m.Groups[1].Value)
                    .OrderBy(i => i)
                    .ToList();
                if (!expected.SequenceEqual(actual))
                    bad.Add($"{lang.Culture.Name}:{key}");
            }
        }
        Assert.True(bad.Count == 0, "Placeholder mismatches:\n" + string.Join("\n", bad));
    }
}
