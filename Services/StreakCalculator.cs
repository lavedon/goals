using Microsoft.Data.Sqlite;
using Goals.Models;

namespace Goals.Services;

public class StreakCalculator(SqliteConnection connection, TimeEntryReader reader)
{
    private const int MaxDailyLookback = 365;
    private const int MaxWeeklyLookback = 52;

    public async Task<Dictionary<int, int>> ComputeDailyStreaksAsync(
        List<DailyGoal> goals, DateOnly today)
    {
        if (goals.Count == 0)
            return new Dictionary<int, int>();

        // Fetch all daily totals in one query for the lookback window
        var lookbackStart = today.AddDays(-MaxDailyLookback);
        var allTotals = await reader.GetDailyTotalsByCategoryRangeAsync(lookbackStart, today);

        var streaks = new Dictionary<int, int>();

        foreach (var goal in goals)
        {
            var streak = 0;
            // Start from yesterday (today is incomplete)
            var date = today.AddDays(-1);

            for (int i = 0; i < MaxDailyLookback; i++, date = date.AddDays(-1))
            {
                if (goal.ExcludedDays.Contains(date.DayOfWeek))
                    continue; // excluded days don't break or count

                allTotals.TryGetValue(date, out var dayTotals);
                dayTotals ??= new Dictionary<int, TimeSpan>();
                dayTotals.TryGetValue(goal.CategoryId, out var actual);

                if (actual >= goal.TotalTarget)
                    streak++;
                else
                    break; // streak broken
            }

            streaks[goal.Id] = streak;
            await SaveStreakAsync("Daily", goal.Id, streak, today);
        }

        return streaks;
    }

    public async Task<Dictionary<int, int>> ComputeWeeklyStreaksAsync(
        List<WeeklyGoal> goals, DateOnly today)
    {
        if (goals.Count == 0)
            return new Dictionary<int, int>();

        var streaks = new Dictionary<int, int>();

        foreach (var goal in goals)
        {
            var streak = 0;
            // Start from last completed week
            var weekStart = WeekCalculator.GetWeekStart(today).AddDays(-7);

            for (int i = 0; i < MaxWeeklyLookback; i++, weekStart = weekStart.AddDays(-7))
            {
                var weekTotals = await reader.GetWeekTotalsByCategoryAsync(weekStart);
                weekTotals.TryGetValue(goal.CategoryId, out var actual);

                if (actual >= goal.TotalTarget)
                    streak++;
                else
                    break;
            }

            streaks[goal.Id] = streak;
            await SaveStreakAsync("Weekly", goal.Id, streak, today);
        }

        return streaks;
    }

    private async Task SaveStreakAsync(string goalType, int goalId, int streak, DateOnly asOf)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO Streaks (GoalType, GoalId, CurrentStreak, LastUpdated)
            VALUES (@GoalType, @GoalId, @Streak, @LastUpdated)
            """;
        cmd.Parameters.AddWithValue("@GoalType", goalType);
        cmd.Parameters.AddWithValue("@GoalId", goalId);
        cmd.Parameters.AddWithValue("@Streak", streak);
        cmd.Parameters.AddWithValue("@LastUpdated", asOf.ToString("yyyy-MM-dd"));
        await cmd.ExecuteNonQueryAsync();
    }
}
