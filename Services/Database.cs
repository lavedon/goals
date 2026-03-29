using Microsoft.Data.Sqlite;

namespace Goals.Services;

public class Database : IDisposable
{
    private readonly SqliteConnection _connection;

    public Database(string dbPath)
    {
        var directory = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
    }

    public SqliteConnection Connection => _connection;

    public async Task InitializeAsync()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Categories (
                Id INTEGER PRIMARY KEY,
                Name TEXT NOT NULL UNIQUE
            );
            """;
        await cmd.ExecuteNonQueryAsync();

        await SeedCategoriesAsync();

        await using var cmd2 = _connection.CreateCommand();
        cmd2.CommandText = """
            CREATE TABLE IF NOT EXISTS WeeklyGoals (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CategoryId INTEGER NOT NULL,
                TotalTarget TEXT NOT NULL,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
            );
            """;
        await cmd2.ExecuteNonQueryAsync();

        await using var cmd3 = _connection.CreateCommand();
        cmd3.CommandText = """
            CREATE TABLE IF NOT EXISTS DailyGoals (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CategoryId INTEGER NOT NULL,
                TotalTarget TEXT NOT NULL,
                ExcludeDayOfWeek TEXT,
                FOREIGN KEY (CategoryId) REFERENCES Categories(Id)
            );
            """;
        await cmd3.ExecuteNonQueryAsync();

        await using var cmd4 = _connection.CreateCommand();
        cmd4.CommandText = """
            CREATE TABLE IF NOT EXISTS Streaks (
                GoalType TEXT NOT NULL,
                GoalId INTEGER NOT NULL,
                CurrentStreak INTEGER NOT NULL DEFAULT 0,
                LastUpdated TEXT NOT NULL,
                PRIMARY KEY (GoalType, GoalId)
            );
            """;
        await cmd4.ExecuteNonQueryAsync();
    }

    private async Task SeedCategoriesAsync()
    {
        var categories = new (int Id, string Name)[]
        {
            (0, "Job"),
            (1, "LCReview"),
            (2, "LCNew"),
            (3, "Anki"),
            (4, "Okta")
        };

        foreach (var (id, name) in categories)
        {
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = "INSERT OR IGNORE INTO Categories (Id, Name) VALUES (@Id, @Name)";
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.Parameters.AddWithValue("@Name", name);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
