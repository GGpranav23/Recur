using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;

namespace HabitApp;

public partial class SettingsWindow : Window
{
    private static readonly string[] WeekStartValues = { "windows", "sunday", "monday", "saturday" };

    private readonly bool _ready;

    // True when a backup was restored, so the main window must reload.
    public bool DataRestored { get; private set; }

    public SettingsWindow()
    {
        InitializeComponent();

        LightRadio.IsChecked = Preferences.ThemeMode == "light";
        DarkRadio.IsChecked = Preferences.ThemeMode == "dark";
        SystemRadio.IsChecked = Preferences.ThemeMode == "system";

        string windowsDay =
            CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(Preferences.WindowsFirstDay);
        WindowsWeekItem.Content = $"Windows setting ({windowsDay})";

        int index = Array.IndexOf(WeekStartValues, Preferences.WeekStart);
        WeekStartBox.SelectedIndex = index < 0 ? 0 : index;

        _ready = true;
    }

    private void Theme_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        string mode = DarkRadio.IsChecked == true ? "dark"
            : LightRadio.IsChecked == true ? "light"
            : "system";
        Preferences.SetTheme(mode);
    }

    private void WeekStart_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready || WeekStartBox.SelectedIndex < 0)
        {
            return;
        }
        Preferences.SetWeekStart(WeekStartValues[WeekStartBox.SelectedIndex]);
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Back up your habits",
            Filter = "Habit backup (*.db)|*.db",
            FileName = $"Recur-backup-{DateTime.Today:yyyy-MM-dd}.db"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            Database.BackupTo(dialog.FileName);
            Info("Your backup was saved.");
        }
        catch (Exception error)
        {
            Warn("The backup could not be saved.\n\n" + error.Message);
        }
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Restore from a backup",
            Filter = "Habit backup (*.db)|*.db|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var answer = MessageBox.Show(this,
            "Restoring replaces all your current habits and history with the ones in the backup.\n\n" +
            "A copy of your current data is saved first, just in case. Continue?",
            "Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            Database.RestoreFrom(dialog.FileName);
            DataRestored = true;
            Info("Your habits were restored.");
        }
        catch (Exception error)
        {
            Warn("That file could not be restored. Make sure it is a backup made by this app.\n\n" +
                 error.Message);
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export habit history",
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"habits-{DateTime.Today:yyyy-MM-dd}.csv"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var lines = new List<string> { "Habit,Date,Value,Unit,Note" };
        foreach (Habit habit in Database.GetHabits())
        {
            Dictionary<DateTime, double> history =
                Database.GetEntries(habit.Id, DateTime.MinValue, DateTime.Today);
            Dictionary<DateTime, string> notes =
                Database.GetNotes(habit.Id, DateTime.MinValue, DateTime.Today);

            foreach (DateTime day in history.Keys.Union(notes.Keys).OrderBy(d => d))
            {
                string value = "";
                if (history.TryGetValue(day, out double amount))
                {
                    value = amount == Habit.SkipValue
                        ? "skipped"
                        : amount.ToString("0.##", CultureInfo.InvariantCulture);
                }
                notes.TryGetValue(day, out string? note);

                lines.Add(string.Join(",",
                    Quote(habit.Name),
                    day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    value,
                    Quote(habit.Unit),
                    Quote(note ?? "")));
            }
        }

        try
        {
            System.IO.File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true));
            Info("Your history was exported.");
        }
        catch (Exception error)
        {
            Warn("The file could not be saved.\n\n" + error.Message);
        }
    }

    private static string Quote(string text)
    {
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Info(string message)
    {
        MessageBox.Show(this, message, "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Warn(string message)
    {
        MessageBox.Show(this, message, "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}