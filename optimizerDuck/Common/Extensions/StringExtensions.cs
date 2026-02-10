using System.Management.Automation;
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
            .Replace("#< CLIXML", string.Empty)
            .Replace("< CLIXML", string.Empty)
            .TrimStart('\uFEFF', '\r', '\n', ' ', '\t')
            .Replace("_x000D__x000A_", "\n");

        if (!cleaned.StartsWith("<Objs", StringComparison.OrdinalIgnoreCase))
            cleaned = "<Objs>" + cleaned + "</Objs>";

        try
        {
            var deserialized = PSSerializer.Deserialize(cleaned);

            return deserialized switch
            {
                IEnumerable<object> list => string.Join(Environment.NewLine,
                    list.Where(_ => true).Select(o => o.ToString()?.Trim() ?? string.Empty)),
                _ => deserialized?.ToString()?.Trim() ?? string.Empty
            };
        }
        catch
        {
            return cleaned.ExtractRawText();
        }
    }

    private static string ExtractRawText(this string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var tags = new[] { "S", "E", "L", "ToString" };

            var values = doc.Descendants()
                .Where(x => tags.Contains(x.Name.LocalName))
                .Select(x => x.Value.Trim())
                .ToList();

            if (values.Count == 0)
                values.Add(doc.Root?.Value.Trim() ?? string.Empty);

            return string.Join(Environment.NewLine, values)
                .Replace("_x000D__x000A_", "\n")
                .Trim();
        }
        catch
        {
            return xml;
        }
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
        if (time.TotalSeconds >= 1) return $"{time.TotalSeconds:F1}s";
        if (time.TotalMilliseconds >= 1) return $"{time.TotalMilliseconds:F0}ms";
        return time.Ticks > 0 ? $"{time.Ticks} ticks" : "0";
    }
}