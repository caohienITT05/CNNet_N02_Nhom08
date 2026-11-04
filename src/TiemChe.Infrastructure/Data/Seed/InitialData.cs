using Microsoft.EntityFrameworkCore;
using TiemChe.Domain.Enums;
using TiemChe.Domain.Models;

namespace TiemChe.Infrastructure.Data.Seed;

internal static class InitialData
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<Role>().HasData(
            new Role { Id = 1, Name = RoleName.Customer },
            new Role { Id = 2, Name = RoleName.Staff },
            new Role { Id = 3, Name = RoleName.Admin });

        model.Entity<Category>().HasData(
            new Category { Id = 1, Name = "Chè truyền thống", DisplayOrder = 1 },
            new Category { Id = 2, Name = "Chè đặc biệt", DisplayOrder = 2 },
            new Category { Id = 3, Name = "Trà sữa", DisplayOrder = 3 });

        // Ngày cố định giúp chạy lại công cụ migration mà không phát sinh thay đổi seed.
        var seedDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        model.Entity<Product>().HasData(
            new Product { Id = 1, CategoryId = 2, Name = "Chè dưỡng nhan", Description = "Tuyết yến, nhựa đào, táo đỏ và long nhãn.", CreatedAtUtc = seedDate },
            new Product { Id = 2, CategoryId = 2, Name = "Chè khúc bạch", Description = "Khúc bạch sữa, nhãn và hạnh nhân lát.", CreatedAtUtc = seedDate },
            new Product { Id = 3, CategoryId = 2, Name = "Chè Thái", Description = "Trái cây, thạch và nước cốt dừa.", CreatedAtUtc = seedDate },
            new Product { Id = 4, CategoryId = 1, Name = "Chè bưởi", Description = "Cùi bưởi giòn, đậu xanh và cốt dừa.", CreatedAtUtc = seedDate },
            new Product { Id = 5, CategoryId = 1, Name = "Chè đậu xanh", Description = "Đậu xanh nấu mềm với cốt dừa.", CreatedAtUtc = seedDate },
            new Product { Id = 6, CategoryId = 3, Name = "Trà sữa truyền thống", Description = "Hồng trà sữa có vị trà đậm.", CreatedAtUtc = seedDate },
            new Product { Id = 7, CategoryId = 3, Name = "Trà sữa ô long", Description = "Trà ô long thơm dịu pha với sữa.", CreatedAtUtc = seedDate.AddDays(1) });

        model.Entity<ProductSize>().HasData(
            new ProductSize { Id = 1, ProductId = 1, Name = "Vừa", PriceVnd = 35000 },
            new ProductSize { Id = 2, ProductId = 1, Name = "Lớn", PriceVnd = 45000 },
            new ProductSize { Id = 3, ProductId = 2, Name = "Vừa", PriceVnd = 32000 },
            new ProductSize { Id = 4, ProductId = 2, Name = "Lớn", PriceVnd = 42000 },
            new ProductSize { Id = 5, ProductId = 3, Name = "Vừa", PriceVnd = 35000 },
            new ProductSize { Id = 6, ProductId = 3, Name = "Lớn", PriceVnd = 45000 },
            new ProductSize { Id = 7, ProductId = 4, Name = "Tiêu chuẩn", PriceVnd = 28000 },
            new ProductSize { Id = 8, ProductId = 5, Name = "Tiêu chuẩn", PriceVnd = 25000 },
            new ProductSize { Id = 9, ProductId = 6, Name = "Vừa", PriceVnd = 30000 },
            new ProductSize { Id = 10, ProductId = 6, Name = "Lớn", PriceVnd = 40000 },
            new ProductSize { Id = 11, ProductId = 7, Name = "Vừa", PriceVnd = 35000 },
            new ProductSize { Id = 12, ProductId = 7, Name = "Lớn", PriceVnd = 45000 });

        model.Entity<Topping>().HasData(
            new Topping { Id = 1, Name = "Trân châu đen", PriceVnd = 7000 },
            new Topping { Id = 2, Name = "Thạch dừa", PriceVnd = 5000 },
            new Topping { Id = 3, Name = "Khúc bạch", PriceVnd = 8000 });

        model.Entity<ProductTopping>().HasData(
            new ProductTopping { ProductId = 2, ToppingId = 3 },
            new ProductTopping { ProductId = 3, ToppingId = 2 },
            new ProductTopping { ProductId = 4, ToppingId = 2 },
            new ProductTopping { ProductId = 6, ToppingId = 1 },
            new ProductTopping { ProductId = 6, ToppingId = 2 },
            new ProductTopping { ProductId = 7, ToppingId = 1 },
            new ProductTopping { ProductId = 7, ToppingId = 2 });
    }
}
