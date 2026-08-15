namespace UneekorRelaConnector.Installer;

public static class InstallerCli
{
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp(stdout);
            return args.Length == 0 ? 1 : 0;
        }

        var command = args[0].ToLowerInvariant();
        var map = ParseFlags(args.Skip(1).ToArray());
        try
        {
            return command switch
            {
                "install" => Install(map, stdout, stderr),
                "uninstall" => Uninstall(map, stdout, stderr),
                "find" => Find(map, stdout),
                _ => Fail(stderr, "Unknown command: " + args[0])
            };
        }
        catch (Exception ex)
        {
            return Fail(stderr, ex.Message);
        }
    }

    private static int Install(Dictionary<string, string> map, TextWriter stdout, TextWriter stderr)
    {
        if (!map.TryGetValue("target", out var target) || string.IsNullOrWhiteSpace(target))
            return Fail(stderr, "--target is required (folder that contains rela.exe, or the exe itself).");

        map.TryGetValue("dll", out var dll);
        var resolved = PluginInstaller.ResolvePluginPayload(dll, AppContext.BaseDirectory);
        if (resolved is null)
            return Fail(stderr, "Plugin DLL not found. Pass --dll path/to/UneekorRelaConnector.dll");

        var requireHost = !map.ContainsKey("allow-missing-host");
        var writeSettings = !map.ContainsKey("no-settings");
        var result = PluginInstaller.Install(resolved, target, requireHost, writeSettings);
        stdout.WriteLine(result.Message);
        return result.Success ? 0 : 2;
    }

    private static int Uninstall(Dictionary<string, string> map, TextWriter stdout, TextWriter stderr)
    {
        if (!map.TryGetValue("target", out var target) || string.IsNullOrWhiteSpace(target))
            return Fail(stderr, "--target is required.");
        var result = PluginInstaller.Uninstall(target);
        stdout.WriteLine(result.Message);
        return result.Success ? 0 : 2;
    }

    private static int Find(Dictionary<string, string> map, TextWriter stdout)
    {
        IEnumerable<string>? roots = null;
        if (map.TryGetValue("root", out var root) && !string.IsNullOrWhiteSpace(root))
            roots = new[] { root };
        var found = PluginInstaller.FindCandidateDirectories(roots);
        if (found.Count == 0)
        {
            stdout.WriteLine("No rela.exe / PurePlay.exe folders found.");
            return 1;
        }

        foreach (var dir in found)
            stdout.WriteLine(dir);
        return 0;
    }

    internal static Dictionary<string, string> ParseFlags(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (!a.StartsWith("--", StringComparison.Ordinal))
                continue;
            var key = a[2..];
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                map[key] = args[++i];
            else
                map[key] = "true";
        }

        return map;
    }

    private static int Fail(TextWriter stderr, string message)
    {
        stderr.WriteLine(message);
        return 2;
    }

    private static void PrintHelp(TextWriter stdout)
    {
        stdout.WriteLine("Uneekor + ExPutt connector installer");
        stdout.WriteLine();
        stdout.WriteLine("Commands:");
        stdout.WriteLine("  install   --target <rela-folder-or-exe> [--dll <plugin.dll>] [--allow-missing-host] [--no-settings]");
        stdout.WriteLine("  uninstall --target <rela-folder-or-exe>");
        stdout.WriteLine("  find      [--root <search-folder>]");
        stdout.WriteLine();
        stdout.WriteLine("Copies only UneekorRelaConnector.dll next to rela.exe.");
        stdout.WriteLine("Do not deploy rela.OtherDevice.Abstractions.dll.");
    }
}
