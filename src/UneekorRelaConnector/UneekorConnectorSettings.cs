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

    /// <summary>None | File | OpenConnect | Both</summary>
    public string PuttingSource { get; set; } = "Both";
    public string? PuttingDirectory { get; set; }
    public string OpenConnectBind { get; set; } = "127.0.0.1";
    public int OpenConnectPort { get; set; } = 921;
    public bool IgnoreUneekorWhilePutting { get; set; } = true;
    public bool AutoPuttingOnPutterClub { get; set; } = true;
    public bool InvertPuttHla { get; set; }
    public decimal PuttSpeedScale { get; set; } = 1m;

    public bool FilePuttingEnabled =>
        PuttingSource is "File" or "Both";

    public bool OpenConnectPuttingEnabled =>
        PuttingSource is "OpenConnect" or "Both";

    public bool PuttingEnabled => FilePuttingEnabled || OpenConnectPuttingEnabled;

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

    public string ResolvePuttingDirectory()
        => string.IsNullOrWhiteSpace(PuttingDirectory)
            ? ExputtPuttFileWatcher.DefaultPuttDirectory()
            : PuttingDirectory;

    public void Normalize()
    {
        Handedness = string.Equals(Handedness, "LH", StringComparison.OrdinalIgnoreCase) ? "LH" : "RH";
        Mode = Mode?.Trim().ToUpperInvariant() switch
        {
            "PUTTING" => "PUTTING",
            "CHIPPING" => "CHIPPING",
            _ => "NORMAL"
        };
        if (SpeedScale <= 0) SpeedScale = 1m;
        if (PuttSpeedScale <= 0) PuttSpeedScale = 1m;
        if (OpenConnectPort <= 0 || OpenConnectPort > 65535) OpenConnectPort = 921;
        if (string.IsNullOrWhiteSpace(OpenConnectBind)) OpenConnectBind = "127.0.0.1";
        PuttingSource = PuttingSource?.Trim() switch
        {
            "None" or "none" or "NONE" => "None",
            "File" or "file" => "File",
            "OpenConnect" or "openconnect" or "TCP" or "Tcp" => "OpenConnect",
            _ => "Both"
        };
    }

    private static string GetPath()
        => Path.Combine(AppContext.BaseDirectory, "Settings", "Other", "uneekor-rela-connector.json");
}
