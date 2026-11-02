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

        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        public DbSet<OrderStatusHistory> OrderStatusHistory { get; set; } = null!;
        public DbSet<StaffOrderStatusHistory> OrderStatusHistories { get; set; } = null!;
        public DbSet<ContactMessage> ContactMessages { get; set; } = null!;
        public DbSet<DiningTable> DiningTables { get; set; } = null!;
        public DbSet<InventoryMovement> InventoryMovements { get; set; } = null!;
        public DbSet<PartyRequest> PartyRequests { get; set; } = null!;
        public DbSet<PartyRequestStatusHistory> PartyRequestStatusHistory { get; set; } = null!;
        public DbSet<Voucher> Vouchers { get; set; } = null!;
        public DbSet<Cart> Carts { get; set; } = null!;
        public DbSet<CartItem> CartItems { get; set; } = null!;
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; } = null!;
        public DbSet<Coupon> Coupons { get; set; } = null!;
        public DbSet<Favorite> Favorites { get; set; } = null!;
        public DbSet<Review> Reviews { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Category>()
                .HasMany(category => category.Products)
                .WithOne(product => product.Category)
                .HasForeignKey(product => product.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Product>()
                .Property(product => product.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(order => order.Total)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(order => order.DiscountAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Order>()
                .Property(order => order.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<Order>()
                .HasOne(order => order.CustomerUser)
                .WithMany()
                .HasForeignKey(order => order.CustomerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(order => order.Customer)
                .WithMany(user => user.CustomerOrders)
                .HasForeignKey(order => order.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .HasOne(order => order.AssignedStaff)
                .WithMany(user => user.AssignedOrders)
                .HasForeignKey(order => order.AssignedStaffId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Order>()
                .HasOne(order => order.Table)
                .WithMany(table => table.Orders)
                .HasForeignKey(order => order.TableId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Order>()
                .HasOne(order => order.Voucher)
                .WithMany(voucher => voucher.Orders)
                .HasForeignKey(order => order.VoucherId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Order>()
                .HasIndex(order => order.TableId)
                .IsUnique()
                .HasFilter("[TableId] IS NOT NULL AND [Type] = 1 AND [IsPaid] = 0 AND [Status] <> 4");

            modelBuilder.Entity<OrderItem>()
                .Property(orderItem => orderItem.Price)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<OrderItem>()
                .HasOne(orderItem => orderItem.Order)
                .WithMany(order => order.Items)
                .HasForeignKey(orderItem => orderItem.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(orderItem => orderItem.Product)
                .WithMany(product => product.OrderItems)
                .HasForeignKey(orderItem => orderItem.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderStatusHistory>()
                .ToTable("OrderStatusHistory");

            modelBuilder.Entity<OrderStatusHistory>()
                .HasOne(history => history.Order)
                .WithMany(order => order.StatusHistory)
                .HasForeignKey(history => history.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderStatusHistory>()
                .HasOne(history => history.Actor)
                .WithMany()
                .HasForeignKey(history => history.ActorId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<OrderStatusHistory>()
                .HasIndex(history => new { history.OrderId, history.CreatedAt });

            modelBuilder.Entity<StaffOrderStatusHistory>()
                .ToTable("OrderStatusHistories");

            modelBuilder.Entity<StaffOrderStatusHistory>()
                .HasOne(history => history.Order)
                .WithMany(order => order.StaffStatusHistory)
                .HasForeignKey(history => history.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<StaffOrderStatusHistory>()
                .HasIndex(history => history.OrderId);

            modelBuilder.Entity<DiningTable>()
                .HasIndex(table => table.Code)
                .IsUnique();

            modelBuilder.Entity<InventoryMovement>()
                .HasOne(movement => movement.Product)
                .WithMany(product => product.InventoryMovements)
                .HasForeignKey(movement => movement.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryMovement>()
                .HasOne(movement => movement.Order)
                .WithMany()
                .HasForeignKey(movement => movement.OrderId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<InventoryMovement>()
                .HasOne(movement => movement.Actor)
                .WithMany()
                .HasForeignKey(movement => movement.ActorId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<InventoryMovement>()
                .HasIndex(movement => new { movement.OrderId, movement.ProductId })
                .IsUnique()
                .HasFilter("[OrderId] IS NOT NULL");

            modelBuilder.Entity<Voucher>()
                .HasIndex(voucher => voucher.Code)
                .IsUnique();

            modelBuilder.Entity<Voucher>()
                .Property(voucher => voucher.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Voucher>()
                .Property(voucher => voucher.MinimumOrderTotal)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PartyRequest>()
                .Property(request => request.QuoteAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PartyRequest>()
                .HasOne(request => request.Customer)
                .WithMany()
                .HasForeignKey(request => request.CustomerUserId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PartyRequest>()
                .HasOne(request => request.Order)
                .WithMany()
                .HasForeignKey(request => request.OrderId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PartyRequest>()
                .HasIndex(request => request.OrderId)
                .IsUnique()
                .HasFilter("[OrderId] IS NOT NULL");

            modelBuilder.Entity<PartyRequestStatusHistory>()
                .HasOne(history => history.PartyRequest)
                .WithMany(request => request.StatusHistory)
                .HasForeignKey(history => history.PartyRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PartyRequestStatusHistory>()
                .HasOne(history => history.Actor)
                .WithMany()
                .HasForeignKey(history => history.ActorId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PartyRequestStatusHistory>()
                .HasIndex(history => new { history.PartyRequestId, history.CreatedAt });

            modelBuilder.Entity<Favorite>()
                .HasIndex(favorite => new { favorite.UserId, favorite.ProductId })
                .IsUnique();

            modelBuilder.Entity<Favorite>()
                .HasOne(favorite => favorite.User)
                .WithMany(user => user.Favorites)
                .HasForeignKey(favorite => favorite.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Favorite>()
                .HasOne(favorite => favorite.Product)
                .WithMany(product => product.Favorites)
                .HasForeignKey(favorite => favorite.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasIndex(review => new { review.ProductId, review.UserId })
                .IsUnique();

            modelBuilder.Entity<Review>()
                .HasOne(review => review.Product)
                .WithMany(product => product.Reviews)
                .HasForeignKey(review => review.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasOne(review => review.User)
                .WithMany(user => user.CustomerReviews)
                .HasForeignKey(review => review.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(review => review.RepliedBy)
                .WithMany(user => user.RepliedReviews)
                .HasForeignKey(review => review.RepliedById)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
