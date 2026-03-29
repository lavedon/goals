using Goals.Commands;
using Goals.Models;
using Goals.Services;
using Spectre.Console;

bool plain = false;
string? command = null;
string? dbPath = null;
var extraArgs = new List<string>();

// Parse arguments
for (int i = 0; i < args.Length; i++)
{
    var arg = args[i].TrimStart('-').ToLowerInvariant();

    if (arg is "help" or "h" or "?")
    {
        PrintUsage();
        return 0;
    }

    if (arg == "plain")
    {
        plain = true;
        continue;
    }

    if (arg.StartsWith("db="))
    {
        dbPath = args[i].Substring(args[i].IndexOf('=') + 1);
        continue;
    }

    if (arg is "report" or "add" or "list" or "remove")
    {
        command = arg;
        continue;
    }

    // Collect extra positional args (e.g. IDs for remove)
    if (command != null)
    {
        extraArgs.Add(args[i]);
        continue;
    }

    PrintError($"Unknown argument: {args[i]}");
    PrintUsage();
    return 1;
}

command ??= "report";
dbPath ??= Path.Combine(AppContext.BaseDirectory, "Data", "timetracker.db");

if (!File.Exists(dbPath))
{
    PrintError($"Database not found: {dbPath}");
    Console.WriteLine("Run the timeTracker app first, or specify --db=path");
    return 1;
}

using var db = new Database(dbPath);
await db.InitializeAsync();

var goalRepo = new GoalRepository(db.Connection);
var reader = new TimeEntryReader(db.Connection);
var streakCalc = new StreakCalculator(db.Connection, reader);

// Cache category names for display
var categoryNames = new Dictionary<int, string>();
foreach (var cat in Enum.GetValues<TaskCategories>())
    categoryNames[(int)cat] = await goalRepo.GetCategoryNameAsync((int)cat);
string GetCategoryName(int id) => categoryNames.TryGetValue(id, out var name) ? name : $"Category {id}";

if (plain)
    Console.WriteLine("=== Goal Tracker ===");
else
{
    AnsiConsole.Write(new Rule("[bold blue]Goal Tracker[/]").RuleStyle("blue"));
    AnsiConsole.WriteLine();
}

switch (command)
{
    case "report":
        await ReportCommand.RunAsync(goalRepo, reader, streakCalc, GetCategoryName, plain);
        break;
    case "add":
        await AddGoalCommand.RunAsync(goalRepo);
        break;
    case "list":
        await ListGoalsCommand.RunAsync(goalRepo, GetCategoryName, plain);
        break;
    case "remove":
        await RemoveGoalCommand.RunAsync(goalRepo, GetCategoryName, extraArgs, plain);
        break;
}

return 0;

// ── Helpers ───────────────────────────────��──────────────────────────

void PrintUsage()
{
    if (plain)
    {
        Console.WriteLine("goals - Track daily and weekly time goals");
        Console.WriteLine();
        Console.WriteLine("Usage: goals [command] [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  report    Show goal status (default)");
        Console.WriteLine("  add       Add a new goal interactively");
        Console.WriteLine("  list      List all configured goals");
        Console.WriteLine("  remove    Remove a goal by ID");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --plain       Plain text output (no colors)");
        Console.WriteLine("  --db=<path>   Path to timetracker.db");
        Console.WriteLine("  --help, -h    Show this help");
    }
    else
    {
        var panel = new Panel(
            new Rows(
                new Markup("[bold]Commands:[/]"),
                new Markup("  [green]report[/]          Show goal status (default)"),
                new Markup("  [green]add[/]             Add a new goal interactively"),
                new Markup("  [green]list[/]            List all configured goals"),
                new Markup("  [green]remove[/]          Remove a goal by ID"),
                new Markup(""),
                new Markup("[bold]Options:[/]"),
                new Markup("  [green]--plain[/]         Plain text output (no colors)"),
                new Markup("  [green]--db=<path>[/]     Path to timetracker.db"),
                new Markup("  [green]--help[/], [green]-h[/]      Show this help")
            ))
            .Header("[bold blue]goals[/]")
            .Border(BoxBorder.Rounded)
            .Padding(1, 0);

        AnsiConsole.Write(panel);
    }
}

void PrintError(string message)
{
    if (plain)
        Console.Error.WriteLine($"ERROR: {message}");
    else
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
}
