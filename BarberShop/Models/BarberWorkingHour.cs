namespace BarberShop.Models;

public class BarberWorkingHour
{
    public int Id { get; set; }

    public int BarberId { get; set; }
    public virtual Barber Barber { get; set; } = null!;

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; } = new TimeSpan(9, 0, 0);

    public TimeSpan EndTime { get; set; } = new TimeSpan(19, 0, 0);

    public TimeSpan? LunchBreakStart { get; set; } = new TimeSpan(12, 0, 0);

    public TimeSpan? LunchBreakEnd { get; set; } = new TimeSpan(13, 0, 0);

    public bool IsWorkingDay { get; set; } = true;
}
