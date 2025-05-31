using Microsoft.EntityFrameworkCore;
using Restaurant.Models;

namespace Restaurant
{
    public class RestaurantContext : DbContext
    {
        public RestaurantContext(DbContextOptions<RestaurantContext> options) : base(options)
        {
        }

        // DbSets - make sure these match what you're using in your controllers
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<RestaurantTable> RestaurantTables { get; set; }
        public DbSet<Staff> Staff { get; set; }
        public DbSet<Credentials> Credentials { get; set; }
        public DbSet<StaffCredentials> StaffCredentials { get; set; }
        public DbSet<Customer> Customers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure relationships
            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.Table)
                .WithMany(t => t.Reservations)
                .HasForeignKey(r => r.TableId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Reservation>()
                .HasOne(r => r.ServedBy)
                .WithMany(s => s.Reservations)
                .HasForeignKey(r => r.ServedById)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RestaurantTable>()
                .HasOne(t => t.ServedBy)
                .WithMany(s => s.Tables)
                .HasForeignKey(t => t.ServedById)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure decimal precision if needed
            modelBuilder.Entity<Reservation>()
                .Property(r => r.ReservationDate)
                .HasColumnType("date");

            // Set default values
            modelBuilder.Entity<Reservation>()
                .Property(r => r.ReservationStatus)
                .HasDefaultValue("active");

            modelBuilder.Entity<Reservation>()
                .Property(r => r.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // Configure identity columns to match your SQL
            modelBuilder.Entity<Reservation>()
                .Property(r => r.ReservationId)
                .UseIdentityColumn(600000, 1); // Start at 600000, increment by 1

            modelBuilder.Entity<Staff>()
                .Property(s => s.StaffId)
                .UseIdentityColumn(1, 1); // Start at 1, increment by 1

            modelBuilder.Entity<Credentials>()
                .Property(c => c.Id)
                .UseIdentityColumn(1, 1);

            modelBuilder.Entity<StaffCredentials>()
                .Property(sc => sc.Id)
                .UseIdentityColumn(1, 1);
        }
    }
}