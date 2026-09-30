namespace UneekorRelaConnector.Installer;

internal static class Program
{
    public static int Main(string[] args)
        => InstallerCli.Run(args, Console.Out, Console.Error);
}
