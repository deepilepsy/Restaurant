using Restaurant.Models;
using Microsoft.EntityFrameworkCore;

namespace Restaurant.Models
{
    public class RestaurantContext : DbContext
    {
        public RestaurantContext(DbContextOptions<RestaurantContext> options)
            : base(options)
        {
        }

        public DbSet<Staff> Staff { get; set; }
        public DbSet<Tables> Tables { get; set; }
        public DbSet<Receipt> Receipts { get; set; }
        public DbSet<ExpiredReceipts> ExpiredReceipts { get; set; }
        public DbSet<Credentials> Credentials { get; set; }

    }
}