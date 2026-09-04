using System.Globalization;
using System.Windows.Data;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.System;

namespace optimizerDuck.Common.Converters;

/// <summary>Converts a <see cref="DeviceKind"/> to its localized display text.</summary>
public class DeviceKindConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is DeviceKind kind
            ? kind switch
            {
                DeviceKind.Desktop => Loc.Instance["Dashboard.SystemInfo.Os.DeviceType.Desktop"],
                DeviceKind.Laptop => Loc.Instance["Dashboard.SystemInfo.Os.DeviceType.Laptop"],
                _ => Loc.Instance["Common.Unknown"],
            }
            : Loc.Instance["Common.Unknown"];
    }

    public object ConvertBack(
        object? value,
        Type targetType,
        object? parameter,
        CultureInfo culture
    )
    {
        throw new NotSupportedException();
    }
}
