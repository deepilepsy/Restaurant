namespace Restaurant.Models;

public class Tables
{
    public DateOnly reservationDate { get; set; }
    public TimeSpan reservationTime { get; set; }
    public int tableNo { get; set; }
}