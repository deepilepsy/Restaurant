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
    public DbSet<ReservationDetail> ReservationDetails { get; set; }
    public DbSet<RestaurantTable> RestaurantTables { get; set; }
    public DbSet<Staff> Staff { get; set; }
    public DbSet<UserCredential> UserCredentials { get; set; }
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
            .HasOne(r => r.ReservationDetail)
            .WithMany()
            .HasForeignKey(r => r.ResDetailsId)
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
            .WithMany()
            .HasForeignKey(r => r.ReservationId)
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

        // Configure string-based monetary values (as per your SQL schema)
        modelBuilder.Entity<MenuItem>()
            .Property(m => m.Price)
            .HasMaxLength(20);

        modelBuilder.Entity<Receipt>()
            .Property(r => r.TotalAmount)
            .HasMaxLength(20);

        modelBuilder.Entity<ReceiptItem>()
            .Property(ri => ri.UnitPrice)
            .HasMaxLength(20);

        // Configure table naming to match SQL schema
        modelBuilder.Entity<Customer>()
            .ToTable("customers");

        modelBuilder.Entity<Reservation>()
            .ToTable("reservations");

        modelBuilder.Entity<ReservationDetail>()
            .ToTable("reservation_details");

        modelBuilder.Entity<RestaurantTable>()
            .ToTable("restaurant_tables");

        modelBuilder.Entity<Staff>()
            .ToTable("staff");

        modelBuilder.Entity<UserCredential>()
            .ToTable("user_credentials");

        modelBuilder.Entity<MenuItem>()
            .ToTable("menu_items");

        modelBuilder.Entity<Receipt>()
            .ToTable("receipts");

        modelBuilder.Entity<ReceiptItem>()
            .ToTable("receipt_items");

        // Configure identity columns
        modelBuilder.Entity<Reservation>()
            .Property(r => r.ReservationId)
            .UseIdentityColumn(600000, 1);
    }
}