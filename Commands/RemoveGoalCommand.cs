using Goals.Services;
using Spectre.Console;

namespace Goals.Commands;

public static class RemoveGoalCommand
{
    public static async Task RunAsync(GoalRepository goalRepo, HabitRepository habitRepo,
        Func<int, string> getCategoryName, List<string> extraArgs, bool plain)
    {
        // Parse: remove <daily|weekly|habit> <id>  OR  remove <id> (tries all)
        string? goalType = null;
        int? id = null;

        foreach (var arg in extraArgs)
        {
            if (arg.Equals("daily", StringComparison.OrdinalIgnoreCase))
                goalType = "Daily";
            else if (arg.Equals("weekly", StringComparison.OrdinalIgnoreCase))
                goalType = "Weekly";
            else if (arg.Equals("habit", StringComparison.OrdinalIgnoreCase))
                goalType = "Habit";
            else if (int.TryParse(arg, out var parsed))
                id = parsed;
        }

        // Interactive fallbacks
        if (goalType == null && id == null)
        {
            await ListGoalsCommand.RunAsync(goalRepo, habitRepo, getCategoryName, plain);
            Console.WriteLine();

            goalType = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Remove which type of goal?")
                    .AddChoices("Daily", "Weekly", "Habit"));
        }

        if (id == null)
        {
            id = AnsiConsole.Prompt(
                new TextPrompt<int>("Goal ID to remove:"));
        }

        if (goalType == null)
        {
            // Only ID provided — try all tables
            var removed = await goalRepo.RemoveDailyGoalAsync(id.Value);
            if (!removed)
                removed = await goalRepo.RemoveWeeklyGoalAsync(id.Value);
            if (!removed)
                removed = await habitRepo.RemoveHabitAsync(id.Value);

            if (removed)
                PrintSuccess($"Removed goal #{id.Value}.");
            else
                PrintError($"No goal found with ID {id.Value}. Run 'goals list' to see IDs.");
        }
        else
        {
            var removed = goalType switch
            {
                "Daily" => await goalRepo.RemoveDailyGoalAsync(id.Value),
                "Weekly" => await goalRepo.RemoveWeeklyGoalAsync(id.Value),
                "Habit" => await habitRepo.RemoveHabitAsync(id.Value),
                _ => false
            };

            if (removed)
                PrintSuccess($"Removed {goalType.ToLower()} goal #{id.Value}.");
            else
                PrintError($"No {goalType.ToLower()} goal found with ID {id.Value}.");
        }

        void PrintSuccess(string msg)
        {
            if (plain) Console.WriteLine(msg);
            else AnsiConsole.MarkupLine($"[green]{Markup.Escape(msg)}[/]");
        }

        void PrintError(string msg)
        {
            if (plain) Console.Error.WriteLine($"ERROR: {msg}");
            else AnsiConsole.MarkupLine($"[red]{Markup.Escape(msg)}[/]");
        }
    }
}
