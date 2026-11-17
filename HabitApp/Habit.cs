using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace HabitApp;

public class Habit : INotifyPropertyChanged
{
    // Stored as the value of a day the user chose to skip.
    public const double SkipValue = -1;

    public static readonly string[] Palette =
    {
        "#2F7D6B", "#C8553D", "#3D6FB4", "#B8862B",
        "#7A4EA3", "#4E8F3A", "#B5477A", "#3A8C9E"
    };

    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Question { get; set; } = "";
    public string Description { get; set; } = "";
    public string Color { get; set; } = "#2F7D6B";
    public bool IsNumeric { get; set; }
    public bool AtMost { get; set; }
    public int FreqNum { get; set; } = 1;
    public int FreqDen { get; set; } = 1;
    public double Target { get; set; }
    public string Unit { get; set; } = "";
    public string Reminder { get; set; } = "";
    public int Position { get; set; }
    public bool IsArchived { get; set; }
    public ObservableCollection<DayCell> Days { get; } = new();

    public double Score { get; private set; }
    public int CurrentStreak { get; private set; }
    public int BestStreak { get; private set; }

    public string ScoreText => $"{Math.Round(Score * 100)}%";

    public string StreakText =>
        CurrentStreak == 1 ? "1 day streak" : $"{CurrentStreak} day streak";

    public string StatsTooltip =>
        $"Strength {ScoreText}. Best streak: {BestStreak} " +
        (BestStreak == 1 ? "day." : "days.");

    public string NameTooltip => Question.Length > 0 ? Question : "Open details";
    public string ArchiveLabel => IsArchived ? "Unarchive" : "Archive";
    public double RowOpacity => IsArchived ? 0.55 : 1;

    // Done or skipped today, so nothing is left to do.
    public bool IsDoneToday
    {
        get
        {
            if (Days.Count == 0)
            {
                return false;
            }
            DayCell today = Days[Days.Count - 1];
            return today.IsDone || today.IsSkipped;
        }
    }

    // How much a logged value counts, from 0 (nothing) to 1 (fully done).
    public double Credit(double? value)
    {
        if (value == null || value.Value < 0)
        {
            return 0;
        }

        double amount = value.Value;
        if (!IsNumeric)
        {
            return amount > 0 ? 1 : 0;
        }
        if (AtMost)
        {
            return amount <= Target ? 1 : 0;
        }
        if (Target <= 0)
        {
            return amount > 0 ? 1 : 0;
        }
        return Math.Min(amount / Target, 1);
    }

    public bool IsDone(double? value)
    {
        return Credit(value) >= 0.999;
    }

    public void SetStats(HabitStats stats)
    {
        Score = stats.Score;
        CurrentStreak = stats.CurrentStreak;
        BestStreak = stats.BestStreak;
        Notify();
    }

    public void Notify()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public string Summary
    {
        get
        {
            string frequency;
            if (FreqNum >= FreqDen)
            {
                frequency = "Every day";
            }
            else if (FreqDen == 7)
            {
                frequency = FreqNum == 1 ? "Once a week" : $"{FreqNum} times a week";
            }
            else if (FreqDen == 30)
            {
                frequency = FreqNum == 1 ? "Once a month" : $"{FreqNum} times a month";
            }
            else if (FreqNum == 1)
            {
                frequency = $"Every {FreqDen} days";
            }
            else
            {
                frequency = $"{FreqNum} times in {FreqDen} days";
            }

            if (IsNumeric && (AtMost || Target > 0))
            {
                string limit = AtMost ? "at most" : "at least";
                return $"{frequency} · {limit} {DayCell.Format(Target)} {Unit}".Trim();
            }
            return frequency;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class DayCell : INotifyPropertyChanged
{
    private double? _value;
    private string _note;

    public DayCell(Habit habit, DateTime date, double? value, string note)
    {
        Habit = habit;
        Date = date;
        _value = value;
        _note = note;
    }

    public Habit Habit { get; }
    public DateTime Date { get; }
    public string Color => Habit.Color;

    // Null means nothing has been logged for this day.
    public double? Value
    {
        get => _value;
        set
        {
            _value = value;
            Changed();
        }
    }

    public string Note
    {
        get => _note;
        set
        {
            _note = value ?? "";
            Changed();
        }
    }

    public bool IsSkipped => _value == Habit.SkipValue;
    public bool IsDone => Habit.IsDone(_value);
    public bool ShowTick => !Habit.IsNumeric && IsDone;
    public bool HasNote => _note.Length > 0;

    public string Text => Habit.IsNumeric && _value >= 0 ? Format(_value!.Value) : "";

    public string SkipLabel => IsSkipped ? "Remove skip" : "Skip this day";
    public string NoteLabel => HasNote ? "Edit note…" : "Add note…";

    public string Label
    {
        get
        {
            string text = Date.ToString("ddd, d MMM");
            if (IsSkipped)
            {
                text += " · skipped";
            }
            else if (Habit.IsNumeric && _value >= 0)
            {
                text += $": {_value:0.##} {Habit.Unit}".TrimEnd();
            }
            else if (IsDone)
            {
                text += " · done";
            }

            if (HasNote)
            {
                text += "\n" + _note;
            }
            return text;
        }
    }

    public static string Format(double value)
    {
        if (value >= 10000) return (value / 1000).ToString("0") + "k";
        if (value >= 1000) return (value / 1000).ToString("0.#") + "k";
        if (value >= 100) return value.ToString("0");
        if (value >= 10) return value.ToString("0.#");
        return value.ToString("0.##");
    }

    private void Changed()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class DayHeader
{
    public string DayName { get; set; } = "";
    public string DayNumber { get; set; } = "";
    public bool IsToday { get; set; }
}