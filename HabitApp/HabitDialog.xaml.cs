using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace HabitApp;

public partial class HabitDialog : Window
{
    private readonly Habit _habit;
    private readonly bool _ready;

    public HabitDialog(Habit habit, bool isNew)
    {
        InitializeComponent();

        _habit = habit;
        Title = isNew ? "New habit" : "Edit habit";

        NameBox.Text = habit.Name;
        QuestionBox.Text = habit.Question;
        DescriptionBox.Text = habit.Description;

        YesNoRadio.IsChecked = !habit.IsNumeric;
        NumericRadio.IsChecked = habit.IsNumeric;
        // The type is fixed once a habit exists, so old entries keep their meaning.
        YesNoRadio.IsEnabled = isNew;
        NumericRadio.IsEnabled = isNew;

        TargetTypeBox.SelectedIndex = habit.AtMost ? 1 : 0;
        TargetBox.Text = habit.IsNumeric ? habit.Target.ToString("0.##") : "";
        UnitBox.Text = habit.Unit;

        if (habit.FreqNum >= habit.FreqDen)
        {
            FrequencyBox.SelectedIndex = 0;
            FrequencyNumberBox.Text = "3";
        }
        else if (habit.FreqDen == 7)
        {
            FrequencyBox.SelectedIndex = 1;
            FrequencyNumberBox.Text = habit.FreqNum.ToString();
        }
        else if (habit.FreqDen == 30)
        {
            FrequencyBox.SelectedIndex = 2;
            FrequencyNumberBox.Text = habit.FreqNum.ToString();
        }
        else
        {
            FrequencyBox.SelectedIndex = 3;
            FrequencyNumberBox.Text = habit.FreqDen.ToString();
        }

        ReminderCheck.IsChecked = habit.Reminder.Length > 0;
        ReminderBox.Text = habit.Reminder.Length > 0 ? habit.Reminder : "20:00";

        ColorList.ItemsSource = Habit.Palette;
        ColorList.SelectedItem = habit.Color;
        if (ColorList.SelectedIndex < 0)
        {
            ColorList.SelectedIndex = 0;
        }

        _ready = true;
        UpdateVisibility();

        Loaded += (_, _) => NameBox.Focus();
    }

    private void Option_Changed(object sender, RoutedEventArgs e)
    {
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        if (!_ready)
        {
            return;
        }

        TargetPanel.Visibility = NumericRadio.IsChecked == true
            ? Visibility.Visible
            : Visibility.Collapsed;

        ReminderBox.IsEnabled = ReminderCheck.IsChecked == true;

        int index = FrequencyBox.SelectedIndex;
        FrequencyNumberBox.Visibility = index > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (index == 1)
        {
            FrequencyHint.Text = "times per week";
        }
        else if (index == 2)
        {
            FrequencyHint.Text = "times per month";
        }
        else if (index == 3)
        {
            FrequencyHint.Text = "days between each time";
        }
        else
        {
            FrequencyHint.Text = "";
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            Warn("Please give the habit a name.");
            return;
        }

        bool isNumeric = NumericRadio.IsChecked == true;
        bool atMost = isNumeric && TargetTypeBox.SelectedIndex == 1;
        double target = 0;
        if (isNumeric)
        {
            bool ok = double.TryParse(TargetBox.Text.Trim(), NumberStyles.Float,
                CultureInfo.CurrentCulture, out target);

            if (atMost && (!ok || target < 0))
            {
                Warn("Please enter a daily limit of zero or more.");
                return;
            }
            if (!atMost && (!ok || target <= 0))
            {
                Warn("Please enter a daily target greater than zero.");
                return;
            }
        }

        int num = 1;
        int den = 1;
        int index = FrequencyBox.SelectedIndex;
        if (index > 0)
        {
            bool ok = int.TryParse(FrequencyNumberBox.Text.Trim(), out int count);
            if (!ok || count < 1)
            {
                Warn("Please enter a whole number for how often.");
                return;
            }

            if (index == 1)
            {
                num = Math.Min(count, 7);
                den = 7;
            }
            else if (index == 2)
            {
                num = Math.Min(count, 30);
                den = 30;
            }
            else
            {
                num = 1;
                den = Math.Min(count, 365);
            }
        }

        string reminder = "";
        if (ReminderCheck.IsChecked == true)
        {
            bool ok = DateTime.TryParse(ReminderBox.Text.Trim(), CultureInfo.CurrentCulture,
                DateTimeStyles.None, out DateTime time);
            if (!ok)
            {
                Warn("Please enter the reminder time like 07:30 or 20:00.");
                return;
            }
            reminder = time.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        _habit.Name = name;
        _habit.Question = QuestionBox.Text.Trim();
        _habit.Description = DescriptionBox.Text.Trim();
        _habit.IsNumeric = isNumeric;
        _habit.AtMost = atMost;
        _habit.Target = target;
        _habit.Unit = isNumeric ? UnitBox.Text.Trim() : "";
        _habit.FreqNum = num;
        _habit.FreqDen = den;
        _habit.Reminder = reminder;
        _habit.Color = ColorList.SelectedItem as string ?? Habit.Palette[0];

        DialogResult = true;
    }

    private void Warn(string message)
    {
        MessageBox.Show(this, message, Title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}