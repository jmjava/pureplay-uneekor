using System.Windows;
using System.Windows.Controls;

namespace UneekorRelaConnector;

internal static class UneekorConnectorSettingsDialog
{
    public static bool TryCollect(UneekorConnectorSettings settings)
    {
        var window = new Window
        {
            Title = "Uneekor Connector Settings",
            Width = 480,
            Height = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize
        };

        var root = new Grid { Margin = new Thickness(16) };
        for (var i = 0; i < 6; i++)
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var shotDataBox = AddLabeledText(root, 0, "ShotData folder", settings.ShotDataDirectory ?? "");
        var speedBox = AddLabeledText(root, 1, "Speed scale", settings.SpeedScale.ToString("0.####"));
        var invertBox = new CheckBox
        {
            Content = "Invert HLA",
            IsChecked = settings.InvertHla,
            Margin = new Thickness(0, 8, 0, 8)
        };
        Grid.SetRow(invertBox, 2);
        Grid.SetColumn(invertBox, 1);
        root.Children.Add(invertBox);

        var handedBox = AddLabeledCombo(root, 3, "Handedness", new[] { "RH", "LH" }, settings.Handedness);
        var modeBox = AddLabeledCombo(root, 4, "Mode", new[] { "NORMAL", "PUTTING", "CHIPPING" }, settings.Mode);

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
        Grid.SetRow(buttons, 5);
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

            settings.ShotDataDirectory = string.IsNullOrWhiteSpace(shotDataBox.Text)
                ? null
                : shotDataBox.Text.Trim();
            settings.SpeedScale = scale;
            settings.InvertHla = invertBox.IsChecked == true;
            settings.Handedness = handedBox.SelectedItem as string ?? "RH";
            settings.Mode = modeBox.SelectedItem as string ?? "NORMAL";
            accepted = true;
            window.DialogResult = true;
            window.Close();
        };
        cancel.Click += (_, _) =>
        {
            window.DialogResult = false;
            window.Close();
        };

        window.Content = root;
        window.ShowDialog();
        return accepted;
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
