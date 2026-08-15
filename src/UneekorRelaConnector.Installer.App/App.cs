using System.Windows;

namespace UneekorRelaConnector.Installer;

public sealed class App : Application
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0)
            return InstallerCli.Run(args, Console.Out, Console.Error);

        var app = new App();
        app.ShutdownMode = ShutdownMode.OnMainWindowClose;
        app.Run(new InstallerWindow());
        return 0;
    }
}
