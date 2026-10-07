using Microsoft.EntityFrameworkCore;
using SaigonChe.Api.Models;
namespace SaigonChe.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }
        public DbSet<Product> Products {get; set;}
        public DbSet<Category> Categories { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Product>()
                .Property(p=>p.Price)
                .HasPrecision(18,2);
        }
    }
}
