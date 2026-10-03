using System.Text.Json;
using Azure.Search.Documents.Models;

namespace Microservices.Infrastructure.Utilities;

/// <summary>
/// Normalizes Azure AI Search document fields (scalar or collection) for API responses.
/// </summary>
public static class SearchDocumentReader
{
    public static string? GetString(SearchDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var v) || v == null)
            return null;
        if (v is string s)
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        if (v is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String)
            {
                var str = je.GetString();
                return string.IsNullOrWhiteSpace(str) ? null : str.Trim();
            }
            if (je.ValueKind == JsonValueKind.Array)
                return JoinList(ExtractStringList(je), ", ");
        }
        var text = v.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Reads a field as a single display string: collections become comma-separated values.
    /// </summary>
    public static string? GetStringOrJoinedArray(SearchDocument doc, string key, string separator = ", ")
    {
        var list = ExtractStringList(doc, key);
        if (list.Count > 0)
            return JoinList(list, separator);

        return GetString(doc, key);
    }

    public static string? JoinList(IReadOnlyList<string> values, string separator = ", ") =>
        values.Count > 0 ? string.Join(separator, values) : null;

    public static List<string> ExtractStringList(SearchDocument doc, string fieldName)
    {
        if (!doc.TryGetValue(fieldName, out var obj) || obj == null)
            return new List<string>();

        return ExtractStringList(obj);
    }

    public static List<string> ExtractStringList(object obj)
    {
        if (obj is string s)
            return string.IsNullOrWhiteSpace(s)
                ? new List<string>()
                : new List<string> { s.Trim() };

        if (obj is JsonElement je)
            return ExtractStringList(je);

        if (obj is IEnumerable<string> stringEnumerable)
        {
            return stringEnumerable
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .ToList();
        }

        if (obj is System.Collections.IEnumerable enumerable && obj is not string)
        {
            var values = new List<string>();
            foreach (var item in enumerable)
            {
                var value = CoerceToString(item);
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value.Trim());
            }
            return values;
        }

        var fallback = obj.ToString();
        return string.IsNullOrWhiteSpace(fallback)
            ? new List<string>()
            : new List<string> { fallback.Trim() };
    }

    private static List<string> ExtractStringList(JsonElement je)
    {
        if (je.ValueKind == JsonValueKind.String)
        {
            var s = je.GetString();
            return string.IsNullOrWhiteSpace(s) ? new List<string>() : new List<string> { s.Trim() };
        }

        if (je.ValueKind != JsonValueKind.Array)
            return new List<string>();

        var values = new List<string>();
        foreach (var element in je.EnumerateArray())
        {
            var value = CoerceToString(element);
            if (!string.IsNullOrWhiteSpace(value))
                values.Add(value.Trim());
        }

        return values;
    }

    private static string? CoerceToString(object? item)
    {
        return item switch
        {
            null => null,
            string s => s,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            JsonElement element when element.ValueKind == JsonValueKind.Number => element.GetRawText(),
            JsonElement element when element.ValueKind == JsonValueKind.True => "true",
            JsonElement element when element.ValueKind == JsonValueKind.False => "false",
            JsonElement element => element.ToString(),
            _ => item.ToString()
        };
    }
}
