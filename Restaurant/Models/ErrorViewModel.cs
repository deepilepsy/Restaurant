namespace Restaurant.Models;


public class ErrorViewModel
{
    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}

public class Reservation
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateTime Date { get; set; }
    public string Time { get; set; }
}

public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class ApplicationDbContext : DbContext
{
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<Employee> Employees { get; set; }
}



