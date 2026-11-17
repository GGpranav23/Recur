using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HabitApp;

public class NumberDialog : Window
{
    private readonly TextBox _box = new();

    // The amount entered, or null when the box was left empty to clear the day.
    public double? Value { get; private set; }

    public NumberDialog(string title, string prompt, double? current)
    {
        Title = title;
        Width = 340;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        FontFamily = new FontFamily("Segoe UI");
        FontSize = 14;
        SetResourceReference(BackgroundProperty, "AppBackground");

        var panel = new StackPanel { Margin = new Thickness(20) };

        var label = new TextBlock
        {
            Text = prompt,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 8)
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextPrimary");
        panel.Children.Add(label);

        _box.Text = current >= 0 ? current!.Value.ToString("0.##") : "";
        panel.Children.Add(_box);

        var hint = new TextBlock
        {
            Text = "Leave empty to clear this day.",
            FontSize = 12,
            Margin = new Thickness(0, 6, 0, 0)
        };
        hint.SetResourceReference(TextBlock.ForegroundProperty, "TextMuted");
        panel.Children.Add(hint);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0)
        };

        var cancel = new Button
        {
            Content = "Cancel",
            IsCancel = true,
            Style = (Style)FindResource("SecondaryButton")
        };

        var save = new Button
        {
            Content = "Save",
            IsDefault = true,
            Margin = new Thickness(8, 0, 0, 0),
            Style = (Style)FindResource("PrimaryButton")
        };
        save.Click += Save_Click;

        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        panel.Children.Add(buttons);

        Content = panel;

        Loaded += (_, _) =>
        {
            _box.Focus();
            _box.SelectAll();
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string text = _box.Text.Trim();

        if (text.Length == 0)
        {
            Value = null;
            DialogResult = true;
            return;
        }

        bool ok = double.TryParse(
            text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value);

        if (!ok || value < 0)
        {
            MessageBox.Show(this,
                "Please enter a number, or leave the box empty to clear this day.",
                Title);
            return;
        }

        Value = value;
        DialogResult = true;
    }
}