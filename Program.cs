using Goals.Commands;
using Goals.Models;
using Goals.Services;
using Spectre.Console;

bool plain = false;
bool interactive = false;
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

    if (arg == "interactive")
    {
        interactive = true;
        continue;
    }

    if (arg.StartsWith("db="))
    {
        dbPath = args[i].Substring(args[i].IndexOf('=') + 1);
        continue;
    }

    if (arg is "report" or "add" or "list" or "remove" or "modify" or "check" or "uncheck")
    {
        command = arg;
        continue;
    }

    // Collect extra positional args (e.g. IDs, habit names)
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
var habitRepo = new HabitRepository(db.Connection);

// Cache category names for display
var categoryNames = new Dictionary<int, string>();
foreach (var cat in Enum.GetValues<TaskCategories>())
    categoryNames[(int)cat] = await goalRepo.GetCategoryNameAsync((int)cat);
string GetCategoryName(int id) => categoryNames.TryGetValue(id, out var name) ? name : $"Category {id}";

if (interactive)
{
    await RunInteractiveAsync();
    return 0;
}

if (plain)
    Console.WriteLine("=== Goal Tracker ===");
else
{
    AnsiConsole.Write(new Rule("[bold blue]Goal Tracker[/]").RuleStyle("blue"));
    AnsiConsole.WriteLine();
}

await RunCommandAsync(command);
return 0;

// ── Command dispatch ────────────────────────────────────────────────

async Task RunCommandAsync(string cmd)
{
    switch (cmd)
    {
        case "report":
            await ReportCommand.RunAsync(goalRepo, reader, streakCalc, habitRepo, GetCategoryName, plain);
            break;
        case "add":
            await AddGoalCommand.RunAsync(goalRepo, habitRepo);
            break;
        case "list":
            await ListGoalsCommand.RunAsync(goalRepo, habitRepo, GetCategoryName, plain);
            break;
        case "remove":
            await RemoveGoalCommand.RunAsync(goalRepo, habitRepo, GetCategoryName, extraArgs, plain);
            break;
        case "modify":
            await ModifyGoalCommand.RunAsync(goalRepo, habitRepo, GetCategoryName);
            break;
        case "check":
            await RunCheckAsync(habitRepo, extraArgs, @checked: true);
            break;
        case "uncheck":
            await RunCheckAsync(habitRepo, extraArgs, @checked: false);
            break;
    }
}

// ── Interactive mode ────────────────────────────────────────────────

async Task RunInteractiveAsync()
{
    while (true)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new Rule("[bold blue]Goal Tracker[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();

        await ReportCommand.RunAsync(goalRepo, reader, streakCalc, habitRepo, GetCategoryName, plain: false);

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold blue]What would you like to do?[/]")
                .AddChoices(
                    "Check Habit",
                    "Uncheck Habit",
                    "Add Goal/Habit",
                    "Modify Goal/Habit",
                    "Remove Goal/Habit",
                    "List All",
                    "Exit"));

        if (choice == "Exit")
            return;

        AnsiConsole.WriteLine();

        switch (choice)
        {
            case "Check Habit":
                await RunCheckAsync(habitRepo, [], @checked: true);
                break;
            case "Uncheck Habit":
                await RunCheckAsync(habitRepo, [], @checked: false);
                break;
            case "Add Goal/Habit":
                await AddGoalCommand.RunAsync(goalRepo, habitRepo);
                break;
            case "Modify Goal/Habit":
                await ModifyGoalCommand.RunAsync(goalRepo, habitRepo, GetCategoryName);
                break;
            case "Remove Goal/Habit":
                await RemoveGoalCommand.RunAsync(goalRepo, habitRepo, GetCategoryName, [], plain: false);
                break;
            case "List All":
                await ListGoalsCommand.RunAsync(goalRepo, habitRepo, GetCategoryName, plain: false);
                break;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press any key to continue...[/]");
        Console.ReadKey(true);
    }
}

// ── Check/Uncheck ───────────────────────────────────────────────────

async Task RunCheckAsync(HabitRepository repo, List<string> checkArgs, bool @checked)
{
    var today = DateOnly.FromDateTime(DateTime.Now);
    var input = checkArgs.FirstOrDefault();

    Habit? habit = null;
    if (input != null)
    {
        habit = await repo.GetHabitByNameOrIdAsync(input);
        if (habit == null)
        {
            PrintError($"Habit not found: {input}");
            var habits = await repo.GetHabitsAsync();
            if (habits.Count > 0)
            {
                if (plain)
                {
                    Console.WriteLine("Available habits:");
                    foreach (var h in habits)
                        Console.WriteLine($"  {h.Id}: {h.Name}");
                }
                else
                {
                    AnsiConsole.MarkupLine("[bold]Available habits:[/]");
                    foreach (var h in habits)
                        AnsiConsole.MarkupLine($"  [green]{h.Id}[/]: {Markup.Escape(h.Name)}");
                }
            }
            return;
        }
    }
    else
    {
        // Interactive — pick from list
        var habits = await repo.GetHabitsAsync();
        if (habits.Count == 0)
        {
            if (plain)
                Console.WriteLine("No habits configured. Run 'goals add' to create one.");
            else
                AnsiConsole.MarkupLine("[yellow]No habits configured. Run [bold]goals add[/] to create one.[/]");
            return;
        }

        var todayChecked = await repo.GetCheckedForDateAsync(today);
        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<Habit>()
                .Title(@checked ? "Which habit to check off?" : "Which habit to uncheck?")
                .UseConverter(h =>
                {
                    var mark = todayChecked.ContainsKey(h.Id) ? "[green]✓[/] " : "  ";
                    return $"{mark}{h.Name}";
                })
                .AddChoices(habits));
        habit = selected;
    }

    if (@checked)
    {
        await repo.CheckAsync(habit.Id, today);
        if (plain)
            Console.WriteLine($"Checked: {habit.Name}");
        else
            AnsiConsole.MarkupLine($"[green]✓[/] {Markup.Escape(habit.Name)}");
    }
    else
    {
        await repo.UncheckAsync(habit.Id, today);
        if (plain)
            Console.WriteLine($"Unchecked: {habit.Name}");
        else
            AnsiConsole.MarkupLine($"[grey]✗[/] {Markup.Escape(habit.Name)}");
    }
}

// ── Helpers ──────────────────────────────────────────────────────────

void PrintUsage()
{
    if (plain)
    {
        Console.WriteLine("goals - Track daily and weekly time goals");
        Console.WriteLine();
        Console.WriteLine("Usage: goals [command] [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  report          Show goal status (default)");
        Console.WriteLine("  add             Add a new goal or habit interactively");
        Console.WriteLine("  list            List all configured goals and habits");
        Console.WriteLine("  remove          Remove a goal or habit by ID");
        Console.WriteLine("  modify          Modify an existing goal or habit");
        Console.WriteLine("  check <name>    Mark a habit as done for today");
        Console.WriteLine("  uncheck <name>  Unmark a habit for today");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --plain         Plain text output (no colors)");
        Console.WriteLine("  --interactive   Interactive menu mode");
        Console.WriteLine("  --db=<path>     Path to timetracker.db");
        Console.WriteLine("  --help, -h      Show this help");
    }
    else
    {
        var panel = new Panel(
            new Rows(
                new Markup("[bold]Commands:[/]"),
                new Markup("  [green]report[/]              Show goal status (default)"),
                new Markup("  [green]add[/]                 Add a new goal or habit interactively"),
                new Markup("  [green]list[/]                List all configured goals and habits"),
                new Markup("  [green]remove[/]              Remove a goal or habit by ID"),
                new Markup("  [green]modify[/]              Modify an existing goal or habit"),
                new Markup("  [green]check <name|id>[/]     Mark a habit as done for today"),
                new Markup("  [green]uncheck <name|id>[/]   Unmark a habit for today"),
                new Markup(""),
                new Markup("[bold]Options:[/]"),
                new Markup("  [green]--plain[/]         Plain text output (no colors)"),
                new Markup("  [green]--interactive[/]   Interactive menu mode"),
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
