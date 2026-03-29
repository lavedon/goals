using Microsoft.Data.Sqlite;
using Goals.Models;

namespace Goals.Services;

public class GoalRepository(SqliteConnection connection)
{
    public async Task<List<DailyGoal>> GetDailyGoalsAsync()
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, CategoryId, TotalTarget, ExcludeDayOfWeek FROM DailyGoals";

        var goals = new List<DailyGoal>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            goals.Add(new DailyGoal
            {
                Id = reader.GetInt32(0),
                CategoryId = reader.GetInt32(1),
                TotalTarget = TimeSpan.Parse(reader.GetString(2)),
                ExcludedDays = ParseExcludedDays(reader.IsDBNull(3) ? null : reader.GetString(3))
            });
        }
        return goals;
    }

    public async Task<List<WeeklyGoal>> GetWeeklyGoalsAsync()
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, CategoryId, TotalTarget FROM WeeklyGoals";

        var goals = new List<WeeklyGoal>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            goals.Add(new WeeklyGoal
            {
                Id = reader.GetInt32(0),
                CategoryId = reader.GetInt32(1),
                TotalTarget = TimeSpan.Parse(reader.GetString(2))
            });
        }
        return goals;
    }

    public async Task AddDailyGoalAsync(DailyGoal goal)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO DailyGoals (CategoryId, TotalTarget, ExcludeDayOfWeek)
            VALUES (@CategoryId, @TotalTarget, @ExcludeDayOfWeek)
            """;
        cmd.Parameters.AddWithValue("@CategoryId", goal.CategoryId);
        cmd.Parameters.AddWithValue("@TotalTarget", goal.TotalTarget.ToString());
        cmd.Parameters.AddWithValue("@ExcludeDayOfWeek",
            goal.ExcludedDays.Count > 0
                ? string.Join(",", goal.ExcludedDays)
                : DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task AddWeeklyGoalAsync(WeeklyGoal goal)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO WeeklyGoals (CategoryId, TotalTarget)
            VALUES (@CategoryId, @TotalTarget)
            """;
        cmd.Parameters.AddWithValue("@CategoryId", goal.CategoryId);
        cmd.Parameters.AddWithValue("@TotalTarget", goal.TotalTarget.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> RemoveDailyGoalAsync(int id)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM DailyGoals WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> RemoveWeeklyGoalAsync(int id)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM WeeklyGoals WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", id);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> HasAnyGoalsAsync()
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM (
                SELECT 1 FROM DailyGoals
                UNION ALL
                SELECT 1 FROM WeeklyGoals
            ) LIMIT 1
            """;
        var count = (long)(await cmd.ExecuteScalarAsync())!;
        return count > 0;
    }

    public async Task<string> GetCategoryNameAsync(int categoryId)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Name FROM Categories WHERE Id = @Id";
        cmd.Parameters.AddWithValue("@Id", categoryId);
        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString() ?? $"Category {categoryId}";
    }

    private static HashSet<DayOfWeek> ParseExcludedDays(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var days = new HashSet<DayOfWeek>();
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (Enum.TryParse<DayOfWeek>(part, out var day))
                days.Add(day);
        }
        return days;
    }
}
