using UneekorRelaConnector.Installer;
using Xunit;

namespace UneekorRelaConnector.Tests;

public class SettingsAndInstallerTests
{
    [Fact]
    public void Normalize_clamps_and_maps_aliases()
    {
        var settings = new UneekorConnectorSettings
        {
            Handedness = "lh",
            Mode = "putting",
            SpeedScale = 0,
            PuttSpeedScale = -2,
            OpenConnectPort = 99999,
            OpenConnectBind = "",
            PuttingSource = "TCP"
        };
        settings.Normalize();
        Assert.Equal("LH", settings.Handedness);
        Assert.Equal("PUTTING", settings.Mode);
        Assert.Equal(1m, settings.SpeedScale);
        Assert.Equal(1m, settings.PuttSpeedScale);
        Assert.Equal(921, settings.OpenConnectPort);
        Assert.Equal("127.0.0.1", settings.OpenConnectBind);
        Assert.Equal("OpenConnect", settings.PuttingSource);
        Assert.True(settings.OpenConnectPuttingEnabled);
        Assert.False(settings.FilePuttingEnabled);
    }

    [Fact]
    public void Load_and_save_round_trip()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "settings.json");
        var settings = new UneekorConnectorSettings { InvertHla = true, PuttingSource = "File" };
        settings.Save(path);

        var loaded = UneekorConnectorSettings.Load(path);
        Assert.True(loaded.InvertHla);
        Assert.Equal("File", loaded.PuttingSource);
        Assert.True(loaded.FilePuttingEnabled);
    }

    [Fact]
    public void Install_copies_plugin_and_writes_settings()
    {
        using var dir = new TempDir();
        var host = Path.Combine(dir.Path, "host");
        Directory.CreateDirectory(host);
        File.WriteAllText(Path.Combine(host, "rela.exe"), "fake");
        var dll = Path.Combine(dir.Path, "UneekorRelaConnector.dll");
        File.WriteAllText(dll, "plugin");

        var result = PluginInstaller.Install(dll, host);
        Assert.True(result.Success, result.Message);
        Assert.True(File.Exists(Path.Combine(host, "UneekorRelaConnector.dll")));
        Assert.True(File.Exists(Path.Combine(host, "Settings", "Other", "uneekor-rela-connector.json")));
        Assert.True(Directory.Exists(Path.Combine(host, "Settings", "Other", "exputt-putts")));
        Assert.Contains("Both", File.ReadAllText(result.SettingsPath!));
    }

    [Fact]
    public void Install_refuses_abstractions_and_missing_host()
    {
        using var dir = new TempDir();
        var host = Path.Combine(dir.Path, "empty");
        Directory.CreateDirectory(host);
        var abs = Path.Combine(dir.Path, "rela.OtherDevice.Abstractions.dll");
        File.WriteAllText(abs, "nope");
        var refused = PluginInstaller.Install(abs, host, requireHostExe: false);
        Assert.False(refused.Success);
        Assert.Contains("Abstractions", refused.Message);

        var dll = Path.Combine(dir.Path, "UneekorRelaConnector.dll");
        File.WriteAllText(dll, "plugin");
        var missingHost = PluginInstaller.Install(dll, host, requireHostExe: true);
        Assert.False(missingHost.Success);
        Assert.Contains("rela.exe", missingHost.Message);
    }

    [Fact]
    public void Uninstall_removes_dll_and_keeps_settings()
    {
        using var dir = new TempDir();
        var host = Path.Combine(dir.Path, "host");
        Directory.CreateDirectory(host);
        File.WriteAllText(Path.Combine(host, "rela.exe"), "fake");
        var dll = Path.Combine(dir.Path, "UneekorRelaConnector.dll");
        File.WriteAllText(dll, "plugin");
        PluginInstaller.Install(dll, host);

        var result = PluginInstaller.Uninstall(host);
        Assert.True(result.Success, result.Message);
        Assert.False(File.Exists(Path.Combine(host, "UneekorRelaConnector.dll")));
        Assert.True(File.Exists(Path.Combine(host, "Settings", "Other", "uneekor-rela-connector.json")));
    }

    [Fact]
    public void Find_candidates_and_resolve_payload()
    {
        using var dir = new TempDir();
        var host = Path.Combine(dir.Path, "rela");
        Directory.CreateDirectory(host);
        File.WriteAllText(Path.Combine(host, "PurePlay.exe"), "fake");
        var found = PluginInstaller.FindCandidateDirectories(new[] { dir.Path });
        Assert.Contains(host, found);

        var payloadDir = Path.Combine(dir.Path, "payload");
        Directory.CreateDirectory(payloadDir);
        var payload = Path.Combine(payloadDir, "UneekorRelaConnector.dll");
        File.WriteAllText(payload, "plugin");
        Assert.Equal(payload, PluginInstaller.ResolvePluginPayload(null, dir.Path));
    }

    [Fact]
    public void Cli_install_and_help()
    {
        using var dir = new TempDir();
        var host = Path.Combine(dir.Path, "host");
        Directory.CreateDirectory(host);
        File.WriteAllText(Path.Combine(host, "rela.exe"), "fake");
        var dll = Path.Combine(dir.Path, "UneekorRelaConnector.dll");
        File.WriteAllText(dll, "plugin");

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = InstallerCli.Run(
            new[] { "install", "--target", host, "--dll", dll },
            stdout,
            stderr);
        Assert.Equal(0, code);
        Assert.Contains("Installed", stdout.ToString());

        stdout = new StringWriter();
        Assert.Equal(0, InstallerCli.Run(new[] { "--help" }, stdout, new StringWriter()));
        Assert.Contains("install", stdout.ToString());
    }
}
