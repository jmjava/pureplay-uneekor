using System.Globalization;
using System.Text.Json;

namespace UneekorRelaConnector;

/// <summary>
/// Parses ExPutt / community putting payloads.
/// Accepts a flat JSON object or GSPro Open Connect v1 shot JSON
/// (the dialect [springbok](https://github.com/springbok/MLM2PRO-GSPro-Connector) sends).
/// </summary>
internal static class ExputtPuttParser
{
    public static bool IsPutterClub(string? club)
    {
        if (string.IsNullOrWhiteSpace(club))
            return false;
        var c = club.Trim();
        return c.Equals("PT", StringComparison.OrdinalIgnoreCase)
               || c.Equals("PUTTER", StringComparison.OrdinalIgnoreCase)
               || c.Equals("PUTTING", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPuttingActive(string? mode, string? club, bool autoOnPutterClub)
        => string.Equals(mode, "PUTTING", StringComparison.OrdinalIgnoreCase)
           || (autoOnPutterClub && IsPutterClub(club));

    public static bool TryParse(string json, out ParsedPutt putt, out string? error)
    {
        putt = default;
        error = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            error = "empty payload";
            return false;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (Exception ex)
        {
            error = "invalid json: " + ex.Message;
            return false;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "json root must be an object";
                return false;
            }

            if (IsHeartbeat(root))
            {
                error = "heartbeat";
                return false;
            }

            var ball = root;
            if (TryGetProperty(root, out var ballData, "BallData", "ballData", "ball"))
                ball = ballData;

            var speed = Num(ball, "Speed", "speed", "BallSpeed", "ballspeed");
            if (!speed.HasValue || speed.Value <= 0)
            {
                error = "missing or invalid speed";
                return false;
            }

            var hla = Num(ball, "HLA", "hla", "LaunchDirection") ?? 0m;
            var vla = Num(ball, "VLA", "vla", "LaunchAngle") ?? 0m;

            JsonElement club = default;
            var hasClub = TryGetProperty(root, out club, "ClubData", "clubData", "club");
            var path = hasClub
                ? Num(club, "Path", "path", "ClubPath") ?? 0m
                : Num(root, "Path", "path", "ClubPath") ?? 0m;
            var face = hasClub
                ? Num(club, "FaceToTarget", "faceToTarget", "face_to_target") ?? 0m
                : Num(root, "FaceToTarget", "faceToTarget", "face_to_target") ?? 0m;

            var source = Str(root, "DeviceID", "deviceId", "source") ?? "exputt";
            putt = new ParsedPutt(
                Source: source,
                Speed: speed.Value,
                Hla: hla,
                Vla: vla,
                Path: path,
                FaceToTarget: face);
            return true;
        }
    }

    public static bool IsHeartbeat(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return IsHeartbeat(doc.RootElement);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsHeartbeat(JsonElement root)
    {
        if (!TryGetProperty(root, out var options, "ShotDataOptions", "shotDataOptions"))
            return false;
        if (Flag(options, "IsHeartBeat", "isHeartBeat"))
            return true;
        if (TryGetProperty(options, out var contains, "ContainsBallData", "containsBallData")
            && contains.ValueKind is JsonValueKind.False)
            return true;
        return false;
    }

    private static bool Flag(JsonElement obj, params string[] names)
    {
        if (!TryGetProperty(obj, out var el, names))
            return false;
        return el.ValueKind == JsonValueKind.True
               || (el.ValueKind == JsonValueKind.String
                   && bool.TryParse(el.GetString(), out var b)
                   && b);
    }

    private static string? Str(JsonElement obj, params string[] names)
    {
        return TryGetProperty(obj, out var el, names) && el.ValueKind == JsonValueKind.String
            ? el.GetString()
            : null;
    }

    private static decimal? Num(JsonElement obj, params string[] names)
    {
        if (!TryGetProperty(obj, out var el, names))
            return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var n))
            return n;
        if (el.ValueKind == JsonValueKind.String
            && decimal.TryParse(el.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    private static bool TryGetProperty(JsonElement obj, out JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetProperty(name, out value))
                return true;
        }

        value = default;
        return false;
    }
}

internal readonly record struct ParsedPutt(
    string Source,
    decimal Speed,
    decimal Hla,
    decimal Vla,
    decimal Path,
    decimal FaceToTarget);
