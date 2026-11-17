using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HabitApp;

public class NoteDialog : Window
{
    private readonly TextBox _box = new();

    public string NoteText { get; private set; } = "";

    public NoteDialog(string title, string current)
    {
        Title = title;
        Width = 420;
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
            Text = "Note for this day",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondary");
        panel.Children.Add(label);

        _box.Text = current;
        _box.AcceptsReturn = true;
        _box.TextWrapping = TextWrapping.Wrap;
        _box.Height = 110;
        _box.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        panel.Children.Add(_box);

        var hint = new TextBlock
        {
            Text = "Leave empty to remove the note.",
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
            _box.CaretIndex = _box.Text.Length;
        };
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        NoteText = _box.Text.Trim();
        DialogResult = true;
    }
}