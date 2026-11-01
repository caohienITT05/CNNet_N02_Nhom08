using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Models;

namespace SaigonChe.Web.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<OrderStatusHistory> OrderStatusHistory { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<DiningTable> DiningTables { get; set; }
        public DbSet<InventoryMovement> InventoryMovements { get; set; }
        public DbSet<PartyRequest> PartyRequests { get; set; }
        public DbSet<PartyRequestStatusHistory> PartyRequestStatusHistory { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Category>()
                .HasMany(c => c.Products)
                .WithOne(p => p.Category)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .Property(p => p.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(o => o.Total)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(o => o.DiscountAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(o => o.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<Order>()
                .HasOne(o => o.CustomerUser)
                .WithMany()
                .HasForeignKey(o => o.CustomerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.AssignedStaff)
                .WithMany()
                .HasForeignKey(o => o.AssignedStaffId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Table)
                .WithMany(t => t.Orders)
                .HasForeignKey(o => o.TableId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Order>()
                .HasOne(o => o.Voucher)
                .WithMany(v => v.Orders)
                .HasForeignKey(o => o.VoucherId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Order>()
                .HasIndex(o => o.TableId)
                .IsUnique()
                .HasFilter("[TableId] IS NOT NULL AND [Type] = 1 AND [IsPaid] = 0 AND [Status] <> 4");

            modelBuilder.Entity<OrderItem>()
                .Property(oi => oi.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.Items)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderStatusHistory>()
                .HasOne(h => h.Order)
                .WithMany(o => o.StatusHistory)
                .HasForeignKey(h => h.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderStatusHistory>()
                .HasOne(h => h.Actor)
                .WithMany()
                .HasForeignKey(h => h.ActorId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<OrderStatusHistory>()
                .HasIndex(h => new { h.OrderId, h.CreatedAt });

            modelBuilder.Entity<DiningTable>()
                .HasIndex(t => t.Code)
                .IsUnique();

            modelBuilder.Entity<InventoryMovement>()
                .HasOne(m => m.Product)
                .WithMany(p => p.InventoryMovements)
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryMovement>()
                .HasOne(m => m.Order)
                .WithMany()
                .HasForeignKey(m => m.OrderId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<InventoryMovement>()
                .HasOne(m => m.Actor)
                .WithMany()
                .HasForeignKey(m => m.ActorId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<InventoryMovement>()
                .HasIndex(m => new { m.OrderId, m.ProductId })
                .IsUnique()
                .HasFilter("[OrderId] IS NOT NULL");

            modelBuilder.Entity<Voucher>()
                .HasIndex(v => v.Code)
                .IsUnique();

            modelBuilder.Entity<Voucher>()
                .Property(v => v.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Voucher>()
                .Property(v => v.MinimumOrderTotal)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PartyRequest>()
                .Property(r => r.QuoteAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PartyRequest>()
                .HasOne(r => r.Customer)
                .WithMany()
                .HasForeignKey(r => r.CustomerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PartyRequest>()
                .HasOne(r => r.Order)
                .WithMany()
                .HasForeignKey(r => r.OrderId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PartyRequest>()
                .HasIndex(r => r.OrderId)
                .IsUnique()
                .HasFilter("[OrderId] IS NOT NULL");

            modelBuilder.Entity<PartyRequestStatusHistory>()
                .HasOne(h => h.PartyRequest)
                .WithMany(r => r.StatusHistory)
                .HasForeignKey(h => h.PartyRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PartyRequestStatusHistory>()
                .HasOne(h => h.Actor)
                .WithMany()
                .HasForeignKey(h => h.ActorId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PartyRequestStatusHistory>()
                .HasIndex(h => new { h.PartyRequestId, h.CreatedAt });
        }
    }
}
