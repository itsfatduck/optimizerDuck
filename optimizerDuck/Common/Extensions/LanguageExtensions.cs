using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using optimizerDuck.Services.Managers;

namespace optimizerDuck.Common.Extensions;

/// <summary>
///     maybe i will add change language runtime support later
/// </summary>
public class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    public LocExtension(string key, BindingBase arg1) : this(key)
    {
        Args.Add(arg1);
    }

    public LocExtension(string key, BindingBase arg1, BindingBase arg2) : this(key)
    {
        Args.Add(arg1);
        Args.Add(arg2);
    }

    public LocExtension(string key, BindingBase arg1, BindingBase arg2, BindingBase arg3) : this(key)
    {
        Args.Add(arg1);
        Args.Add(arg2);
        Args.Add(arg3);
    }

    public LocExtension(string key, BindingBase arg1, BindingBase arg2, BindingBase arg3, BindingBase arg4) : this(key)
    {
        Args.Add(arg1);
        Args.Add(arg2);
        Args.Add(arg3);
        Args.Add(arg4);
    }

    public LocExtension(string key, BindingBase arg1, BindingBase arg2, BindingBase arg3, BindingBase arg4,
        BindingBase arg5) : this(key)
    {
        Args.Add(arg1);
        Args.Add(arg2);
        Args.Add(arg3);
        Args.Add(arg4);
        Args.Add(arg5);
    }

    public string? Key { get; set; }

    public List<BindingBase> Args { get; } = [];

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (Args.Count == 0)
            return new Binding($"[{Key}]")
            {
                Source = Loc.Instance,
                Mode = BindingMode.OneWay
            }.ProvideValue(serviceProvider);

        var mb = new MultiBinding
        {
            Converter = new LocDynamicConverter(),
            ConverterParameter = Key
        };

        // [0] Loc.Instance (required OneWay)
        mb.Bindings.Add(new Binding
        {
            Source = Loc.Instance,
            Mode = BindingMode.OneWay
        });

        foreach (var arg in Args)
        {
            if (arg is Binding b)
                b.Mode = BindingMode.OneWay;

            mb.Bindings.Add(arg);
        }

        return mb.ProvideValue(serviceProvider);
    }
}

public class LocDynamicConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length == 0 || values[0] is not Loc loc)
            return "";

        if (parameter is not string key)
            return "";

        var format = loc[key];
        if (values.Length == 1)
            return format;

        // skip Loc.Instance, get args
        var args = values.Skip(1).ToArray();
        return string.Format(culture, format, args);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}