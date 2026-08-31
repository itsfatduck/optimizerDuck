using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Common.Extensions;

/// <summary>
///     Base class for ViewModels and domain models that expose localized strings.
///     Auto-subscribes to <see cref="Loc.LanguageChanged" /> and:
///     <list type="number">
///         <item>
///             Raises <see cref="ObservableObject.OnPropertyChanged(string)" /> with the WPF
///             sentinel <c>string.Empty</c> so every binding whose source is this object
///             re-evaluates its getter (for properties expressed as
///             <c>=&gt; Loc.Instance["key"]</c>).
///         </item>
///         <item>
///             Invokes the virtual <see cref="OnLanguageChanged" /> hook so subclasses can
///             refresh cached backing fields and rebuild collections of localized strings
///             that the empty-string PropertyChanged broadcast cannot reach on its own.
///         </item>
///     </list>
/// </summary>
/// <remarks>
///     Conventions:
///     <list type="bullet">
///         <item>
///             Localized expression-bodied properties: <c>public string Name =&gt; Loc.Instance["X.Y.Name"];</c>.
///             The binding re-evaluates the getter on culture change automatically.
///         </item>
///         <item>
///             For values cached into a backing field, override
///             <see cref="OnLanguageChanged" /> and re-assign the field so the generated
///             setter raises PropertyChanged for that property name.
///         </item>
///         <item>
///             For collections of localized strings (e.g. grouped sections), override
///             <see cref="OnLanguageChanged" /> and reassign the collection so WPF rebinds
///             the ItemsSource, or raise <c>OnPropertyChanged("Sections")</c>.
///         </item>
///     </list>
/// </remarks>
public abstract class LocalizedObject : ObservableObject
{
    protected LocalizedObject()
    {
        // Loc lives for whole app. Use weak event so ViewModels can be freed (GC) when closed.
        Loc.AddWeakLanguageChangedHandler(OnLanguageChangedCore);
    }

    private void OnLanguageChangedCore(object? sender, LanguageChangedEventArgs e)
    {
        OnPropertyChanged(string.Empty);
        OnLanguageChanged(e.NewCulture);
    }

    /// <summary>Called after language changes. Override to update cached values.</summary>
    protected virtual void OnLanguageChanged(CultureInfo newCulture) { }
}
