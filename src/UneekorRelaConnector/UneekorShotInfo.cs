using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UneekorRelaConnector;

/// <summary>
/// Minimal model for Uneekor VIEW ShotData/shotinfo.json (Open-Birdie field names).
/// </summary>
internal sealed class UneekorShotInfo
{
    [JsonPropertyName("DATA")]
    public UneekorShotData? Data { get; set; }
}

internal sealed class UneekorShotData
{
    [JsonPropertyName("ballspeed")]
    public string? BallSpeed { get; set; }

    [JsonPropertyName("incline")]
    public string? Incline { get; set; }

    [JsonPropertyName("azimuth")]
    public string? Azimuth { get; set; }

    [JsonPropertyName("backspin")]
    public string? BackSpin { get; set; }

    [JsonPropertyName("sidespin")]
    public string? SideSpin { get; set; }

    [JsonPropertyName("spinmag2d")]
    public string? SpinMag2d { get; set; }

    [JsonPropertyName("spinaxis2d")]
    public string? SpinAxis2d { get; set; }
}

internal sealed class UneekorProShotInfo
{
    [JsonPropertyName("ClubName")]
    public string? ClubName { get; set; }

    [JsonPropertyName("Club")]
    public string? Club { get; set; }
}

internal static class UneekorShotParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryParseShotFolder(string folderPath, out ParsedUneekorShot shot, out string? error)
    {
        shot = default!;
        error = null;

        var shotInfoPath = Path.Combine(folderPath, "shotinfo.json");
        if (!File.Exists(shotInfoPath))
        {
            error = "shotinfo.json missing";
            return false;
        }

        UneekorShotInfo? info;
        try
        {
            info = JsonSerializer.Deserialize<UneekorShotInfo>(File.ReadAllText(shotInfoPath), JsonOptions);
        }
        catch (Exception ex)
        {
            error = "shotinfo.json not ready: " + ex.Message;
            return false;
        }

        var data = info?.Data;
        if (data is null)
        {
            error = "DATA missing";
            return false;
        }

        var speed = Num(data.BallSpeed);
        if (!speed.HasValue || speed.Value <= 0)
        {
            error = "invalid ballspeed";
            return false;
        }

        var back = Num(data.BackSpin) ?? 0m;
        var side = Num(data.SideSpin) ?? 0m;
        var total = Num(data.SpinMag2d);
        if (!total.HasValue)
            total = (decimal)Math.Sqrt((double)(back * back + side * side));

        string? club = null;
        var proPath = Path.Combine(folderPath, "ProShotInfo.json");
        if (File.Exists(proPath))
        {
            try
            {
                var pro = JsonSerializer.Deserialize<UneekorProShotInfo>(File.ReadAllText(proPath), JsonOptions);
                club = string.IsNullOrWhiteSpace(pro?.ClubName) ? pro?.Club : pro?.ClubName;
            }
            catch
            {
                // optional
            }
        }

        shot = new ParsedUneekorShot(
            FolderName: Path.GetFileName(folderPath),
            Speed: speed.Value,
            Vla: Num(data.Incline) ?? 0m,
            Hla: Num(data.Azimuth) ?? 0m,
            BackSpin: back,
            SideSpin: side,
            TotalSpin: total.Value,
            SpinAxis: Num(data.SpinAxis2d) ?? 0m,
            ClubName: club);

        return true;
    }

    private static decimal? Num(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        return decimal.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }
}

internal readonly record struct ParsedUneekorShot(
    string FolderName,
    decimal Speed,
    decimal Vla,
    decimal Hla,
    decimal BackSpin,
    decimal SideSpin,
    decimal TotalSpin,
    decimal SpinAxis,
    string? ClubName);
