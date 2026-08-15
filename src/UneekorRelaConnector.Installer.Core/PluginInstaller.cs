using System.Text.Json;

namespace UneekorRelaConnector.Installer;

public sealed class PluginInstallResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = "";
    public string? InstalledDllPath { get; init; }
    public string? SettingsPath { get; init; }

    public static PluginInstallResult Ok(string message, string? dll = null, string? settings = null)
        => new() { Success = true, Message = message, InstalledDllPath = dll, SettingsPath = settings };

    public static PluginInstallResult Fail(string message)
        => new() { Success = false, Message = message };
}

/// <summary>
/// Copies only <c>UneekorRelaConnector.dll</c> next to <c>rela.exe</c>.
/// Never deploys <c>rela.OtherDevice.Abstractions</c> — the host already embeds that contract.
/// </summary>
public static class PluginInstaller
{
    public const string PluginFileName = "UneekorRelaConnector.dll";
    public const string HostExeName = "rela.exe";
    public const string AbstractionsFileName = "rela.OtherDevice.Abstractions.dll";

    public static readonly string[] HostExeNames = { "rela.exe", "PurePlay.exe" };

    public static PluginInstallResult Install(
        string pluginDllPath,
        string targetDirectory,
        bool requireHostExe = true,
        bool writeDefaultSettings = true)
    {
        if (string.IsNullOrWhiteSpace(pluginDllPath) || !File.Exists(pluginDllPath))
            return PluginInstallResult.Fail("Plugin DLL not found: " + pluginDllPath);

        var sourceName = Path.GetFileName(pluginDllPath);
        if (sourceName.Equals(AbstractionsFileName, StringComparison.OrdinalIgnoreCase)
            || sourceName.Contains("OtherDevice.Abstractions", StringComparison.OrdinalIgnoreCase))
        {
            return PluginInstallResult.Fail(
                "Refusing to install " + sourceName + ". The host already ships the Abstractions contract.");
        }

        if (string.IsNullOrWhiteSpace(targetDirectory))
            return PluginInstallResult.Fail("Target directory is required.");

        targetDirectory = Path.GetFullPath(targetDirectory);
        if (File.Exists(targetDirectory))
            targetDirectory = Path.GetDirectoryName(targetDirectory) ?? targetDirectory;

        Directory.CreateDirectory(targetDirectory);

        if (requireHostExe && !LooksLikeHostDirectory(targetDirectory))
        {
            return PluginInstallResult.Fail(
                "No " + string.Join(" / ", HostExeNames) + " in " + targetDirectory
                + ". Browse to the folder that contains rela.exe.");
        }

        var dest = Path.Combine(targetDirectory, PluginFileName);
        try
        {
            File.Copy(pluginDllPath, dest, overwrite: true);
        }
        catch (Exception ex)
        {
            return PluginInstallResult.Fail("Copy failed: " + ex.Message);
        }

        var settingsDir = Path.Combine(targetDirectory, "Settings", "Other");
        var puttDir = Path.Combine(settingsDir, "exputt-putts");
        Directory.CreateDirectory(puttDir);

        string? settingsPath = Path.Combine(settingsDir, "uneekor-rela-connector.json");
        if (writeDefaultSettings && !File.Exists(settingsPath))
        {
            File.WriteAllText(settingsPath, DefaultSettingsJson());
        }
        else if (!writeDefaultSettings || !File.Exists(settingsPath))
        {
            settingsPath = File.Exists(settingsPath) ? settingsPath : null;
        }

        return PluginInstallResult.Ok(
            "Installed " + PluginFileName + " to " + targetDirectory
            + ". In rēlā / PurePlay: Device Type → Other → Search.",
            dest,
            settingsPath);
    }

    public static PluginInstallResult Uninstall(string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
            return PluginInstallResult.Fail("Target directory is required.");

        targetDirectory = Path.GetFullPath(targetDirectory);
        if (File.Exists(targetDirectory))
            targetDirectory = Path.GetDirectoryName(targetDirectory) ?? targetDirectory;

        var dest = Path.Combine(targetDirectory, PluginFileName);
        if (!File.Exists(dest))
            return PluginInstallResult.Ok("Nothing to remove — " + PluginFileName + " is not in " + targetDirectory);

        try
        {
            File.Delete(dest);
        }
        catch (Exception ex)
        {
            return PluginInstallResult.Fail("Could not delete plugin: " + ex.Message);
        }

        return PluginInstallResult.Ok(
            "Removed " + PluginFileName + ". Settings were left in Settings/Other/.");
    }

    public static bool LooksLikeHostDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return false;
        return HostExeNames.Any(name => File.Exists(Path.Combine(directory, name)));
    }

    public static IReadOnlyList<string> FindCandidateDirectories(IEnumerable<string>? searchRoots = null)
    {
        var roots = (searchRoots ?? DefaultSearchRoots())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var found = new List<string>();
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
                continue;
            if (LooksLikeHostDirectory(root))
                found.Add(root);

            try
            {
                foreach (var child in Directory.EnumerateDirectories(root))
                {
                    if (LooksLikeHostDirectory(child))
                        found.Add(Path.GetFullPath(child));
                }
            }
            catch
            {
                // skip unreadable roots
            }
        }

        return found.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static string? ResolvePluginPayload(string? explicitPath, string installerDirectory)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            return Path.GetFullPath(explicitPath);

        var nextToExe = Path.Combine(installerDirectory, PluginFileName);
        if (File.Exists(nextToExe))
            return nextToExe;

        var payload = Path.Combine(installerDirectory, "payload", PluginFileName);
        return File.Exists(payload) ? payload : null;
    }

    public static string DefaultSettingsJson()
        => JsonSerializer.Serialize(new
        {
            Handedness = "RH",
            Mode = "NORMAL",
            ShotDataDirectory = (string?)null,
            SpeedScale = 1,
            InvertHla = false,
            PuttingSource = "Both",
            PuttingDirectory = (string?)null,
            OpenConnectBind = "127.0.0.1",
            OpenConnectPort = 921,
            IgnoreUneekorWhilePutting = true,
            AutoPuttingOnPutterClub = true,
            InvertPuttHla = false,
            PuttSpeedScale = 1
        }, new JsonSerializerOptions { WriteIndented = true });

    public static IEnumerable<string> DefaultSearchRoots()
    {
        yield return Environment.CurrentDirectory;
        foreach (var special in new[]
                 {
                     Environment.SpecialFolder.ProgramFiles,
                     Environment.SpecialFolder.ProgramFilesX86,
                     Environment.SpecialFolder.LocalApplicationData,
                     Environment.SpecialFolder.Desktop
                 })
        {
            var baseDir = Environment.GetFolderPath(special);
            if (string.IsNullOrWhiteSpace(baseDir))
                continue;
            yield return baseDir;
            yield return Path.Combine(baseDir, "rela");
            yield return Path.Combine(baseDir, "PurePlay");
            yield return Path.Combine(baseDir, "Pure Play");
        }
    }
}
