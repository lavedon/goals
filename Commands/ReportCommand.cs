using Goals.Models;
using Goals.Services;
using Spectre.Console;

namespace Goals.Commands;

public static class ReportCommand
{
    private static readonly Dictionary<int, string> CategoryColors = new()
    {
        [(int)TaskCategories.Job] = "green",
        [(int)TaskCategories.LCReview] = "red",
        [(int)TaskCategories.LCNew] = "cyan",
        [(int)TaskCategories.Anki] = "yellow",
        [(int)TaskCategories.Okta] = "magenta",
    };

    public static async Task RunAsync(GoalRepository goalRepo, TimeEntryReader reader,
        StreakCalculator streakCalc, HabitRepository habitRepo,
        Func<int, string> getCategoryName, bool plain)
    {
        var dailyGoals = await goalRepo.GetDailyGoalsAsync();
        var weeklyGoals = await goalRepo.GetWeeklyGoalsAsync();
        var habits = await habitRepo.GetHabitsAsync();

        if (dailyGoals.Count == 0 && weeklyGoals.Count == 0 && habits.Count == 0)
        {
            if (plain)
                Console.WriteLine("No goals configured. Run 'goals add' to create one.");
            else
                AnsiConsole.MarkupLine("[yellow]No goals configured. Run [bold]goals add[/] to create one.[/]");
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var yesterday = today.AddDays(-1);
        var thisWeekStart = WeekCalculator.GetWeekStart(today);
        var lastWeekStart = thisWeekStart.AddDays(-7);

        var todayTotals = await reader.GetDailyTotalsByCategoryAsync(today);
        var yesterdayTotals = await reader.GetDailyTotalsByCategoryAsync(yesterday);
        var thisWeekTotals = await reader.GetWeekTotalsByCategoryAsync(thisWeekStart);
        var lastWeekTotals = await reader.GetWeekTotalsByCategoryAsync(lastWeekStart);

        // Compute streaks
        var dailyStreaks = await streakCalc.ComputeDailyStreaksAsync(dailyGoals, today);
        var weeklyStreaks = await streakCalc.ComputeWeeklyStreaksAsync(weeklyGoals, today);

        // Daily goals
        if (dailyGoals.Count > 0)
        {
            var todayStatuses = GoalAnalyzer.EvaluateDailyGoals(dailyGoals, todayTotals, today, getCategoryName);
            var yesterdayStatuses = GoalAnalyzer.EvaluateDailyGoals(dailyGoals, yesterdayTotals, yesterday, getCategoryName);

            if (plain)
            {
                Console.WriteLine("--- Daily Goals ---");
                PrintPlainRow("Today", todayStatuses);
                PrintPlainRow("Yesterday", yesterdayStatuses);
                PrintPlainStreakRow(dailyGoals.Select(g => (g.Id, getCategoryName(g.CategoryId))).ToList(), dailyStreaks, "d");
                Console.WriteLine();
            }
            else
            {
                AnsiConsole.Write(new Rule("[bold blue]Daily Goals[/]").RuleStyle("blue"));
                AnsiConsole.WriteLine();

                var dailyTable = new Table().Border(TableBorder.Rounded);
                dailyTable.AddColumn("Metric");

                foreach (var goal in dailyGoals)
                {
                    var color = GetColor(goal.CategoryId);
                    var name = getCategoryName(goal.CategoryId);
                    dailyTable.AddColumn(new TableColumn($"[bold {color}]{name}[/]").Centered());
                }

                AddDailyRow(dailyTable, "[bold]Today[/]", todayStatuses);
                AddDailyRow(dailyTable, "Yesterday", yesterdayStatuses);
                AddStreakRow(dailyTable, dailyGoals.Select(g => g.Id).ToList(), dailyStreaks, "d");

                AnsiConsole.Write(dailyTable);
                AnsiConsole.WriteLine();
            }
        }

        // Weekly goals
        if (weeklyGoals.Count > 0)
        {
            var thisWeekStatuses = GoalAnalyzer.EvaluateWeeklyGoals(weeklyGoals, thisWeekTotals, getCategoryName);
            var lastWeekStatuses = GoalAnalyzer.EvaluateWeeklyGoals(weeklyGoals, lastWeekTotals, getCategoryName);

            if (plain)
            {
                Console.WriteLine($"--- Weekly Goals (Week of {thisWeekStart:MMM dd}) ---");
                PrintPlainRow("This Week", thisWeekStatuses);
                PrintPlainRow($"Last Week (Mon {lastWeekStart:MMM dd})", lastWeekStatuses);
                PrintPlainStreakRow(weeklyGoals.Select(g => (g.Id, getCategoryName(g.CategoryId))).ToList(), weeklyStreaks, "w");
                Console.WriteLine();
            }
            else
            {
                AnsiConsole.Write(new Rule($"[bold blue]Weekly Goals — Week of {thisWeekStart:MMM dd}[/]").RuleStyle("blue"));
                AnsiConsole.WriteLine();

                var weeklyTable = new Table().Border(TableBorder.Rounded);
                weeklyTable.AddColumn("Metric");

                foreach (var goal in weeklyGoals)
                {
                    var color = GetColor(goal.CategoryId);
                    var name = getCategoryName(goal.CategoryId);
                    weeklyTable.AddColumn(new TableColumn($"[bold {color}]{name}[/]").Centered());
                }

                AddWeeklyRow(weeklyTable, "[bold]This Week[/]", thisWeekStatuses);
                AddWeeklyRow(weeklyTable, $"Last Week [grey](Mon {lastWeekStart:MMM dd})[/]", lastWeekStatuses);
                AddStreakRow(weeklyTable, weeklyGoals.Select(g => g.Id).ToList(), weeklyStreaks, "w");

                AnsiConsole.Write(weeklyTable);
                AnsiConsole.WriteLine();
            }
        }

        // Habits
        if (habits.Count > 0)
        {
            var todayChecked = await habitRepo.GetCheckedForDateAsync(today);
            var yesterdayChecked = await habitRepo.GetCheckedForDateAsync(yesterday);
            var habitStreaks = await habitRepo.ComputeStreaksAsync(habits, today);

            if (plain)
            {
                Console.WriteLine("--- Habits ---");
                foreach (var h in habits)
                {
                    var todayStatus = todayChecked.ContainsKey(h.Id) ? "DONE" : "    ";
                    var yestStatus = yesterdayChecked.ContainsKey(h.Id) ? "DONE" : "    ";
                    var streak = habitStreaks.GetValueOrDefault(h.Id, 0);
                    Console.WriteLine($"  {h.Name}: Today=[{todayStatus}] Yesterday=[{yestStatus}] Streak={streak}d");
                }
                Console.WriteLine();
            }
            else
            {
                AnsiConsole.Write(new Rule("[bold blue]Habits[/]").RuleStyle("blue"));
                AnsiConsole.WriteLine();

                var habitTable = new Table().Border(TableBorder.Rounded);
                habitTable.AddColumn("Habit");
                habitTable.AddColumn(new TableColumn("[bold]Today[/]").Centered());
                habitTable.AddColumn(new TableColumn("[bold]Yesterday[/]").Centered());
                habitTable.AddColumn(new TableColumn("[bold]Streak[/]").Centered());

                foreach (var h in habits)
                {
                    var todayMark = todayChecked.ContainsKey(h.Id)
                        ? "[green]✓[/]" : "[grey]—[/]";
                    var yestMark = yesterdayChecked.ContainsKey(h.Id)
                        ? "[green]✓[/]" : "[grey]—[/]";
                    var streak = habitStreaks.GetValueOrDefault(h.Id, 0);
                    var streakText = streak > 0
                        ? $"[bold orange1]{streak}d[/]"
                        : "[grey]0d[/]";

                    habitTable.AddRow(
                        $"[bold]{Markup.Escape(h.Name)}[/]",
                        todayMark,
                        yestMark,
                        streakText);
                }

                AnsiConsole.Write(habitTable);
                AnsiConsole.WriteLine();
            }
        }
    }

    private static void PrintPlainRow(string label, List<GoalStatus> statuses)
    {
        Console.WriteLine($"  {label}:");
        foreach (var s in statuses)
        {
            if (s.IsExcluded)
            {
                Console.WriteLine($"    {s.CategoryName}: Excluded");
            }
            else
            {
                var status = s.IsMet ? "MET" : $"{s.PercentComplete:F0}%";
                var remaining = s.IsMet ? "" : $" ({WeekCalculator.FormatDuration(s.Remaining)} left)";
                Console.WriteLine($"    {s.CategoryName}: {WeekCalculator.FormatDuration(s.Actual)} / {WeekCalculator.FormatDuration(s.Target)} [{status}]{remaining}");
            }
        }
    }

    private static void PrintPlainStreakRow(List<(int Id, string Name)> goals, Dictionary<int, int> streaks, string suffix)
    {
        Console.WriteLine("  Streak:");
        foreach (var (id, name) in goals)
        {
            var streak = streaks.GetValueOrDefault(id, 0);
            Console.WriteLine($"    {name}: {streak}{suffix}");
        }
    }

    private static void AddDailyRow(Table table, string label, List<GoalStatus> statuses)
    {
        var cells = new List<string> { label };
        foreach (var s in statuses)
        {
            var color = GetColor(s.CategoryId);
            if (s.IsExcluded)
            {
                cells.Add("[grey]— Excluded —[/]");
            }
            else
            {
                var status = s.IsMet ? "[green]✓[/]" : $"[yellow]{s.PercentComplete:F0}%[/]";
                cells.Add($"[{color}]{WeekCalculator.FormatDuration(s.Actual)}[/] / {WeekCalculator.FormatDuration(s.Target)} {status}");
            }
        }
        table.AddRow(cells.ToArray());
    }

    private static void AddWeeklyRow(Table table, string label, List<GoalStatus> statuses)
    {
        var cells = new List<string> { label };
        foreach (var s in statuses)
        {
            var color = GetColor(s.CategoryId);
            var status = s.IsMet ? "[green]✓[/]" : $"[yellow]{s.PercentComplete:F0}%[/]";
            var remaining = s.IsMet ? "" : $" [grey]({WeekCalculator.FormatDuration(s.Remaining)} left)[/]";
            cells.Add($"[{color}]{WeekCalculator.FormatDuration(s.Actual)}[/] / {WeekCalculator.FormatDuration(s.Target)} {status}{remaining}");
        }
        table.AddRow(cells.ToArray());
    }

    private static void AddStreakRow(Table table, List<int> goalIds, Dictionary<int, int> streaks, string suffix)
    {
        var cells = new List<string> { "[bold]Streak[/]" };
        foreach (var id in goalIds)
        {
            var streak = streaks.GetValueOrDefault(id, 0);
            var text = streak > 0
                ? $"[bold orange1]{streak}{suffix}[/]"
                : $"[grey]0{suffix}[/]";
            cells.Add(text);
        }
        table.AddRow(cells.ToArray());
    }

    private static string GetColor(int categoryId) =>
        CategoryColors.TryGetValue(categoryId, out var color) ? color : "white";
}
