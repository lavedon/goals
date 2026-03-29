using Goals.Models;

namespace Goals.Services;

public record GoalStatus(
    string GoalType,
    string CategoryName,
    int CategoryId,
    TimeSpan Target,
    TimeSpan Actual,
    TimeSpan Remaining,
    bool IsMet,
    bool IsExcluded,
    double PercentComplete
);

public static class GoalAnalyzer
{
    public static List<GoalStatus> EvaluateDailyGoals(
        List<DailyGoal> goals,
        Dictionary<int, TimeSpan> actuals,
        DateOnly today,
        Func<int, string> getCategoryName)
    {
        var results = new List<GoalStatus>();
        foreach (var goal in goals)
        {
            var isExcluded = goal.ExcludedDays.Contains(today.DayOfWeek);
            actuals.TryGetValue(goal.CategoryId, out var actual);
            var remaining = goal.TotalTarget - actual;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            var percent = goal.TotalTarget > TimeSpan.Zero
                ? Math.Min(actual / goal.TotalTarget * 100, 100)
                : 100;

            results.Add(new GoalStatus(
                GoalType: "Daily",
                CategoryName: getCategoryName(goal.CategoryId),
                CategoryId: goal.CategoryId,
                Target: goal.TotalTarget,
                Actual: actual,
                Remaining: remaining,
                IsMet: actual >= goal.TotalTarget,
                IsExcluded: isExcluded,
                PercentComplete: percent
            ));
        }
        return results;
    }

    public static List<GoalStatus> EvaluateWeeklyGoals(
        List<WeeklyGoal> goals,
        Dictionary<int, TimeSpan> actuals,
        Func<int, string> getCategoryName)
    {
        var results = new List<GoalStatus>();
        foreach (var goal in goals)
        {
            actuals.TryGetValue(goal.CategoryId, out var actual);
            var remaining = goal.TotalTarget - actual;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
            var percent = goal.TotalTarget > TimeSpan.Zero
                ? Math.Min(actual / goal.TotalTarget * 100, 100)
                : 100;

            results.Add(new GoalStatus(
                GoalType: "Weekly",
                CategoryName: getCategoryName(goal.CategoryId),
                CategoryId: goal.CategoryId,
                Target: goal.TotalTarget,
                Actual: actual,
                Remaining: remaining,
                IsMet: actual >= goal.TotalTarget,
                IsExcluded: false,
                PercentComplete: percent
            ));
        }
        return results;
    }
}
