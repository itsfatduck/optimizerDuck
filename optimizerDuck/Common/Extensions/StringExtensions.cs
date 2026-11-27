using System.Text;
using System.Xml.Linq;

namespace optimizerDuck.Common.Extensions;

public static class TextExtensions
{
    public static string ParseCliXml(this string? cliXml)
    {
        if (string.IsNullOrWhiteSpace(cliXml))
            return string.Empty;

        var cleaned = cliXml
            .Replace("#< CLIXML", string.Empty, StringComparison.Ordinal)
            .Replace("< CLIXML", string.Empty, StringComparison.Ordinal)
            .TrimStart('\uFEFF', '\r', '\n', ' ', '\t');

        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        if (!cleaned.StartsWith('<'))
            return DecodeCliXmlEscapes(cleaned).Trim();

        var xmlText = cleaned.StartsWith("<Objs", StringComparison.OrdinalIgnoreCase)
            ? cleaned
            : $"<Objs>{cleaned}</Objs>";

        try
        {
            var doc = XDocument.Parse(xmlText);
            if (doc.Root is null)
                return string.Empty;

            var values = new List<string>();
            foreach (var element in doc.Root.Elements())
                CollectCliXmlText(element, values);

            if (values.Count == 0)
            {
                var rootText = DecodeCliXmlEscapes(doc.Root.Value).Trim();
                if (!string.IsNullOrEmpty(rootText))
                    values.Add(rootText);
            }

            return string.Join(Environment.NewLine, values).Trim();
        }
        catch
        {
            return DecodeCliXmlEscapes(cleaned).Trim();
        }
    }

    private static void CollectCliXmlText(XElement element, List<string> values)
    {
        switch (element.Name.LocalName)
        {
            case "S":
                if (
                    string.Equals(
                        (string?)element.Attribute("S"),
                        "progress",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    return;
                AddDecoded(element.Value, values);
                break;

            case "E":
            case "L":
            case "ToString":
                AddDecoded(element.Value, values);
                break;

            case "Obj":
            {
                var toStringEl = element
                    .Elements()
                    .FirstOrDefault(e => e.Name.LocalName == "ToString");
                if (toStringEl is not null)
                {
                    var text = DecodeCliXmlEscapes(toStringEl.Value).Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        values.Add(text);
                        return;
                    }
                }

                foreach (var child in element.Descendants())
                {
                    if (child.Name.LocalName is "S" or "E" or "L")
                    {
                        if (
                            child.Name.LocalName == "S"
                            && string.Equals(
                                (string?)child.Attribute("S"),
                                "progress",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                            continue;
                        AddDecoded(child.Value, values);
                    }
                }
                break;
            }

            default:
                foreach (var child in element.Descendants())
                {
                    if (child.Name.LocalName is "S" or "E" or "L" or "ToString")
                    {
                        if (
                            child.Name.LocalName == "S"
                            && string.Equals(
                                (string?)child.Attribute("S"),
                                "progress",
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                            continue;
                        AddDecoded(child.Value, values);
                    }
                }
                break;
        }
    }

    private static void AddDecoded(string rawValue, List<string> values)
    {
        var decoded = DecodeCliXmlEscapes(rawValue).Trim();
        if (!string.IsNullOrEmpty(decoded))
            values.Add(decoded);
    }

    /// <summary>
    ///     Decodes PowerShell CLIXML escape sequences such as <c>_x000D__x000A_</c> and
    ///     <c>_xHHHH_</c> without invoking <c>System.Management.Automation</c>.
    /// </summary>
    internal static string DecodeCliXmlEscapes(string text)
    {
        if (string.IsNullOrEmpty(text) || !text.Contains("_x", StringComparison.Ordinal))
            return text;

        var span = text.AsSpan();
        var sb = new StringBuilder(text.Length);
        var i = 0;

        while (i < span.Length)
        {
            if (
                i + 13 < span.Length
                && span[i..].StartsWith("_x000D__x000A_", StringComparison.OrdinalIgnoreCase)
            )
            {
                sb.Append('\n');
                i += 14;
                continue;
            }

            if (
                i + 6 < span.Length
                && span[i] == '_'
                && span[i + 1] == 'x'
                && span[i + 6] == '_'
                && TryParseHex4(span.Slice(i + 2, 4), out var ch)
            )
            {
                sb.Append(ch);
                i += 7;
                continue;
            }

            sb.Append(span[i]);
            i++;
        }

        return sb.ToString();
    }

    private static bool TryParseHex4(ReadOnlySpan<char> hex, out char ch)
    {
        var value = 0;
        for (var i = 0; i < 4; i++)
        {
            var c = hex[i];
            int digit;
            if (c is >= '0' and <= '9')
                digit = c - '0';
            else if (c is >= 'a' and <= 'f')
                digit = c - 'a' + 10;
            else if (c is >= 'A' and <= 'F')
                digit = c - 'A' + 10;
            else
            {
                ch = default;
                return false;
            }

            value = (value << 4) | digit;
        }

        ch = (char)value;
        return true;
    }

    public static string EncodeBase64(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        try
        {
            var bytes = Encoding.Unicode.GetBytes(value);
            return Convert.ToBase64String(bytes);
        }
        catch
        {
            return value;
        }
    }

    public static string DecodeBase64(this string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        try
        {
            var bytes = Convert.FromBase64String(value);
            return Encoding.Unicode.GetString(bytes);
        }
        catch
        {
            return value;
        }
    }

    public static string FormatTime(this TimeSpan time)
    {
        if (time.TotalSeconds >= 1)
            return $"{time.TotalSeconds:F1}s";
        if (time.TotalMilliseconds >= 1)
            return $"{time.TotalMilliseconds:F0}ms";
        return time.Ticks > 0 ? $"{time.Ticks} ticks" : "0";
    }
}
