using Goals.Models;
using Goals.Services;
using Spectre.Console;

namespace Goals.Commands;

public static class AddGoalCommand
{
    public static async Task RunAsync(GoalRepository goalRepo)
    {
        var goalType = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What type of goal?")
                .AddChoices("Daily", "Weekly"));

        var category = AnsiConsole.Prompt(
            new SelectionPrompt<TaskCategories>()
                .Title("Which category?")
                .AddChoices(Enum.GetValues<TaskCategories>()));

        var targetInput = AnsiConsole.Prompt(
            new TextPrompt<string>("Target time (e.g. [green]8:00[/] for 8 hours, [green]0:30[/] for 30 minutes):")
                .Validate(input => TimeSpan.TryParse(input, out _)
                    ? ValidationResult.Success()
                    : ValidationResult.Error("Enter a valid time like 8:00 or 0:30")));
        var target = TimeSpan.Parse(targetInput);

        if (goalType == "Daily")
        {
            var excludedDays = AnsiConsole.Prompt(
                new MultiSelectionPrompt<DayOfWeek>()
                    .Title("Exclude any days? (space to select, enter to confirm)")
                    .NotRequired()
                    .AddChoices(Enum.GetValues<DayOfWeek>()));

            var goal = new DailyGoal
            {
                CategoryId = (int)category,
                TotalTarget = target,
                ExcludedDays = new HashSet<DayOfWeek>(excludedDays)
            };
            await goalRepo.AddDailyGoalAsync(goal);
            AnsiConsole.MarkupLine($"[green]Added daily goal:[/] {category} — {WeekCalculator.FormatDuration(target)}/day");
        }
        else
        {
            var goal = new WeeklyGoal
            {
                CategoryId = (int)category,
                TotalTarget = target
            };
            await goalRepo.AddWeeklyGoalAsync(goal);
            AnsiConsole.MarkupLine($"[green]Added weekly goal:[/] {category} — {WeekCalculator.FormatDuration(target)}/week");
        }
    }
}
