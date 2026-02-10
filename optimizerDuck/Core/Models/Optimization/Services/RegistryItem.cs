using Microsoft.Win32;

namespace optimizerDuck.Core.Models.Optimization.Services;

public record struct RegistryItem
{
    public readonly RegistryValueKind Kind;
    public readonly string? Name;
    public readonly string Path;
    public readonly object? Value;

    public RegistryItem(string path, string name, object value, RegistryValueKind kind)
    {
        Path = path;
        Name = name;
        Value = value;
        Kind = kind;
    }

    public RegistryItem(string path, string name, object value)
    {
        Path = path;
        Name = name;
        Value = value;
        Kind = AutoDetectKind(value);
    }

    public RegistryItem(string path, string name)
    {
        Path = path;
        Name = name;
        Value = null;
        Kind = RegistryValueKind.Unknown;
    }

    public RegistryItem(string path)
    {
        Path = path;
        Name = null;
        Value = null;
        Kind = RegistryValueKind.Unknown;
    }

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