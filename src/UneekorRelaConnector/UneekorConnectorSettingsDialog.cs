using System.Windows;
using System.Windows.Controls;

namespace UneekorRelaConnector;

internal static class UneekorConnectorSettingsDialog
{
    public static bool TryCollect(UneekorConnectorSettings settings)
    {
        var window = new Window
        {
            Title = "Uneekor + ExPutt Connector Settings",
            Width = 560,
            Height = 620,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize
        };

        var root = new Grid { Margin = new Thickness(16) };
        for (var i = 0; i < 14; i++)
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var row = 0;
        var shotDataBox = AddLabeledText(root, row++, "ShotData folder", settings.ShotDataDirectory ?? "");
        var speedBox = AddLabeledText(root, row++, "Speed scale", settings.SpeedScale.ToString("0.####"));
        var invertBox = AddCheck(root, row++, "Invert HLA (full swing)", settings.InvertHla);
        var handedBox = AddLabeledCombo(root, row++, "Handedness", new[] { "RH", "LH" }, settings.Handedness);
        var modeBox = AddLabeledCombo(root, row++, "Mode", new[] { "NORMAL", "PUTTING", "CHIPPING" }, settings.Mode);

        AddHeading(root, row++, "Putting (ExPutt / dual monitor)");
        var puttingBox = AddLabeledCombo(
            root, row++, "Putting source",
            new[] { "None", "File", "OpenConnect", "Both" },
            settings.PuttingSource);
        var puttDirBox = AddLabeledText(root, row++, "Putt JSON folder", settings.PuttingDirectory ?? "");
        var bindBox = AddLabeledText(root, row++, "Open Connect bind", settings.OpenConnectBind);
        var portBox = AddLabeledText(root, row++, "Open Connect port", settings.OpenConnectPort.ToString());
        var puttScaleBox = AddLabeledText(root, row++, "Putt speed scale", settings.PuttSpeedScale.ToString("0.####"));
        var invertPuttBox = AddCheck(root, row++, "Invert HLA (putts)", settings.InvertPuttHla);
        var ignoreUneekorBox = AddCheck(root, row++, "Ignore Uneekor while putting", settings.IgnoreUneekorWhilePutting);
        var autoPuttBox = AddCheck(root, row++, "Auto-putt when club is PT", settings.AutoPuttingOnPutterClub);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        var ok = new Button { Content = "Save", Width = 90, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancel = new Button { Content = "Cancel", Width = 90, IsCancel = true };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        Grid.SetRow(buttons, row);
        Grid.SetColumnSpan(buttons, 2);
        root.Children.Add(buttons);

        var accepted = false;
        ok.Click += (_, _) =>
        {
            if (!decimal.TryParse(speedBox.Text.Trim(), out var scale) || scale <= 0)
            {
                MessageBox.Show(window, "Speed scale must be a positive number (use 2.23694 if VIEW reports m/s).", "Uneekor");
                return;
            }

            if (!decimal.TryParse(puttScaleBox.Text.Trim(), out var puttScale) || puttScale <= 0)
            {
                MessageBox.Show(window, "Putt speed scale must be a positive number.", "Uneekor");
                return;
            }

            if (!int.TryParse(portBox.Text.Trim(), out var port) || port <= 0 || port > 65535)
            {
                MessageBox.Show(window, "Open Connect port must be 1–65535 (GSPro default is 921).", "Uneekor");
                return;
            }

            settings.ShotDataDirectory = string.IsNullOrWhiteSpace(shotDataBox.Text)
                ? null
                : shotDataBox.Text.Trim();
            settings.SpeedScale = scale;
            settings.InvertHla = invertBox.IsChecked == true;
            settings.Handedness = handedBox.SelectedItem as string ?? "RH";
            settings.Mode = modeBox.SelectedItem as string ?? "NORMAL";
            settings.PuttingSource = puttingBox.SelectedItem as string ?? "Both";
            settings.PuttingDirectory = string.IsNullOrWhiteSpace(puttDirBox.Text)
                ? null
                : puttDirBox.Text.Trim();
            settings.OpenConnectBind = string.IsNullOrWhiteSpace(bindBox.Text)
                ? "127.0.0.1"
                : bindBox.Text.Trim();
            settings.OpenConnectPort = port;
            settings.PuttSpeedScale = puttScale;
            settings.InvertPuttHla = invertPuttBox.IsChecked == true;
            settings.IgnoreUneekorWhilePutting = ignoreUneekorBox.IsChecked == true;
            settings.AutoPuttingOnPutterClub = autoPuttBox.IsChecked == true;
            accepted = true;
            window.DialogResult = true;
            window.Close();
        };
        cancel.Click += (_, _) =>
        {
            window.DialogResult = false;
            window.Close();
        };

        var scroll = new ScrollViewer
        {
            Content = root,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        window.Content = scroll;
        window.ShowDialog();
        return accepted;
    }

    private static void AddHeading(Grid root, int row, string text)
    {
        var lbl = new TextBlock
        {
            Text = text,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 14, 0, 6)
        };
        Grid.SetRow(lbl, row);
        Grid.SetColumnSpan(lbl, 2);
        root.Children.Add(lbl);
    }

    private static CheckBox AddCheck(Grid root, int row, string label, bool value)
    {
        var box = new CheckBox
        {
            Content = label,
            IsChecked = value,
            Margin = new Thickness(0, 6, 0, 6),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(box, row);
        Grid.SetColumn(box, 1);
        root.Children.Add(box);
        return box;
    }

    private static TextBox AddLabeledText(Grid root, int row, string label, string value)
    {
        var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 8, 6) };
        Grid.SetRow(lbl, row);
        Grid.SetColumn(lbl, 0);
        root.Children.Add(lbl);

        var box = new TextBox { Text = value, Margin = new Thickness(0, 6, 0, 6) };
        Grid.SetRow(box, row);
        Grid.SetColumn(box, 1);
        root.Children.Add(box);
        return box;
    }

    private static ComboBox AddLabeledCombo(Grid root, int row, string label, string[] items, string selected)
    {
        var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 6, 8, 6) };
        Grid.SetRow(lbl, row);
        Grid.SetColumn(lbl, 0);
        root.Children.Add(lbl);

        var box = new ComboBox
        {
            Margin = new Thickness(0, 6, 0, 6),
            ItemsSource = items,
            SelectedItem = items.Contains(selected) ? selected : items[0]
        };
        Grid.SetRow(box, row);
        Grid.SetColumn(box, 1);
        root.Children.Add(box);
        return box;
    }
}
