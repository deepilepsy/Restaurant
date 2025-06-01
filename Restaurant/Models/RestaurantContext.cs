using Microsoft.EntityFrameworkCore;
using Restaurant.Models;

public class RestaurantContext : DbContext
{
    public RestaurantContext(DbContextOptions<RestaurantContext> options) : base(options)
    {
    }

    // DbSets for all entities
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<RestaurantTable> RestaurantTables { get; set; }
    public DbSet<Staff> Staff { get; set; }
    public DbSet<Credentials> Credentials { get; set; }
    public DbSet<StaffCredentials> StaffCredentials { get; set; }
    public DbSet<MenuItem> MenuItems { get; set; }
    public DbSet<Receipt> Receipts { get; set; }
    public DbSet<ReceiptItem> ReceiptItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Reservation relationships
        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Customer)
            .WithMany()
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Table)
            .WithMany()
            .HasForeignKey(r => r.TableId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.ServedBy)
            .WithMany()
            .HasForeignKey(r => r.ServedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure RestaurantTable relationships
        modelBuilder.Entity<RestaurantTable>()
            .HasOne(t => t.ServedBy)
            .WithMany()
            .HasForeignKey(t => t.ServedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure Receipt relationships
        modelBuilder.Entity<Receipt>()
            .HasOne(r => r.Reservation)
            .WithOne()
            .HasForeignKey<Receipt>(r => r.ReservationId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Receipt>()
            .HasOne(r => r.Table)
            .WithMany()
            .HasForeignKey(r => r.TableId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Receipt>()
            .HasOne(r => r.Customer)
            .WithMany()
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Receipt>()
            .HasOne(r => r.Staff)
            .WithMany()
            .HasForeignKey(r => r.StaffId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure ReceiptItem relationships
        modelBuilder.Entity<ReceiptItem>()
            .HasOne(ri => ri.Receipt)
            .WithMany(r => r.ReceiptItems)
            .HasForeignKey(ri => ri.ReceiptId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReceiptItem>()
            .HasOne(ri => ri.MenuItem)
            .WithMany()
            .HasForeignKey(ri => ri.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure decimal precision for monetary values
        modelBuilder.Entity<Receipt>()
            .Property(r => r.TotalAmount)
            .HasColumnType("decimal(10,2)");

        modelBuilder.Entity<ReceiptItem>()
            .Property(ri => ri.UnitPrice)
            .HasColumnType("decimal(10,2)");

        modelBuilder.Entity<ReceiptItem>()
            .Property(ri => ri.TotalPrice)
            .HasColumnType("decimal(10,2)");

        // Ensure proper table naming and column mapping
        modelBuilder.Entity<Customer>()
            .ToTable("customers");

        modelBuilder.Entity<Reservation>()
            .ToTable("reservations");

        modelBuilder.Entity<RestaurantTable>()
            .ToTable("restaurant_tables");

        modelBuilder.Entity<Staff>()
            .ToTable("staff");

        modelBuilder.Entity<Credentials>()
            .ToTable("credentials");

        modelBuilder.Entity<StaffCredentials>()
            .ToTable("staff_credentials");

        modelBuilder.Entity<MenuItem>()
            .ToTable("menu_items");

        modelBuilder.Entity<Receipt>()
            .ToTable("receipts");

        modelBuilder.Entity<ReceiptItem>()
            .ToTable("receipt_items");
    }
}