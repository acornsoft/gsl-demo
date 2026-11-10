using System.Globalization;
using System.Text.Json;

namespace Gsl.Functions.RegE;

/// <summary>Validates the JSON body. Returns a clear error message for HTTP 400.</summary>
public static class RegERequestParser
{
    public static (RegEDeadlineRequest? Request, string? Error) Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return (null, "Request body is required: {\"noticeDate\":\"yyyy-MM-dd\",\"newAccount\":false,\"posOrForeign\":false}");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(body); }
        catch (JsonException) { return (null, "Request body is not valid JSON."); }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return (null, "Request body must be a JSON object.");

            if (!TryGet(root, "noticeDate", out var nd) || nd.ValueKind != JsonValueKind.String)
                return (null, "noticeDate is required and must be a string in yyyy-MM-dd format.");
            if (!DateOnly.TryParseExact(nd.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var noticeDate))
                return (null, $"noticeDate '{nd.GetString()}' is not a valid date in yyyy-MM-dd format.");

            var (newAccount, e1) = ReadBool(root, "newAccount");
            if (e1 is not null) return (null, e1);
            var (posOrForeign, e2) = ReadBool(root, "posOrForeign");
            if (e2 is not null) return (null, e2);

            return (new RegEDeadlineRequest(noticeDate, newAccount, posOrForeign), null);
        }
    }

    private static (bool, string?) ReadBool(JsonElement root, string name)
    {
        if (!TryGet(root, name, out var v) || v.ValueKind == JsonValueKind.Null) return (false, null);
        return v.ValueKind switch
        {
            JsonValueKind.True => (true, null),
            JsonValueKind.False => (false, null),
            _ => (false, $"{name} must be a boolean (true or false)."),
        };
    }

    private static bool TryGet(JsonElement root, string name, out JsonElement value)
    {
        foreach (var p in root.EnumerateObject())
        {
            if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) { value = p.Value; return true; }
        }
        value = default;
        return false;
    }
}
