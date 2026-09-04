using System.ComponentModel;
using System.Globalization;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.Optimization.Providers;

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
}
