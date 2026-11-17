using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;

namespace HabitApp;

public static class Database
{
    private const string DayFormat = "yyyy-MM-dd";
    private static readonly string DbPath = BuildPath();

    private static string BuildPath()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HabitApp"); // Folder name kept from before the rename so existing data is found.
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "habits.db");
    }

    private static SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={DbPath}");
        connection.Open();
        return connection;
    }

    private static string ToText(DateTime day)
    {
        return day.ToString(DayFormat, CultureInfo.InvariantCulture);
    }

    private static DateTime FromText(string text)
    {
        return DateTime.ParseExact(text, DayFormat, CultureInfo.InvariantCulture);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    // ---------- Setup ----------

    public static void Initialize()
    {
        using var connection = Open();

        Execute(connection, @"
            CREATE TABLE IF NOT EXISTS Habits (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Color TEXT NOT NULL,
                Kind INTEGER NOT NULL DEFAULT 0,
                FreqNum INTEGER NOT NULL DEFAULT 1,
                FreqDen INTEGER NOT NULL DEFAULT 1,
                Target REAL NOT NULL DEFAULT 0,
                Unit TEXT NOT NULL DEFAULT '',
                Position INTEGER NOT NULL DEFAULT 0,
                Archived INTEGER NOT NULL DEFAULT 0,
                CreatedOn TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS Entries (
                HabitId INTEGER NOT NULL,
                Day TEXT NOT NULL,
                Value REAL NOT NULL,
                PRIMARY KEY (HabitId, Day)
            );
            CREATE TABLE IF NOT EXISTS Notes (
                HabitId INTEGER NOT NULL,
                Day TEXT NOT NULL,
                Body TEXT NOT NULL,
                PRIMARY KEY (HabitId, Day)
            );
            CREATE TABLE IF NOT EXISTS Settings (
                Name TEXT PRIMARY KEY,
                Value TEXT NOT NULL
            );");

        // Columns added in later versions of the app.
        AddColumnIfMissing(connection, "Reminder", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "Question", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "Description", "TEXT NOT NULL DEFAULT ''");
        AddColumnIfMissing(connection, "TargetType", "INTEGER NOT NULL DEFAULT 0");
    }

    private static void AddColumnIfMissing(
        SqliteConnection connection, string column, string definition)
    {
        using var check = connection.CreateCommand();
        check.CommandText =
            "SELECT COUNT(*) FROM pragma_table_info('Habits') WHERE name = $name";
        check.Parameters.AddWithValue("$name", column);
        long found = (long)check.ExecuteScalar()!;
        if (found == 0)
        {
            Execute(connection, $"ALTER TABLE Habits ADD COLUMN {column} {definition}");
        }
    }

    // ---------- Habits ----------

    public static List<Habit> GetHabits()
    {
        var habits = new List<Habit>();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, Name, Color, Kind, FreqNum, FreqDen, Target, Unit,
                   Reminder, Question, Description, TargetType, Position, Archived
            FROM Habits ORDER BY Position, Id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            habits.Add(new Habit
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                Color = reader.GetString(2),
                IsNumeric = reader.GetInt32(3) == 1,
                FreqNum = reader.GetInt32(4),
                FreqDen = reader.GetInt32(5),
                Target = reader.GetDouble(6),
                Unit = reader.GetString(7),
                Reminder = reader.GetString(8),
                Question = reader.GetString(9),
                Description = reader.GetString(10),
                AtMost = reader.GetInt32(11) == 1,
                Position = reader.GetInt32(12),
                IsArchived = reader.GetInt32(13) == 1
            });
        }
        return habits;
    }

    public static void SaveHabit(Habit habit)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();

        if (habit.Id == 0)
        {
            command.CommandText = @"
                INSERT INTO Habits
                    (Name, Question, Description, Color, Kind, TargetType, FreqNum, FreqDen,
                     Target, Unit, Reminder, Position, CreatedOn)
                VALUES
                    ($name, $question, $description, $color, $kind, $targetType, $num, $den,
                     $target, $unit, $reminder,
                     (SELECT COALESCE(MAX(Position), 0) + 1 FROM Habits), $created);
                SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$created", ToText(DateTime.Today));
        }
        else
        {
            command.CommandText = @"
                UPDATE Habits
                SET Name = $name, Question = $question, Description = $description,
                    Color = $color, Kind = $kind, TargetType = $targetType,
                    FreqNum = $num, FreqDen = $den, Target = $target, Unit = $unit,
                    Reminder = $reminder
                WHERE Id = $id";
            command.Parameters.AddWithValue("$id", habit.Id);
        }

        command.Parameters.AddWithValue("$name", habit.Name);
        command.Parameters.AddWithValue("$question", habit.Question);
        command.Parameters.AddWithValue("$description", habit.Description);
        command.Parameters.AddWithValue("$color", habit.Color);
        command.Parameters.AddWithValue("$kind", habit.IsNumeric ? 1 : 0);
        command.Parameters.AddWithValue("$targetType", habit.AtMost ? 1 : 0);
        command.Parameters.AddWithValue("$num", habit.FreqNum);
        command.Parameters.AddWithValue("$den", habit.FreqDen);
        command.Parameters.AddWithValue("$target", habit.Target);
        command.Parameters.AddWithValue("$unit", habit.Unit);
        command.Parameters.AddWithValue("$reminder", habit.Reminder);

        if (habit.Id == 0)
        {
            habit.Id = (long)command.ExecuteScalar()!;
        }
        else
        {
            command.ExecuteNonQuery();
        }
    }

    public static void SetArchived(long id, bool archived)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Habits SET Archived = $archived WHERE Id = $id";
        command.Parameters.AddWithValue("$archived", archived ? 1 : 0);
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    public static void SetPositions(IList<long> ids)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        for (int i = 0; i < ids.Count; i++)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE Habits SET Position = $position WHERE Id = $id";
            command.Parameters.AddWithValue("$position", i);
            command.Parameters.AddWithValue("$id", ids[i]);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public static void DeleteHabit(long id)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM Entries WHERE HabitId = $id;
            DELETE FROM Notes WHERE HabitId = $id;
            DELETE FROM Habits WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id);
        command.ExecuteNonQuery();
    }

    // ---------- Daily values ----------

    public static Dictionary<DateTime, double> GetEntries(long habitId, DateTime from, DateTime to)
    {
        var entries = new Dictionary<DateTime, double>();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Day, Value FROM Entries
            WHERE HabitId = $id AND Day >= $from AND Day <= $to";
        command.Parameters.AddWithValue("$id", habitId);
        command.Parameters.AddWithValue("$from", ToText(from));
        command.Parameters.AddWithValue("$to", ToText(to));
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            entries[FromText(reader.GetString(0))] = reader.GetDouble(1);
        }
        return entries;
    }

    // A null value clears the day.
    public static void SetValue(long habitId, DateTime day, double? value)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        if (value.HasValue)
        {
            command.CommandText =
                "INSERT OR REPLACE INTO Entries (HabitId, Day, Value) VALUES ($id, $day, $value)";
            command.Parameters.AddWithValue("$value", value.Value);
        }
        else
        {
            command.CommandText = "DELETE FROM Entries WHERE HabitId = $id AND Day = $day";
        }
        command.Parameters.AddWithValue("$id", habitId);
        command.Parameters.AddWithValue("$day", ToText(day));
        command.ExecuteNonQuery();
    }

    // ---------- Notes ----------

    public static Dictionary<DateTime, string> GetNotes(long habitId, DateTime from, DateTime to)
    {
        var notes = new Dictionary<DateTime, string>();
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Day, Body FROM Notes
            WHERE HabitId = $id AND Day >= $from AND Day <= $to";
        command.Parameters.AddWithValue("$id", habitId);
        command.Parameters.AddWithValue("$from", ToText(from));
        command.Parameters.AddWithValue("$to", ToText(to));
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            notes[FromText(reader.GetString(0))] = reader.GetString(1);
        }
        return notes;
    }

    // Empty text removes the note.
    public static void SetNote(long habitId, DateTime day, string text)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        if (text.Length > 0)
        {
            command.CommandText =
                "INSERT OR REPLACE INTO Notes (HabitId, Day, Body) VALUES ($id, $day, $body)";
            command.Parameters.AddWithValue("$body", text);
        }
        else
        {
            command.CommandText = "DELETE FROM Notes WHERE HabitId = $id AND Day = $day";
        }
        command.Parameters.AddWithValue("$id", habitId);
        command.Parameters.AddWithValue("$day", ToText(day));
        command.ExecuteNonQuery();
    }

    // ---------- Settings ----------


    // ---------- Backup and restore ----------

    public static string DataFolder => Path.GetDirectoryName(DbPath)!;

    public static void BackupTo(string file)
    {
        if (File.Exists(file))
        {
            File.Delete(file);
        }

        using var source = Open();
        using var target = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString());
        target.Open();
        source.BackupDatabase(target);
    }

    public static void RestoreFrom(string file)
    {
        if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(DbPath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("That is the file the app is already using.");
        }

        using var backup = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = file,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        backup.Open();

        using (var check = backup.CreateCommand())
        {
            check.CommandText = @"
                SELECT COUNT(*) FROM sqlite_master
                WHERE type = 'table' AND name IN ('Habits', 'Entries')";
            long tables = (long)check.ExecuteScalar()!;
            if (tables < 2)
            {
                throw new InvalidDataException("This file is not a habit backup.");
            }
        }

        // Keep a copy of the current data, just in case.
        BackupTo(Path.Combine(DataFolder, "before-restore.db"));

        using (var target = Open())
        {
            backup.BackupDatabase(target);
        }

        // Older backups may be missing newer columns or tables.
        Initialize();
    }
    public static string GetSetting(string name, string fallback)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Name = $name";
        command.Parameters.AddWithValue("$name", name);
        return command.ExecuteScalar() as string ?? fallback;
    }

    public static void SetSetting(string name, string value)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "INSERT OR REPLACE INTO Settings (Name, Value) VALUES ($name, $value)";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }
}