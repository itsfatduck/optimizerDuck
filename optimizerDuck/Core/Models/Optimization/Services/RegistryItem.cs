using Microsoft.Win32;

namespace optimizerDuck.Core.Models.Optimization.Services;

/// <summary>
///     Represents a single registry value or key to be read, written, or deleted.
/// </summary>
public record struct RegistryItem
{
    /// <summary>
    ///     The registry value kind (DWord, String, etc.).
    /// </summary>
    public readonly RegistryValueKind Kind;

    /// <summary>
    ///     The registry value name, or <c>null</c> to target the key itself.
    /// </summary>
    public readonly string? Name;

    /// <summary>
    ///     The full registry key path (e.g., <c>HKLM\SOFTWARE\...</c>).
    /// </summary>
    public readonly string Path;

    /// <summary>
    ///     The registry value data, or <c>null</c> if not applicable.
    /// </summary>
    public readonly object? Value;

    /// <summary>
    ///     Initializes a new <see cref="RegistryItem"/> with all fields specified.
    /// </summary>
    /// <param name="path">The registry key path.</param>
    /// <param name="name">The value name.</param>
    /// <param name="value">The value data.</param>
    /// <param name="kind">The value kind.</param>
    public RegistryItem(string path, string name, object value, RegistryValueKind kind)
    {
        Path = path;
        Name = name;
        Value = value;
        Kind = kind;
    }

    /// <summary>
    ///     Initializes a new <see cref="RegistryItem"/> with automatic kind detection.
    /// </summary>
    /// <param name="path">The registry key path.</param>
    /// <param name="name">The value name.</param>
    /// <param name="value">The value data (kind is auto-detected from the type).</param>
    public RegistryItem(string path, string name, object value)
    {
        Path = path;
        Name = name;
        Value = value;
        Kind = AutoDetectKind(value);
    }

    /// <summary>
    ///     Initializes a new <see cref="RegistryItem"/> for a named value without data (used for deletion).
    /// </summary>
    /// <param name="path">The registry key path.</param>
    /// <param name="name">The value name to target.</param>
    public RegistryItem(string path, string name)
    {
        Path = path;
        Name = name;
        Value = null;
        Kind = RegistryValueKind.Unknown;
    }

    /// <summary>
    ///     Initializes a new <see cref="RegistryItem"/> for a key path without a specific value (used for key-level operations).
    /// </summary>
    /// <param name="path">The registry key path.</param>
    public RegistryItem(string path)
    {
        Path = path;
        Name = null;
        Value = null;
        Kind = RegistryValueKind.Unknown;
    }

    /// <summary>
    ///     Auto-detects the <see cref="RegistryValueKind"/> based on the CLR type of the value.
    /// </summary>
    private static RegistryValueKind AutoDetectKind(object? value)
    {
        return value switch
        {
            null => RegistryValueKind.Unknown,
            int => RegistryValueKind.DWord,
            long => RegistryValueKind.QWord,
            string => RegistryValueKind.String,
            string[] => RegistryValueKind.MultiString,
            byte[] => RegistryValueKind.Binary,
            _ => RegistryValueKind.Unknown
        };
    }
}