using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cosmix.Application.Attendance.PunchRetention;

public static class PunchAuditSanitizer
{
    private static readonly HashSet<string> LocationKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Latitude", "Longitude", "AccuracyMeters", "Gps", "GpsCoordinates", "GpsLocation",
        "Coordinates", "Location", "Position"
    };

    public static (byte[] Sanitized, bool HadLocation) Sanitize(byte[] payload)
    {
        var root = JsonNode.Parse(payload) as JsonObject
            ?? throw new InvalidOperationException("Legacy punch audit payload is invalid.");
        if (root["operation"]?.GetValue<string>() != "attendance.punch.submit")
            throw new InvalidOperationException("Legacy punch audit operation does not match.");
        var hadLocation = RemoveLocation(root);
        return (JsonSerializer.SerializeToUtf8Bytes(root), hadLocation);
    }

    private static bool RemoveLocation(JsonNode? node)
    {
        var found = false;
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(x => x.Key).ToArray())
            {
                var value = obj[key];
                if (LocationKeys.Contains(key))
                {
                    if (value is not null) found = true;
                    obj.Remove(key);
                }
                else found |= RemoveLocation(value);
            }
        }
        else if (node is JsonArray array)
            foreach (var child in array) found |= RemoveLocation(child);
        return found;
    }
}
