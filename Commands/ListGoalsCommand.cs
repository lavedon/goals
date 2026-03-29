using Goals.Services;
using Spectre.Console;

namespace Goals.Commands;

public static class ListGoalsCommand
{
    public static async Task RunAsync(GoalRepository goalRepo, Func<int, string> getCategoryName, bool plain)
    {
        var dailyGoals = await goalRepo.GetDailyGoalsAsync();
        var weeklyGoals = await goalRepo.GetWeeklyGoalsAsync();

        if (dailyGoals.Count == 0 && weeklyGoals.Count == 0)
        {
            if (plain)
                Console.WriteLine("No goals configured. Run 'goals add' to create one.");
            else
                AnsiConsole.MarkupLine("[yellow]No goals configured. Run [bold]goals add[/] to create one.[/]");
            return;
        }

        if (plain)
        {
            Console.WriteLine("Type     ID  Category  Target   Excluded Days");
            Console.WriteLine("-------  --  --------  -------  ----------------");
            foreach (var g in dailyGoals)
            {
                var excluded = g.ExcludedDays.Count > 0 ? string.Join(", ", g.ExcludedDays) : "-";
                Console.WriteLine($"Daily    {g.Id,-3} {getCategoryName(g.CategoryId),-9} {WeekCalculator.FormatDuration(g.TotalTarget),-8} {excluded}");
            }
            foreach (var g in weeklyGoals)
            {
                Console.WriteLine($"Weekly   {g.Id,-3} {getCategoryName(g.CategoryId),-9} {WeekCalculator.FormatDuration(g.TotalTarget),-8} -");
            }
        }
        else
        {
            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Type");
            table.AddColumn("ID");
            table.AddColumn("Category");
            table.AddColumn("Target");
            table.AddColumn("Excluded Days");

            foreach (var g in dailyGoals)
            {
                table.AddRow(
                    "Daily",
                    g.Id.ToString(),
                    getCategoryName(g.CategoryId),
                    WeekCalculator.FormatDuration(g.TotalTarget),
                    g.ExcludedDays.Count > 0 ? string.Join(", ", g.ExcludedDays) : "—");
            }

            foreach (var g in weeklyGoals)
            {
                table.AddRow(
                    "Weekly",
                    g.Id.ToString(),
                    getCategoryName(g.CategoryId),
                    WeekCalculator.FormatDuration(g.TotalTarget),
                    "—");
            }

            AnsiConsole.Write(table);
        }
    }
}
