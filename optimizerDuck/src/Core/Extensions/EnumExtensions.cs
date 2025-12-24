using System.ComponentModel;
using System.Reflection;

namespace optimizerDuck.Core.Extensions;

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        if (value == null) return string.Empty;

        var type = value.GetType();
        var name = value.ToString();

        var field = type.GetField(name);
        if (field == null)
            return name;

        var attribute = field.GetCustomAttribute<DescriptionAttribute>(false);
        return attribute?.Description ?? name;
    }
}