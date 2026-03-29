namespace Goals.Models;

public class DailyGoal
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public TimeSpan TotalTarget { get; set; }
    public HashSet<DayOfWeek> ExcludedDays { get; set; } = [];
}
