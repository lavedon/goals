using Goals.Models;
using Goals.Services;
using Spectre.Console;

namespace Goals.Commands;

public static class ModifyGoalCommand
{
    public static async Task RunAsync(GoalRepository goalRepo, HabitRepository habitRepo,
        Func<int, string> getCategoryName)
    {
        var goalType = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Modify which type?")
                .AddChoices("Daily", "Weekly", "Habit"));

        if (goalType == "Daily")
            await ModifyDailyAsync(goalRepo, getCategoryName);
        else if (goalType == "Weekly")
            await ModifyWeeklyAsync(goalRepo, getCategoryName);
        else
            await ModifyHabitAsync(habitRepo);
    }

    private static async Task ModifyDailyAsync(GoalRepository goalRepo, Func<int, string> getCategoryName)
    {
        var goals = await goalRepo.GetDailyGoalsAsync();
        if (goals.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No daily goals to modify.[/]");
            return;
        }

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<DailyGoal>()
                .Title("Which daily goal?")
                .UseConverter(g =>
                {
                    var excluded = g.ExcludedDays.Count > 0 ? $" (excl: {string.Join(", ", g.ExcludedDays)})" : "";
                    return $"#{g.Id} {getCategoryName(g.CategoryId)} — {WeekCalculator.FormatDuration(g.TotalTarget)}/day{excluded}";
                })
                .AddChoices(goals));

        var field = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What to change?")
                .AddChoices("Target time", "Excluded days", "Category"));

        if (field == "Target time")
        {
            var input = AnsiConsole.Prompt(
                new TextPrompt<string>($"New target (current: [green]{WeekCalculator.FormatDuration(selected.TotalTarget)}[/]):")
                    .Validate(v => TimeSpan.TryParse(v, out _)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Enter a valid time like 8:00 or 0:30")));
            selected.TotalTarget = TimeSpan.Parse(input);
        }
        else if (field == "Excluded days")
        {
            var prompt = new MultiSelectionPrompt<DayOfWeek>()
                .Title("Select days to exclude (space to toggle, enter to confirm)")
                .NotRequired()
                .AddChoices(Enum.GetValues<DayOfWeek>());
            foreach (var day in selected.ExcludedDays)
                prompt.Select(day);
            var days = AnsiConsole.Prompt(prompt);
            selected.ExcludedDays = new HashSet<DayOfWeek>(days);
        }
        else
        {
            var category = AnsiConsole.Prompt(
                new SelectionPrompt<TaskCategories>()
                    .Title("New category:")
                    .AddChoices(Enum.GetValues<TaskCategories>()));
            selected.CategoryId = (int)category;
        }

        await goalRepo.UpdateDailyGoalAsync(selected);
        AnsiConsole.MarkupLine("[green]Daily goal updated.[/]");
    }

    private static async Task ModifyWeeklyAsync(GoalRepository goalRepo, Func<int, string> getCategoryName)
    {
        var goals = await goalRepo.GetWeeklyGoalsAsync();
        if (goals.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No weekly goals to modify.[/]");
            return;
        }

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<WeeklyGoal>()
                .Title("Which weekly goal?")
                .UseConverter(g => $"#{g.Id} {getCategoryName(g.CategoryId)} — {WeekCalculator.FormatDuration(g.TotalTarget)}/week")
                .AddChoices(goals));

        var field = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What to change?")
                .AddChoices("Target time", "Category"));

        if (field == "Target time")
        {
            var input = AnsiConsole.Prompt(
                new TextPrompt<string>($"New target (current: [green]{WeekCalculator.FormatDuration(selected.TotalTarget)}[/]):")
                    .Validate(v => TimeSpan.TryParse(v, out _)
                        ? ValidationResult.Success()
                        : ValidationResult.Error("Enter a valid time like 40:00 or 8:00")));
            selected.TotalTarget = TimeSpan.Parse(input);
        }
        else
        {
            var category = AnsiConsole.Prompt(
                new SelectionPrompt<TaskCategories>()
                    .Title("New category:")
                    .AddChoices(Enum.GetValues<TaskCategories>()));
            selected.CategoryId = (int)category;
        }

        await goalRepo.UpdateWeeklyGoalAsync(selected);
        AnsiConsole.MarkupLine("[green]Weekly goal updated.[/]");
    }

    private static async Task ModifyHabitAsync(HabitRepository habitRepo)
    {
        var habits = await habitRepo.GetHabitsAsync();
        if (habits.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No habits to modify.[/]");
            return;
        }

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<Habit>()
                .Title("Which habit?")
                .UseConverter(h => $"#{h.Id} {h.Name}")
                .AddChoices(habits));

        var newName = AnsiConsole.Prompt(
            new TextPrompt<string>($"New name (current: [green]{Markup.Escape(selected.Name)}[/]):"));

        await habitRepo.RenameHabitAsync(selected.Id, newName);
        AnsiConsole.MarkupLine($"[green]Renamed to:[/] {Markup.Escape(newName)}");
    }
}
