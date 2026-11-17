using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace HabitApp;

public partial class DetailWindow : Window
{
    private enum Period { Day, Week, Month, Quarter, Year }

    private const double ChartWidth = 690;

    private readonly Habit _habit;
    private readonly DateTime _today = DateTime.Today;
    private readonly Color _accentColor;
    private readonly Brush _accent;
    private readonly Brush _muted;
    private readonly Brush _faint;
    private readonly Brush _ink;
    private readonly bool _ready;

    private Dictionary<DateTime, double> _entries = new();
    private Dictionary<DateTime, string> _notes = new();
    private HabitStats _stats = new();
    private int _calendarOffset; // how many weeks back the calendar is scrolled

    public DetailWindow(Habit habit)
    {
        InitializeComponent();

        _habit = habit;
        _accentColor = ParseColor(habit.Color);
        _accent = new SolidColorBrush(_accentColor);
        _muted = (Brush)FindResource("TextMuted");
        _faint = (Brush)FindResource("CellEmpty");
        _ink = (Brush)FindResource("TextPrimary");

        Title = habit.Name;
        ColorDot.Fill = _accent;
        NameText.Text = habit.Name;

        QuestionText.Text = habit.Question;
        QuestionText.Foreground = _accent;
        QuestionText.Visibility = habit.Question.Length > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        SummaryText.Text = habit.Reminder.Length > 0
            ? $"{habit.Summary} · Reminder at {habit.Reminder}"
            : habit.Summary;

        DescriptionText.Text = habit.Description;
        DescriptionText.Visibility = habit.Description.Length > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        ScorePeriod.SelectedIndex = 1;
        HistoryPeriod.SelectedIndex = 1;

        _ready = true;
        Refresh();
    }

    // ---------- Shared helpers ----------

    private void Refresh()
    {
        _entries = Database.GetEntries(_habit.Id, DateTime.MinValue, _today);
        _notes = Database.GetNotes(_habit.Id, DateTime.MinValue, _today);
        _stats = Stats.Compute(_habit, _entries, _today);

        DrawOverview();
        DrawScoreChart();
        DrawHistoryChart();
        DrawCalendar();
        DrawStreaks();
        DrawNotes();
        DrawFrequency();
        DrawWeekdays();
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.Teal;
        }
    }

    private static string DaysText(int days)
    {
        return days == 1 ? "1 day" : $"{days} days";
    }

    private static void AddText(Canvas canvas, string text, double x, double y,
        double size, Brush brush, double width = 0)
    {
        var block = new TextBlock { Text = text, FontSize = size, Foreground = brush };
        if (width > 0)
        {
            block.Width = width;
            block.TextAlignment = TextAlignment.Center;
        }
        Canvas.SetLeft(block, x);
        Canvas.SetTop(block, y);
        canvas.Children.Add(block);
    }

    // How much one day counts, from 0 (nothing) to 1 (fully done).
    private double Credit(DateTime date)
    {
        return _entries.TryGetValue(date, out double value) ? _habit.Credit(value) : 0;
    }

    private bool IsSkipped(DateTime date)
    {
        return _entries.TryGetValue(date, out double value) && value == Habit.SkipValue;
    }

    private double ScoreOn(DateTime date)
    {
        if (_stats.Scores.Count == 0)
        {
            return 0;
        }

        int index = (date - _stats.StartDay).Days;
        if (index < 0)
        {
            return 0;
        }
        if (index >= _stats.Scores.Count)
        {
            index = _stats.Scores.Count - 1;
        }
        return _stats.Scores[index];
    }

    private static DateTime BucketStart(DateTime date, Period period)
    {
        switch (period)
        {
            case Period.Week:
                int first = (int)CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
                int offset = ((int)date.DayOfWeek - first + 7) % 7;
                return date.AddDays(-offset);
            case Period.Month:
                return new DateTime(date.Year, date.Month, 1);
            case Period.Quarter:
                return new DateTime(date.Year, (date.Month - 1) / 3 * 3 + 1, 1);
            case Period.Year:
                return new DateTime(date.Year, 1, 1);
            default:
                return date;
        }
    }

    private static DateTime Shift(DateTime start, Period period, int steps)
    {
        switch (period)
        {
            case Period.Week:
                return start.AddDays(7 * steps);
            case Period.Month:
                return start.AddMonths(steps);
            case Period.Quarter:
                return start.AddMonths(3 * steps);
            case Period.Year:
                return start.AddYears(steps);
            default:
                return start.AddDays(steps);
        }
    }

    private static string BucketLabel(DateTime start, Period period)
    {
        switch (period)
        {
            case Period.Month:
                return start.ToString("MMM yy");
            case Period.Quarter:
                return $"Q{(start.Month - 1) / 3 + 1} {start:yy}";
            case Period.Year:
                return start.ToString("yyyy");
            default:
                return start.ToString("d MMM");
        }
    }

    private static int BucketCount(Period period)
    {
        switch (period)
        {
            case Period.Day:
                return 30;
            case Period.Week:
                return 20;
            case Period.Month:
                return 12;
            case Period.Quarter:
                return 8;
            default:
                return 5;
        }
    }

    private void Period_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }
        DrawScoreChart();
        DrawHistoryChart();
    }

    // ---------- Overview ----------

    private void DrawOverview()
    {
        double now = _stats.Score;
        StrengthValue.Text = $"{Math.Round(now * 100)}%";
        MonthValue.Text = ChangeText(now - ScoreOn(_today.AddDays(-30)));
        YearValue.Text = ChangeText(now - ScoreOn(_today.AddDays(-365)));
        CurrentValue.Text = DaysText(_stats.CurrentStreak);
        BestValue.Text = DaysText(_stats.BestStreak);

        if (_habit.IsNumeric)
        {
            TotalLabel.Text = _habit.Unit.Length > 0 ? $"Total {_habit.Unit}" : "Total";
            TotalValue.Text = _entries.Values.Where(v => v > 0).Sum().ToString("#,0.##");
        }
        else
        {
            TotalLabel.Text = "Days completed";
            TotalValue.Text = _entries.Values.Count(v => v > 0).ToString();
        }
    }

    private static string ChangeText(double difference)
    {
        int points = (int)Math.Round(difference * 100);
        return (points >= 0 ? "+" : "") + points + "%";
    }

    // ---------- Strength chart ----------

    private void DrawScoreChart()
    {
        ScoreCanvas.Children.Clear();

        var period = (Period)Math.Max(0, ScorePeriod.SelectedIndex);
        int count = BucketCount(period);

        const double height = 190;
        const double left = 40;
        const double right = 14;
        const double top = 10;
        const double bottom = 26;

        double plotWidth = ChartWidth - left - right;
        double plotHeight = height - top - bottom;

        for (int percent = 0; percent <= 100; percent += 25)
        {
            double y = top + plotHeight * (1 - percent / 100.0);
            ScoreCanvas.Children.Add(new Line
            {
                X1 = left,
                X2 = ChartWidth,
                Y1 = y,
                Y2 = y,
                Stroke = _faint,
                StrokeThickness = 1
            });
            AddText(ScoreCanvas, percent + "%", 0, y - 8, 11, _muted);
        }

        DateTime firstBucket = Shift(BucketStart(_today, period), period, -(count - 1));
        int labelEvery = (int)Math.Ceiling(count / 10.0);

        var line = new Polyline
        {
            Stroke = _accent,
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round
        };
        var points = new List<Point>();

        for (int i = 0; i < count; i++)
        {
            DateTime start = Shift(firstBucket, period, i);
            DateTime end = Shift(start, period, 1).AddDays(-1);
            if (end > _today)
            {
                end = _today;
            }

            double x = left + plotWidth * i / (count - 1);
            double y = top + plotHeight * (1 - ScoreOn(end));
            line.Points.Add(new Point(x, y));
            points.Add(new Point(x, y));

            if ((count - 1 - i) % labelEvery == 0)
            {
                AddText(ScoreCanvas, BucketLabel(start, period),
                    x - 30, height - 18, 10, _muted, 60);
            }
        }

        ScoreCanvas.Children.Add(line);

        foreach (Point point in points)
        {
            var dot = new Ellipse { Width = 7, Height = 7, Fill = _accent };
            Canvas.SetLeft(dot, point.X - 3.5);
            Canvas.SetTop(dot, point.Y - 3.5);
            ScoreCanvas.Children.Add(dot);
        }
    }

    // ---------- History bar chart ----------

    private void DrawHistoryChart()
    {
        HistoryCanvas.Children.Clear();

        var period = (Period)Math.Max(0, HistoryPeriod.SelectedIndex);
        int count = BucketCount(period);

        const double height = 190;
        const double top = 22;
        const double bottom = 26;
        double plotHeight = height - top - bottom;
        double baseline = top + plotHeight;

        DateTime firstBucket = Shift(BucketStart(_today, period), period, -(count - 1));

        var totals = new double[count];
        for (int i = 0; i < count; i++)
        {
            DateTime start = Shift(firstBucket, period, i);
            DateTime next = Shift(start, period, 1);

            foreach (KeyValuePair<DateTime, double> entry in _entries)
            {
                if (entry.Key >= start && entry.Key < next && entry.Value > 0)
                {
                    totals[i] += _habit.IsNumeric ? entry.Value : 1;
                }
            }
        }

        double max = Math.Max(1, totals.Max());
        double slot = ChartWidth / count;
        double barWidth = Math.Min(30, slot * 0.6);
        int labelEvery = (int)Math.Ceiling(count / 10.0);

        HistoryCanvas.Children.Add(new Line
        {
            X1 = 0,
            X2 = ChartWidth,
            Y1 = baseline,
            Y2 = baseline,
            Stroke = _faint,
            StrokeThickness = 1
        });

        for (int i = 0; i < count; i++)
        {
            DateTime start = Shift(firstBucket, period, i);
            double x = i * slot + (slot - barWidth) / 2;

            if (totals[i] > 0)
            {
                double barHeight = Math.Max(4, plotHeight * totals[i] / max);
                var bar = new Rectangle
                {
                    Width = barWidth,
                    Height = barHeight,
                    RadiusX = 4,
                    RadiusY = 4,
                    Fill = _accent
                };
                Canvas.SetLeft(bar, x);
                Canvas.SetTop(bar, baseline - barHeight);
                HistoryCanvas.Children.Add(bar);

                AddText(HistoryCanvas, DayCell.Format(totals[i]),
                    i * slot, baseline - barHeight - 16, 10, _accent, slot);
            }

            if ((count - 1 - i) % labelEvery == 0)
            {
                AddText(HistoryCanvas, BucketLabel(start, period),
                    i * slot + slot / 2 - 30, height - 18, 10, _muted, 60);
            }
        }
    }

    // ---------- Calendar ----------

    private void DrawCalendar()
    {
        HeatCanvas.Children.Clear();

        const int weeks = 28;
        const double left = 36;
        const double top = 18;
        const double pitch = 23;
        const double size = 20;

        DateTimeFormatInfo format = CultureInfo.CurrentCulture.DateTimeFormat;
        int firstDay = (int)format.FirstDayOfWeek;

        DateTime thisWeek = BucketStart(_today, Period.Week);
        DateTime lastWeek = thisWeek.AddDays(-7 * _calendarOffset);
        DateTime gridStart = lastWeek.AddDays(-7 * (weeks - 1));

        LaterButton.IsEnabled = _calendarOffset > 0;
        LaterButton.Opacity = _calendarOffset > 0 ? 1 : 0.35;
        CalendarRange.Text = $"{gridStart:MMM yyyy} – {lastWeek.AddDays(6):MMM yyyy}";

        for (int row = 0; row < 7; row++)
        {
            string dayName = format.AbbreviatedDayNames[(firstDay + row) % 7];
            AddText(HeatCanvas, dayName, 0, top + row * pitch + 3, 10, _muted);
        }

        for (int column = 0; column < weeks; column++)
        {
            DateTime weekStart = gridStart.AddDays(column * 7);
            double x = left + column * pitch;

            if (weekStart.Day <= 7)
            {
                AddText(HeatCanvas, weekStart.ToString("MMM"), x, 0, 10, _muted);
            }

            for (int row = 0; row < 7; row++)
            {
                DateTime date = weekStart.AddDays(row);
                if (date > _today)
                {
                    continue;
                }

                double credit = Credit(date);
                bool skipped = IsSkipped(date);
                bool hasNote = _notes.ContainsKey(date);

                Brush fill = _faint;
                if (credit > 0)
                {
                    fill = new SolidColorBrush(_accentColor)
                    {
                        Opacity = 0.35 + 0.65 * credit
                    };
                }

                var content = new Grid();
                content.Children.Add(new TextBlock
                {
                    Text = date.Day.ToString(),
                    FontSize = 9,
                    Foreground = credit > 0 ? Brushes.White : _muted,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });

                if (hasNote)
                {
                    content.Children.Add(new Ellipse
                    {
                        Width = 5,
                        Height = 5,
                        Fill = credit > 0 ? Brushes.White : _ink,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(0, 2, 2, 0)
                    });
                }

                var cell = new Border
                {
                    Width = size,
                    Height = size,
                    CornerRadius = new CornerRadius(5),
                    Background = skipped ? Brushes.Transparent : fill,
                    Cursor = Cursors.Hand,
                    Child = content
                };

                if (skipped)
                {
                    // Skipped days are drawn as an outline.
                    cell.BorderBrush = _accent;
                    cell.BorderThickness = new Thickness(1.5);
                }
                else if (date == _today)
                {
                    cell.BorderBrush = _ink;
                    cell.BorderThickness = new Thickness(1.5);
                }

                string tip = date.ToString("ddd, d MMM yyyy");
                if (skipped)
                {
                    tip += " · skipped";
                }
                else if (_entries.TryGetValue(date, out double value))
                {
                    if (_habit.IsNumeric)
                    {
                        tip += $": {value:0.##} {_habit.Unit}".TrimEnd();
                    }
                    else if (value > 0)
                    {
                        tip += " · done";
                    }
                }
                if (_notes.TryGetValue(date, out string? note))
                {
                    tip += "\n" + note;
                }
                cell.ToolTip = tip;

                DateTime clicked = date;
                cell.MouseLeftButtonUp += (_, _) => EditDay(clicked);
                cell.MouseRightButtonUp += (_, e) =>
                {
                    ShowDayMenu(clicked, cell);
                    e.Handled = true;
                };

                Canvas.SetLeft(cell, x);
                Canvas.SetTop(cell, top + row * pitch);
                HeatCanvas.Children.Add(cell);
            }
        }
    }

    private void EditDay(DateTime date)
    {
        double? current = _entries.TryGetValue(date, out double found) ? found : (double?)null;
        double? value;

        if (_habit.IsNumeric)
        {
            string unit = _habit.Unit.Length > 0 ? $" ({_habit.Unit})" : "";
            string prompt = $"Amount for {date:ddd, d MMM yyyy}{unit}";
            var dialog = new NumberDialog(_habit.Name, prompt, current) { Owner = this };
            if (dialog.ShowDialog() != true)
            {
                return;
            }
            value = dialog.Value;
        }
        else
        {
            value = Credit(date) > 0 ? (double?)null : 1;
        }

        Database.SetValue(_habit.Id, date, value);
        Refresh();
    }

    private void ShowDayMenu(DateTime date, FrameworkElement target)
    {
        bool skipped = IsSkipped(date);
        bool hasNote = _notes.ContainsKey(date);

        var menu = new ContextMenu { PlacementTarget = target };

        var skip = new MenuItem { Header = skipped ? "Remove skip" : "Skip this day" };
        skip.Click += (_, _) =>
        {
            Database.SetValue(_habit.Id, date, skipped ? (double?)null : Habit.SkipValue);
            Refresh();
        };

        var note = new MenuItem { Header = hasNote ? "Edit note…" : "Add note…" };
        note.Click += (_, _) => EditNote(date);

        var clear = new MenuItem { Header = "Clear check-in" };
        clear.Click += (_, _) =>
        {
            Database.SetValue(_habit.Id, date, null);
            Refresh();
        };

        menu.Items.Add(skip);
        menu.Items.Add(note);
        menu.Items.Add(new Separator());
        menu.Items.Add(clear);
        menu.IsOpen = true;
    }

    private void EditNote(DateTime date)
    {
        _notes.TryGetValue(date, out string? current);
        var dialog = new NoteDialog($"{_habit.Name}, {date:ddd, d MMM yyyy}", current ?? "")
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        Database.SetNote(_habit.Id, date, dialog.NoteText);
        Refresh();
    }

    private void Earlier_Click(object sender, RoutedEventArgs e)
    {
        _calendarOffset += 13;
        DrawCalendar();
    }

    private void Later_Click(object sender, RoutedEventArgs e)
    {
        _calendarOffset = Math.Max(0, _calendarOffset - 13);
        DrawCalendar();
    }

    // ---------- Best streaks ----------

    private void DrawStreaks()
    {
        StreakPanel.Children.Clear();

        List<Streak> best = _stats.Streaks
            .OrderByDescending(s => s.Length)
            .ThenByDescending(s => s.End)
            .Take(5)
            .ToList();

        if (best.Count == 0)
        {
            StreakPanel.Children.Add(new TextBlock
            {
                Text = "No streaks yet. Keep going and they will show up here.",
                Foreground = _muted
            });
            return;
        }

        int longest = best[0].Length;

        foreach (Streak streak in best)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
            row.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

            var length = new TextBlock
            {
                Text = DaysText(streak.Length),
                FontWeight = FontWeights.SemiBold,
                Foreground = _ink,
                VerticalAlignment = VerticalAlignment.Center
            };

            var bar = new Border
            {
                Height = 14,
                Width = Math.Max(14, 300.0 * streak.Length / longest),
                CornerRadius = new CornerRadius(7),
                Background = _accent,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };

            var dates = new TextBlock
            {
                Text = $"{streak.Start:d MMM yyyy} – {streak.End:d MMM yyyy}",
                FontSize = 12,
                Foreground = _muted,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid.SetColumn(bar, 1);
            Grid.SetColumn(dates, 2);
            row.Children.Add(length);
            row.Children.Add(bar);
            row.Children.Add(dates);
            StreakPanel.Children.Add(row);
        }
    }

    // ---------- Notes ----------

    private void DrawNotes()
    {
        NotesPanel.Children.Clear();

        if (_notes.Count == 0)
        {
            NotesPanel.Children.Add(new TextBlock
            {
                Text = "No notes yet. Right-click a day in the calendar, " +
                       "or a square on the main screen, to add one.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = _muted
            });
            return;
        }

        foreach (KeyValuePair<DateTime, string> pair in _notes.OrderByDescending(p => p.Key).Take(20))
        {
            var row = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            row.Children.Add(new TextBlock
            {
                Text = pair.Key.ToString("dddd, d MMM yyyy"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = _accent
            });
            row.Children.Add(new TextBlock
            {
                Text = pair.Value,
                TextWrapping = TextWrapping.Wrap,
                Foreground = _ink,
                Margin = new Thickness(0, 2, 0, 0)
            });
            NotesPanel.Children.Add(row);
        }
    }

    // ---------- Frequency: weekdays by month ----------

    private void DrawFrequency()
    {
        FrequencyCanvas.Children.Clear();

        const int months = 12;
        const double left = 36;
        const double top = 20;
        const double rowPitch = 22;
        double columnPitch = (ChartWidth - left) / months;

        DateTimeFormatInfo format = CultureInfo.CurrentCulture.DateTimeFormat;
        int firstDay = (int)format.FirstDayOfWeek;

        DateTime firstMonth =
            new DateTime(_today.Year, _today.Month, 1).AddMonths(-(months - 1));

        var counts = new int[months, 7];
        foreach (DateTime day in _entries.Keys)
        {
            if (Credit(day) < 0.999)
            {
                continue;
            }

            int month = (day.Year - firstMonth.Year) * 12 + day.Month - firstMonth.Month;
            if (month >= 0 && month < months)
            {
                counts[month, (int)day.DayOfWeek]++;
            }
        }

        for (int row = 0; row < 7; row++)
        {
            string dayName = format.AbbreviatedDayNames[(firstDay + row) % 7];
            AddText(FrequencyCanvas, dayName, 0, top + row * rowPitch + 3, 10, _muted);
        }

        for (int month = 0; month < months; month++)
        {
            DateTime monthStart = firstMonth.AddMonths(month);
            AddText(FrequencyCanvas, monthStart.ToString("MMM"),
                left + month * columnPitch, 0, 10, _muted, columnPitch);

            for (int row = 0; row < 7; row++)
            {
                int day = (firstDay + row) % 7;
                int count = counts[month, day];

                double radius = count > 0 ? 3 + 6.0 * Math.Min(count, 5) / 5 : 1.5;
                var dot = new Ellipse
                {
                    Width = radius * 2,
                    Height = radius * 2,
                    Fill = count > 0 ? _accent : _faint,
                    ToolTip = $"{format.AbbreviatedDayNames[day]}, {monthStart:MMM yyyy}: " +
                              (count == 1 ? "1 time" : $"{count} times")
                };

                double centerX = left + month * columnPitch + columnPitch / 2;
                double centerY = top + row * rowPitch + rowPitch / 2;
                Canvas.SetLeft(dot, centerX - radius);
                Canvas.SetTop(dot, centerY - radius);
                FrequencyCanvas.Children.Add(dot);
            }
        }
    }

    // ---------- Weekday success ----------

    private void DrawWeekdays()
    {
        WeekdayCanvas.Children.Clear();

        const double barWidth = 44;
        const double maxHeight = 100;
        const double baseline = 124;

        DateTimeFormatInfo format = CultureInfo.CurrentCulture.DateTimeFormat;
        int firstDay = (int)format.FirstDayOfWeek;

        var done = new double[7];
        var count = new int[7];
        for (DateTime date = _stats.StartDay; date <= _today; date = date.AddDays(1))
        {
            if (IsSkipped(date))
            {
                continue; // skipped days don't count either way
            }

            int day = (int)date.DayOfWeek;
            count[day]++;
            done[day] += Credit(date);
        }

        double slot = ChartWidth / 7;
        for (int i = 0; i < 7; i++)
        {
            int day = (firstDay + i) % 7;
            double share = count[day] > 0 ? done[day] / count[day] : 0;
            double x = i * slot + (slot - barWidth) / 2;

            var track = new Rectangle
            {
                Width = barWidth,
                Height = maxHeight,
                RadiusX = 6,
                RadiusY = 6,
                Fill = _faint
            };
            Canvas.SetLeft(track, x);
            Canvas.SetTop(track, baseline - maxHeight);
            WeekdayCanvas.Children.Add(track);

            double barHeight = maxHeight * share;
            if (barHeight > 0)
            {
                var bar = new Rectangle
                {
                    Width = barWidth,
                    Height = Math.Max(barHeight, 6),
                    RadiusX = 6,
                    RadiusY = 6,
                    Fill = _accent
                };
                Canvas.SetLeft(bar, x);
                Canvas.SetTop(bar, baseline - bar.Height);
                WeekdayCanvas.Children.Add(bar);
            }

            AddText(WeekdayCanvas, $"{Math.Round(share * 100)}%",
                i * slot, 4, 11, _ink, slot);
            AddText(WeekdayCanvas, format.AbbreviatedDayNames[day],
                i * slot, baseline + 8, 12, _muted, slot);
        }
    }
}