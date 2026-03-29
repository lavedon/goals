using Goals.Services;
using Spectre.Console;

namespace Goals.Commands;

public static class RemoveGoalCommand
{
    public static async Task RunAsync(GoalRepository goalRepo, Func<int, string> getCategoryName, List<string> extraArgs, bool plain)
    {
        // Parse: remove <daily|weekly> <id>  OR  remove <id> (tries both)
        string? goalType = null;
        int? id = null;

        foreach (var arg in extraArgs)
        {
            if (arg.Equals("daily", StringComparison.OrdinalIgnoreCase))
                goalType = "Daily";
            else if (arg.Equals("weekly", StringComparison.OrdinalIgnoreCase))
                goalType = "Weekly";
            else if (int.TryParse(arg, out var parsed))
                id = parsed;
        }

        // Interactive fallbacks
        if (goalType == null && id == null)
        {
            // No args at all — show all goals and prompt
            await ListGoalsCommand.RunAsync(goalRepo, getCategoryName, plain);
            Console.WriteLine();

            goalType = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Remove which type of goal?")
                    .AddChoices("Daily", "Weekly"));
        }

        if (id == null)
        {
            id = AnsiConsole.Prompt(
                new TextPrompt<int>("Goal ID to remove:"));
        }

        if (goalType == null)
        {
            // Only ID provided — try both tables
            var removed = await goalRepo.RemoveDailyGoalAsync(id.Value);
            if (!removed)
                removed = await goalRepo.RemoveWeeklyGoalAsync(id.Value);

            if (removed)
                PrintSuccess($"Removed goal #{id.Value}.");
            else
                PrintError($"No goal found with ID {id.Value}. Run 'goals list' to see IDs.");
        }
        else
        {
            var removed = goalType == "Daily"
                ? await goalRepo.RemoveDailyGoalAsync(id.Value)
                : await goalRepo.RemoveWeeklyGoalAsync(id.Value);

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
