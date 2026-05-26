namespace optimizerDuck.Domain.Customize.Models;

/// <summary>
/// Determines which UI control is rendered for a customize setting.
/// </summary>
public enum CustomizeControlType
{
    /// <summary>On/off toggle switch.</summary>
    Toggle,

    /// <summary>Dropdown (ComboBox) to pick from many options.</summary>
    Dropdown,

    /// <summary>Mutually exclusive visual options (e.g. Center/Left alignment).
    /// Options are rendered as radio buttons or a horizontal list.</summary>
    Option,

    /// <summary>Integer number input.</summary>
    NumberInt,

    /// <summary>Floating-point number input.</summary>
    NumberFloat,

    /// <summary>Free-form text input.</summary>
    String,
}
