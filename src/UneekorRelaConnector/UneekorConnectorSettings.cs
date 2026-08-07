using System.IO;
using System.Text.Json;

namespace UneekorRelaConnector;

internal sealed class UneekorConnectorSettings
{
    public string Handedness { get; set; } = "RH";
    public string Mode { get; set; } = "NORMAL";
    public string? ShotDataDirectory { get; set; }
    public decimal SpeedScale { get; set; } = 1m;
    public bool InvertHla { get; set; }

    public static UneekorConnectorSettings Load()
    {
        var path = GetPath();
        if (!File.Exists(path))
            return new UneekorConnectorSettings();

        var settings = JsonSerializer.Deserialize<UneekorConnectorSettings>(File.ReadAllText(path));
        settings ??= new UneekorConnectorSettings();
        settings.Normalize();
        return settings;
    }

    public void Save()
    {
        Normalize();
        var path = GetPath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(
            path,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public UneekorConnectorSettings Copy()
        => (UneekorConnectorSettings)MemberwiseClone();

    private void Normalize()
    {
        Handedness = string.Equals(Handedness, "LH", StringComparison.OrdinalIgnoreCase) ? "LH" : "RH";
        Mode = Mode?.Trim().ToUpperInvariant() switch
        {
            "PUTTING" => "PUTTING",
            "CHIPPING" => "CHIPPING",
            _ => "NORMAL"
        };
        if (SpeedScale <= 0) SpeedScale = 1m;
    }

    private static string GetPath()
        => Path.Combine(AppContext.BaseDirectory, "Settings", "Other", "uneekor-rela-connector.json");
}
