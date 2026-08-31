using System.Globalization;
using optimizerDuck.Common.Extensions;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Test.Common.Extensions;

public class LocalizedObservableTests : IDisposable
{
    private readonly CultureInfo _originalCulture;

    public LocalizedObservableTests()
    {
        _originalCulture = Loc.CurrentCulture;
    }

    public void Dispose()
    {
        Loc.Instance.ChangeCulture(_originalCulture);
        GC.SuppressFinalize(this);
    }

    private sealed class TestLocalized : LocalizedObject
    {
        public string Greeting => "Greeting";
        public string Farewell => "Farewell";
        public int NotAString => 42;
    }

    [Fact]
    public void WhenLanguageChanges_RaisesPropertyChangedForAllBindings()
    {
        var sut = new TestLocalized();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Loc.Instance.ChangeCulture(new CultureInfo("vi"));

        // WPF convention: empty string means "all bindings" — the binding engine
        // re-evaluates every getter that wraps the localization indexer.
        Assert.Contains(string.Empty, raised);
    }

    [Fact]
    public void WhenLanguageChanges_DoesNotRaiseForNonStringProperties()
    {
        var sut = new TestLocalized();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Loc.Instance.ChangeCulture(new CultureInfo("ja"));

        // Empty-string broadcast still counts; the goal is to verify no
        // individual non-string property name is targeted.
        Assert.DoesNotContain(nameof(TestLocalized.NotAString), raised);
    }
}
