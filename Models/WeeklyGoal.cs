namespace Goals.Models;

public class WeeklyGoal
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public TimeSpan TotalTarget { get; set; }
}
