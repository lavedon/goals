using Microsoft.Data.Sqlite;

namespace Goals.Services;

public class TimeEntryReader(SqliteConnection connection)
{
    public async Task<Dictionary<int, TimeSpan>> GetDailyTotalsByCategoryAsync(DateOnly date)
    {
        var start = date.ToString("yyyy-MM-dd");
        var end = date.AddDays(1).ToString("yyyy-MM-dd");

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT CategoryId,
                   SUM((julianday(EndTimestamp) - julianday(StartTimestamp)) * 86400) as TotalSeconds
            FROM TimeEntries
            WHERE StartTimestamp >= @Start AND StartTimestamp < @End
              AND CategoryId IS NOT NULL
            GROUP BY CategoryId
            """;
        cmd.Parameters.AddWithValue("@Start", start);
        cmd.Parameters.AddWithValue("@End", end);

        return await ReadTotalsAsync(cmd);
    }

    public async Task<Dictionary<int, TimeSpan>> GetWeekTotalsByCategoryAsync(DateOnly weekStart)
    {
        var start = weekStart.ToString("yyyy-MM-dd");
        var end = weekStart.AddDays(7).ToString("yyyy-MM-dd");

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT CategoryId,
                   SUM((julianday(EndTimestamp) - julianday(StartTimestamp)) * 86400) as TotalSeconds
            FROM TimeEntries
            WHERE StartTimestamp >= @Start AND StartTimestamp < @End
              AND CategoryId IS NOT NULL
            GROUP BY CategoryId
            """;
        cmd.Parameters.AddWithValue("@Start", start);
        cmd.Parameters.AddWithValue("@End", end);

        return await ReadTotalsAsync(cmd);
    }

    public async Task<Dictionary<DateOnly, Dictionary<int, TimeSpan>>> GetDailyTotalsByCategoryRangeAsync(
        DateOnly startInclusive, DateOnly endExclusive)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT DATE(StartTimestamp) as EntryDate, CategoryId,
                   SUM((julianday(EndTimestamp) - julianday(StartTimestamp)) * 86400) as TotalSeconds
            FROM TimeEntries
            WHERE StartTimestamp >= @Start AND StartTimestamp < @End
              AND CategoryId IS NOT NULL
            GROUP BY EntryDate, CategoryId
            """;
        cmd.Parameters.AddWithValue("@Start", startInclusive.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@End", endExclusive.ToString("yyyy-MM-dd"));

        var result = new Dictionary<DateOnly, Dictionary<int, TimeSpan>>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(0) || reader.IsDBNull(1)) continue;
            var date = DateOnly.Parse(reader.GetString(0));
            var categoryId = reader.GetInt32(1);
            var totalSeconds = reader.GetDouble(2);

            if (!result.TryGetValue(date, out var dayTotals))
            {
                dayTotals = new Dictionary<int, TimeSpan>();
                result[date] = dayTotals;
            }
            dayTotals[categoryId] = TimeSpan.FromSeconds(totalSeconds);
        }
        return result;
    }

    private static async Task<Dictionary<int, TimeSpan>> ReadTotalsAsync(SqliteCommand cmd)
    {
        var totals = new Dictionary<int, TimeSpan>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(0)) continue;
            var categoryId = reader.GetInt32(0);
            var totalSeconds = reader.GetDouble(1);
            totals[categoryId] = TimeSpan.FromSeconds(totalSeconds);
        }
        return totals;
    }
}
