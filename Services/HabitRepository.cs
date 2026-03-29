using Microsoft.Data.Sqlite;
using Goals.Models;

namespace Goals.Services;

public class HabitRepository(SqliteConnection connection)
{
    public async Task<List<Habit>> GetHabitsAsync()
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Name FROM Habits ORDER BY Id";

        var habits = new List<Habit>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            habits.Add(new Habit
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }
        return habits;
    }

    public async Task<Habit?> GetHabitByNameOrIdAsync(string input)
    {
        await using var cmd = connection.CreateCommand();
        if (int.TryParse(input, out var id))
        {
            cmd.CommandText = "SELECT Id, Name FROM Habits WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Id", id);
        }
        else
        {
            cmd.CommandText = "SELECT Id, Name FROM Habits WHERE Name = @Name COLLATE NOCASE";
            cmd.Parameters.AddWithValue("@Name", input);
        }

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Habit { Id = reader.GetInt32(0), Name = reader.GetString(1) };
        }
        return null;
    }

    public async Task AddHabitAsync(string name)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO Habits (Name) VALUES (@Name)";
        cmd.Parameters.AddWithValue("@Name", name);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> RemoveHabitAsync(int id)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM HabitLog WHERE HabitId = @Id";
        cmd.Parameters.AddWithValue("@Id", id);
        await cmd.ExecuteNonQueryAsync();

        await using var cmd2 = connection.CreateCommand();
        cmd2.CommandText = "DELETE FROM Habits WHERE Id = @Id";
        cmd2.Parameters.AddWithValue("@Id", id);
        return await cmd2.ExecuteNonQueryAsync() > 0;
    }

    public async Task RenameHabitAsync(int id, string newName)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE Habits SET Name = @Name WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.Parameters.AddWithValue("@Name", newName);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task CheckAsync(int habitId, DateOnly date)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO HabitLog (HabitId, Date)
            VALUES (@HabitId, @Date)
            """;
        cmd.Parameters.AddWithValue("@HabitId", habitId);
        cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UncheckAsync(int habitId, DateOnly date)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM HabitLog WHERE HabitId = @HabitId AND Date = @Date";
        cmd.Parameters.AddWithValue("@HabitId", habitId);
        cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> IsCheckedAsync(int habitId, DateOnly date)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM HabitLog WHERE HabitId = @HabitId AND Date = @Date";
        cmd.Parameters.AddWithValue("@HabitId", habitId);
        cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));
        return (long)(await cmd.ExecuteScalarAsync())! > 0;
    }

    public async Task<Dictionary<int, bool>> GetCheckedForDateAsync(DateOnly date)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT HabitId FROM HabitLog WHERE Date = @Date";
        cmd.Parameters.AddWithValue("@Date", date.ToString("yyyy-MM-dd"));

        var result = new Dictionary<int, bool>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetInt32(0)] = true;
        }
        return result;
    }

    public async Task<Dictionary<int, int>> ComputeStreaksAsync(List<Habit> habits, DateOnly today)
    {
        if (habits.Count == 0)
            return new Dictionary<int, int>();

        // Fetch all log entries for the lookback window in one query
        var lookbackStart = today.AddDays(-365);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT HabitId, Date FROM HabitLog WHERE Date >= @Start AND Date <= @End";
        cmd.Parameters.AddWithValue("@Start", lookbackStart.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@End", today.ToString("yyyy-MM-dd"));

        var logDates = new Dictionary<int, HashSet<DateOnly>>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var habitId = reader.GetInt32(0);
            var date = DateOnly.Parse(reader.GetString(1));
            if (!logDates.TryGetValue(habitId, out var dates))
            {
                dates = [];
                logDates[habitId] = dates;
            }
            dates.Add(date);
        }

        var streaks = new Dictionary<int, int>();
        foreach (var habit in habits)
        {
            logDates.TryGetValue(habit.Id, out var dates);
            dates ??= [];

            var streak = 0;
            // Include today if checked, otherwise start from yesterday
            var date = dates.Contains(today) ? today : today.AddDays(-1);

            for (int i = 0; i < 365; i++, date = date.AddDays(-1))
            {
                if (dates.Contains(date))
                    streak++;
                else
                    break;
            }

            streaks[habit.Id] = streak;
        }

        return streaks;
    }
}
