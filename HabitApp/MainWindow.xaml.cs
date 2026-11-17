using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace HabitApp;

public partial class MainWindow : Window
{
    // Width used by everything in a row except the day squares.
    private const double FixedRowWidth = 550;
    private const double DayWidth = 36;

    private readonly List<Habit> _allHabits = new();              // every habit, in "my order"
    private readonly ObservableCollection<Habit> _habits = new(); // what is on screen
    private readonly TrayIcon _tray = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly HashSet<string> _reminded = new();
    private readonly bool _ready;

    private int _dayCount = 14;
    private DateTime _shownDay = DateTime.Today;
    private bool _exiting;
    private bool _trayHintShown;

    public MainWindow()
    {
        InitializeComponent();
        Database.Initialize();

        // Restore the sorting and filter choices from last time.
        SortBox.SelectedIndex = ReadNumber("sort", 0, 0, 4);
        HideDoneBox.IsChecked = Database.GetSetting("hideDone", "0") == "1";
        ShowArchivedBox.IsChecked = Database.GetSetting("showArchived", "0") == "1";

        HabitList.ItemsSource = _habits;
        _ready = true;
        ReloadAll();

        _tray.OpenRequested += ShowFromTray;
        _tray.ExitRequested += ExitApp;

        _timer.Tick += Timer_Tick;
        _timer.Start();
    }

    private static int ReadNumber(string name, int fallback, int min, int max)
    {
        bool ok = int.TryParse(Database.GetSetting(name, fallback.ToString()), out int value);
        return ok ? Math.Clamp(value, min, max) : fallback;
    }

    // ---------- Loading ----------

    private void ReloadAll()
    {
        TodayText.Text = DateTime.Today.ToString("dddd, d MMMM");
        DayHeaderList.ItemsSource = BuildHeaders();

        _allHabits.Clear();
        foreach (var habit in Database.GetHabits())
        {
            LoadDays(habit);
            RefreshStats(habit);
            _allHabits.Add(habit);
        }

        ApplyView();
    }

    private DateTime FirstDay()
    {
        return DateTime.Today.AddDays(-(_dayCount - 1));
    }

    private List<DayHeader> BuildHeaders()
    {
        var headers = new List<DayHeader>();
        DateTime first = FirstDay();
        for (int i = 0; i < _dayCount; i++)
        {
            DateTime date = first.AddDays(i);
            headers.Add(new DayHeader
            {
                DayName = date.ToString("ddd"),
                DayNumber = date.Day.ToString(),
                IsToday = date == DateTime.Today
            });
        }
        return headers;
    }

    private void LoadDays(Habit habit)
    {
        DateTime first = FirstDay();
        Dictionary<DateTime, double> entries =
            Database.GetEntries(habit.Id, first, DateTime.Today);
        Dictionary<DateTime, string> notes =
            Database.GetNotes(habit.Id, first, DateTime.Today);

        habit.Days.Clear();
        for (int i = 0; i < _dayCount; i++)
        {
            DateTime date = first.AddDays(i);
            double? value = entries.TryGetValue(date, out double found) ? found : (double?)null;
            string note = notes.TryGetValue(date, out string? text) ? text : "";
            habit.Days.Add(new DayCell(habit, date, value, note));
        }
    }

    private static void RefreshStats(Habit habit)
    {
        Dictionary<DateTime, double> history =
            Database.GetEntries(habit.Id, DateTime.MinValue, DateTime.Today);
        habit.SetStats(Stats.Compute(habit, history, DateTime.Today));
    }

    // ---------- Sorting and filtering ----------

    private void View_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        Database.SetSetting("sort", SortBox.SelectedIndex.ToString());
        Database.SetSetting("hideDone", HideDoneBox.IsChecked == true ? "1" : "0");
        Database.SetSetting("showArchived", ShowArchivedBox.IsChecked == true ? "1" : "0");
        ApplyView();
    }

    private void ApplyView()
    {
        IEnumerable<Habit> view = _allHabits;

        if (ShowArchivedBox.IsChecked != true)
        {
            view = view.Where(h => !h.IsArchived);
        }
        if (HideDoneBox.IsChecked == true)
        {
            view = view.Where(h => !h.IsDoneToday);
        }

        switch (SortBox.SelectedIndex)
        {
            case 1:
                view = view.OrderBy(h => h.Name, StringComparer.CurrentCultureIgnoreCase);
                break;
            case 2:
                view = view.OrderBy(h => ColorRank(h.Color))
                           .ThenBy(h => h.Name, StringComparer.CurrentCultureIgnoreCase);
                break;
            case 3:
                view = view.OrderByDescending(h => h.Score);
                break;
            case 4:
                view = view.OrderBy(h => h.IsDoneToday);
                break;
        }

        // Archived habits always come last.
        List<Habit> ordered = view.OrderBy(h => h.IsArchived).ToList();

        _habits.Clear();
        foreach (Habit habit in ordered)
        {
            _habits.Add(habit);
        }

        UpdateEmptyState();
    }

    private static int ColorRank(string color)
    {
        int index = Array.IndexOf(Habit.Palette, color);
        return index < 0 ? int.MaxValue : index;
    }

    private void UpdateEmptyState()
    {
        bool none = _allHabits.Count == 0;
        bool nothingShown = _habits.Count == 0;

        EmptyState.Visibility = nothingShown ? Visibility.Visible : Visibility.Collapsed;
        HabitScroll.Visibility = nothingShown ? Visibility.Collapsed : Visibility.Visible;
        Toolbar.Visibility = none ? Visibility.Collapsed : Visibility.Visible;
        EmptyButton.Visibility = none ? Visibility.Visible : Visibility.Collapsed;

        EmptyTitle.Text = none ? "No habits yet" : "Nothing to show";
        EmptyText.Text = none
            ? "Add your first habit, then tick it off each day to build a streak."
            : "Every habit is hidden by the options above.";
    }

    // Show more days when the window is wider, fewer when it is narrower.
    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        int count = (int)((ActualWidth - FixedRowWidth) / DayWidth);
        count = Math.Clamp(count, 7, 31);

        if (count == _dayCount)
        {
            return;
        }

        _dayCount = count;
        DayHeaderList.ItemsSource = BuildHeaders();
        foreach (var habit in _allHabits)
        {
            LoadDays(habit);
        }
    }

    // ---------- Tray, reminders and the midnight refresh ----------

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (DateTime.Today != _shownDay)
        {
            _shownDay = DateTime.Today;
            _reminded.Clear();
            ReloadAll();
        }

        CheckReminders();
    }

    private static string ReminderKey(Habit habit)
    {
        return $"{habit.Id}|{DateTime.Today:yyyyMMdd}";
    }

    private void CheckReminders()
    {
        DateTime now = DateTime.Now;
        var due = new List<string>();

        foreach (var habit in _allHabits)
        {
            if (habit.IsArchived || habit.Reminder.Length == 0)
            {
                continue;
            }

            bool ok = TimeSpan.TryParse(
                habit.Reminder, CultureInfo.InvariantCulture, out TimeSpan time);
            if (!ok || now.TimeOfDay < time)
            {
                continue;
            }

            // Only one reminder per habit per day.
            if (!_reminded.Add(ReminderKey(habit)))
            {
                continue;
            }

            if (!habit.IsDoneToday)
            {
                due.Add(habit.Name);
            }
        }

        if (due.Count > 0)
        {
            _tray.Notify("Habit reminder", "Time for: " + string.Join(", ", due));
        }
    }

    private void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        Activate();
    }

    private void ExitApp()
    {
        _exiting = true;
        _timer.Stop();
        _tray.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exiting)
        {
            // Closing the window only hides it, so reminders keep working.
            e.Cancel = true;
            Hide();

            if (!_trayHintShown)
            {
                _trayHintShown = true;
                _tray.Notify("Recur is still running",
                    "It stays near the clock so reminders can reach you. " +
                    "Right-click the icon and choose Exit to close it.");
            }
        }

        base.OnClosing(e);
    }

    // ---------- Habit buttons and menus ----------

    private void NewHabit_Click(object sender, RoutedEventArgs e)
    {
        var habit = new Habit
        {
            Color = Habit.Palette[_allHabits.Count % Habit.Palette.Length]
        };

        var dialog = new HabitDialog(habit, true) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        Database.SaveHabit(habit);
        LoadDays(habit);
        RefreshStats(habit);
        _allHabits.Add(habit);
        ApplyView();
    }

    private void OpenDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Habit habit)
        {
            var window = new DetailWindow(habit) { Owner = this };
            window.ShowDialog();

            // Days may have been edited on the detail page, so refresh.
            LoadDays(habit);
            RefreshStats(habit);
            ApplyView();
        }
    }

    private void EditHabit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Habit habit)
        {
            var dialog = new HabitDialog(habit, false) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            Database.SaveHabit(habit);
            LoadDays(habit);
            RefreshStats(habit);
            habit.Notify();

            // A changed reminder time is allowed to fire again today.
            _reminded.Remove(ReminderKey(habit));
            ApplyView();
        }
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        Move(sender, -1);
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        Move(sender, 1);
    }

    private void Move(object sender, int direction)
    {
        if (sender is not FrameworkElement element || element.DataContext is not Habit habit)
        {
            return;
        }

        // Moving only makes sense in "My order", so switch to it first.
        if (SortBox.SelectedIndex != 0)
        {
            SortBox.SelectedIndex = 0;
        }

        int shown = _habits.IndexOf(habit);
        int target = shown + direction;
        if (shown < 0 || target < 0 || target >= _habits.Count)
        {
            return;
        }

        Habit neighbor = _habits[target];
        if (neighbor.IsArchived != habit.IsArchived)
        {
            return; // archived habits stay at the bottom
        }

        int a = _allHabits.IndexOf(habit);
        int b = _allHabits.IndexOf(neighbor);
        _allHabits[a] = neighbor;
        _allHabits[b] = habit;

        Database.SetPositions(_allHabits.Select(h => h.Id).ToList());
        ApplyView();
    }

    private void Archive_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Habit habit)
        {
            habit.IsArchived = !habit.IsArchived;
            Database.SetArchived(habit.Id, habit.IsArchived);
            habit.Notify();
            ApplyView();
        }
    }

    private void DeleteHabit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is Habit habit)
        {
            var answer = MessageBox.Show(
                $"Delete \"{habit.Name}\" and all of its history?\n\n" +
                "Tip: Archive hides a habit but keeps its history.",
                "Delete habit",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer != MessageBoxResult.Yes)
            {
                return;
            }

            Database.DeleteHabit(habit.Id);
            _allHabits.Remove(habit);
            ApplyView();
        }
    }

    // ---------- Day squares ----------

    private void DayCell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is DayCell cell)
        {
            if (cell.Habit.IsNumeric)
            {
                string unit = cell.Habit.Unit.Length > 0 ? $" ({cell.Habit.Unit})" : "";
                string prompt = $"Amount for {cell.Date:ddd, d MMM}{unit}";
                var dialog = new NumberDialog(cell.Habit.Name, prompt, cell.Value)
                {
                    Owner = this
                };
                if (dialog.ShowDialog() != true)
                {
                    return;
                }
                cell.Value = dialog.Value;
            }
            else
            {
                cell.Value = cell.IsDone ? (double?)null : 1;
            }

            SaveDay(cell);
        }
    }

    private void SkipDay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is DayCell cell)
        {
            cell.Value = cell.IsSkipped ? (double?)null : Habit.SkipValue;
            SaveDay(cell);
        }
    }

    private void ClearDay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is DayCell cell)
        {
            cell.Value = null;
            SaveDay(cell);
        }
    }

    private void NoteDay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is DayCell cell)
        {
            var dialog = new NoteDialog($"{cell.Habit.Name}, {cell.Date:ddd, d MMM}", cell.Note)
            {
                Owner = this
            };
            if (dialog.ShowDialog() != true)
            {
                return;
            }

            cell.Note = dialog.NoteText;
            Database.SetNote(cell.Habit.Id, cell.Date, cell.Note);
        }
    }

    private void SaveDay(DayCell cell)
    {
        Database.SetValue(cell.Habit.Id, cell.Date, cell.Value);
        RefreshStats(cell.Habit);

        // These views depend on today's status or the score, so re-sort them.
        if (HideDoneBox.IsChecked == true || SortBox.SelectedIndex >= 3)
        {
            ApplyView();
        }
    }

    // ---------- Export ----------

    // ---------- Settings ----------

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow { Owner = this };
        window.ShowDialog();

        // A restore replaces every habit, so reload everything.
        if (window.DataRestored)
        {
            _reminded.Clear();
            ReloadAll();
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
        foreach (var habit in _allHabits)
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
            MessageBox.Show(this, "Your history was saved.", "Export",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception error)
        {
            MessageBox.Show(this, "The file could not be saved.\n\n" + error.Message, "Export",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string Quote(string text)
    {
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}