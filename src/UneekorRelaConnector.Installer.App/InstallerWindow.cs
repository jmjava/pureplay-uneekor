using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace UneekorRelaConnector.Installer;

public sealed class InstallerWindow : Window
{
    private readonly TextBox _targetBox;
    private readonly ComboBox _candidates;
    private readonly TextBox _log;
    private readonly CheckBox _writeSettings;

    public InstallerWindow()
    {
        Title = "Uneekor + ExPutt Connector Setup";
        Width = 640;
        Height = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var root = new Grid { Margin = new Thickness(16) };
        for (var i = 0; i < 8; i++)
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        root.Children.Add(At(0, new TextBlock
        {
            Text = "Installs UneekorRelaConnector.dll next to rela.exe. Do not copy the Abstractions DLL.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        }));

        _candidates = new ComboBox { Margin = new Thickness(0, 0, 0, 8) };
        _candidates.SelectionChanged += (_, _) =>
        {
            if (_targetBox is not null && _candidates.SelectedItem is string dir)
                _targetBox.Text = dir;
        };
        root.Children.Add(At(1, Labeled("Detected hosts", _candidates)));

        _targetBox = new TextBox { Margin = new Thickness(0, 4, 0, 0) };
        var browse = new Button { Content = "Browse…", Width = 90, Margin = new Thickness(8, 4, 0, 0) };
        browse.Click += (_, _) => Browse();
        var targetRow = new DockPanel();
        DockPanel.SetDock(browse, Dock.Right);
        targetRow.Children.Add(browse);
        targetRow.Children.Add(_targetBox);
        root.Children.Add(At(2, Labeled("rēlā / PurePlay folder", targetRow)));

        _writeSettings = new CheckBox
        {
            Content = "Write default settings if missing",
            IsChecked = true,
            Margin = new Thickness(0, 12, 0, 8)
        };
        root.Children.Add(At(3, _writeSettings));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
        buttons.Children.Add(ActionButton("Install", Install));
        buttons.Children.Add(ActionButton("Uninstall", Uninstall));
        buttons.Children.Add(ActionButton("Refresh", RefreshCandidates));
        root.Children.Add(At(4, buttons));

        root.Children.Add(At(5, new TextBlock
        {
            Text = "After install: Device Type → Other → Search. ExPutt Camera goes on monitor 2; point springbok at 127.0.0.1:921.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8)
        }));

        _log = new TextBox
        {
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            AcceptsReturn = true
        };
        root.Children.Add(At(6, new TextBlock { Text = "Log", Margin = new Thickness(0, 4, 0, 4) }));
        Grid.SetRow(_log, 8);
        root.Children.Add(_log);

        Content = root;
        Loaded += (_, _) => RefreshCandidates();
    }

    private void RefreshCandidates()
    {
        var found = PluginInstaller.FindCandidateDirectories();
        _candidates.ItemsSource = found;
        if (found.Count > 0 && string.IsNullOrWhiteSpace(_targetBox.Text))
        {
            _candidates.SelectedIndex = 0;
            _targetBox.Text = found[0];
        }

        Log(found.Count == 0
            ? "No rela.exe / PurePlay.exe found. Browse to the host folder."
            : "Found " + found.Count + " host folder(s).");
    }

    private void Browse()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select rela.exe or PurePlay.exe",
            Filter = "Host executable|rela.exe;PurePlay.exe|All files|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            _targetBox.Text = Path.GetDirectoryName(dialog.FileName) ?? dialog.FileName;
        }
    }

    private void Install()
    {
        var dll = PluginInstaller.ResolvePluginPayload(null, AppContext.BaseDirectory);
        if (dll is null)
        {
            Log("ERROR: UneekorRelaConnector.dll not found next to this setup app or in payload/.");
            return;
        }

        var target = _targetBox.Text.Trim();
        var result = PluginInstaller.Install(
            dll,
            target,
            requireHostExe: true,
            writeDefaultSettings: _writeSettings.IsChecked == true);
        Log((result.Success ? "OK: " : "ERROR: ") + result.Message);
        if (result.InstalledDllPath is not null)
            Log("DLL: " + result.InstalledDllPath);
    }

    private void Uninstall()
    {
        var result = PluginInstaller.Uninstall(_targetBox.Text.Trim());
        Log((result.Success ? "OK: " : "ERROR: ") + result.Message);
    }

    private void Log(string line)
    {
        _log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + line + Environment.NewLine);
        _log.ScrollToEnd();
    }

    private static Button ActionButton(string text, Action click)
    {
        var button = new Button { Content = text, Width = 100, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(8, 4, 8, 4) };
        button.Click += (_, _) => click();
        return button;
    }

    private static FrameworkElement Labeled(string label, UIElement child)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 4) });
        panel.Children.Add(child);
        return panel;
    }

    private static UIElement At(int row, UIElement child)
    {
        Grid.SetRow(child, row);
        return child;
    }
}
