using System;
using System.Collections.Generic;
using System.Linq;

namespace HabitApp;

public class Streak
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public int Length => (End - Start).Days + 1;
}

public class HabitStats
{
    public double Score { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public DateTime StartDay { get; set; } = DateTime.Today;
    public List<double> Scores { get; } = new();
    public List<Streak> Streaks { get; } = new();
}

public static class Stats
{
    public static HabitStats Compute(
        Habit habit, Dictionary<DateTime, double> entries, DateTime today)
    {
        var stats = new HabitStats { StartDay = today };
        if (entries.Count == 0)
        {
            return stats;
        }

        DateTime start = entries.Keys.Min();
        if (start > today)
        {
            start = today;
        }
        stats.StartDay = start;

        int total = (today - start).Days + 1;

        // "needed" completions inside every "window" of days.
        int window = Math.Max(1, habit.FreqDen);
        int needed = Math.Max(1, Math.Min(habit.FreqNum, window));

        // How quickly old days fade. Rarer habits fade more slowly.
        double halfLife = Math.Max(14, 2 * window);
        double decay = Math.Pow(0.5, 1.0 / halfLife);

        var onTrack = new bool[total];
        var recent = new Queue<double>();
        double windowSum = 0;
        double score = 0;

        for (int i = 0; i < total; i++)
        {
            bool found = entries.TryGetValue(start.AddDays(i), out double value);

            if (found && value == Habit.SkipValue)
            {
                // Skipped days are left out: the score holds steady
                // and an existing streak carries on through them.
                stats.Scores.Add(score);
                onTrack[i] = i > 0 && onTrack[i - 1];
                continue;
            }

            double credit = found ? habit.Credit(value) : 0;
            recent.Enqueue(credit);
            windowSum += credit;
            if (recent.Count > window)
            {
                windowSum -= recent.Dequeue();
            }

            double share = Math.Min(1, windowSum / needed);
            score = score * decay + share * (1 - decay);

            stats.Scores.Add(score);
            onTrack[i] = share >= 0.999;
        }

        int last = total - 1;

        // Today isn't over yet, so an empty today doesn't pull the score down.
        if (!entries.ContainsKey(today) && total > 1)
        {
            stats.Scores[last] = stats.Scores[last - 1];
        }
        stats.Score = stats.Scores[last];

        // Every unbroken run of on-track days.
        int runStart = -1;
        for (int i = 0; i <= total; i++)
        {
            bool on = i < total && onTrack[i];
            if (on && runStart < 0)
            {
                runStart = i;
            }
            if (!on && runStart >= 0)
            {
                stats.Streaks.Add(new Streak
                {
                    Start = start.AddDays(runStart),
                    End = start.AddDays(i - 1)
                });
                runStart = -1;
            }
        }

        if (stats.Streaks.Count > 0)
        {
            stats.BestStreak = stats.Streaks.Max(s => s.Length);
        }

        // Current run, counted back from today (or yesterday if today isn't done yet).
        int index = onTrack[last] ? last : last - 1;
        while (index >= 0 && onTrack[index])
        {
            stats.CurrentStreak++;
            index--;
        }

        return stats;
    }
}