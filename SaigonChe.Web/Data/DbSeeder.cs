using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Models;

namespace SaigonChe.Web.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var configuration = services.GetRequiredService<IConfiguration>();

            await context.Database.MigrateAsync();

            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            const string adminEmail = "admin@gmail.com";
            const string previousAdminEmail = "admin@saigonche.com";

            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            var previousAdminUser = await userManager.FindByEmailAsync(previousAdminEmail);

            if (previousAdminUser != null)
            {
                if (adminUser != null && adminUser.Id != previousAdminUser.Id)
                {
                    throw new InvalidOperationException(
                        $"Cannot migrate the seeded admin account: {adminEmail} is already used by another account.");
                }

                adminUser = previousAdminUser;
                adminUser.UserName = adminEmail;
                adminUser.Email = adminEmail;
                adminUser.EmailConfirmed = true;

                var updateResult = await userManager.UpdateAsync(adminUser);
                if (!updateResult.Succeeded)
                {
                    var errors = string.Join("; ", updateResult.Errors.Select(error => error.Description));
                    throw new InvalidOperationException($"Could not update the admin email: {errors}");
                }
            }

            if (adminUser == null)
            {
                var adminPassword = configuration["Seed:AdminPassword"];
                if (string.IsNullOrWhiteSpace(adminPassword))
                {
                    throw new InvalidOperationException(
                        "Configure Seed:AdminPassword using a secure configuration provider before creating the initial Admin account.");
                }

                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, adminPassword);
                if (!result.Succeeded)
                {
                    var errors = string.Join("; ", result.Errors.Select(error => error.Description));
                    throw new InvalidOperationException($"Could not create the admin account: {errors}");
                }
            }

            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                var roleResult = await userManager.AddToRoleAsync(adminUser, "Admin");
                if (!roleResult.Succeeded)
                {
                    var errors = string.Join("; ", roleResult.Errors.Select(error => error.Description));
                    throw new InvalidOperationException($"Could not assign the Admin role: {errors}");
                }
            }

            if (!await context.Categories.AnyAsync())
            {
                context.Categories.AddRange(
                    new Category { Name = "Chè truyền thống", Description = "Món chè quen thuộc, đậm chất Sài Gòn." },
                    new Category { Name = "Chè hiện đại", Description = "Biến tấu mới mẻ, phù hợp giới trẻ." },
                    new Category { Name = "Món bán chạy", Description = "Món được khách hàng yêu thích nhất." }
                );
                await context.SaveChangesAsync();
            }

            if (!await context.Products.AnyAsync())
            {
                var categories = await context.Categories.ToListAsync();
                var traditional = categories.First(c => c.Name == "Chè truyền thống");
                var modern = categories.First(c => c.Name == "Chè hiện đại");

                context.Products.AddRange(
                    new Product { Name = "Chè ba màu", Description = "Sữa dừa, đậu xanh và thạch ngọt, tạo nên vị ngọt thanh và màu sắc bắt mắt.", Price = 25000m, IsAvailable = true, IsFeatured = true, CategoryId = traditional.Id, ImageUrl = "https://images.unsplash.com/photo-1563805042-7684c019e1cb?auto=format&fit=crop&w=900&q=80" },
                    new Product { Name = "Chè khúc bạch", Description = "Khúc bạch mềm mịn, hương thơm dịu nhẹ, phủ đầy sữa dừa đặc trưng.", Price = 30000m, IsAvailable = true, IsFeatured = true, CategoryId = traditional.Id, ImageUrl = "https://images.unsplash.com/photo-1571091718767-18b5b1457add?auto=format&fit=crop&w=900&q=80" },
                    new Product { Name = "Chè sầu riêng", Description = "Vị sầu riêng đậm đà, quyện cùng đá xay mát lạnh cho hương vị hấp dẫn.", Price = 45000m, IsAvailable = true, IsFeatured = true, CategoryId = modern.Id, ImageUrl = "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=900&q=80" },
                    new Product { Name = "Chè đậu xanh", Description = "Đậu xanh mềm, đường phèn ngọt thanh, điểm xuyết dừa nạo thơm béo.", Price = 22000m, IsAvailable = true, IsFeatured = false, CategoryId = traditional.Id, ImageUrl = "https://images.unsplash.com/photo-1511690743698-d9d85f2fbf38?auto=format&fit=crop&w=900&q=80" },
                    new Product { Name = "Chè bưởi", Description = "Bưởi sợi tươi mát, nước chè thanh nhẹ và chút mùi thơm của hoa quế.", Price = 28000m, IsAvailable = true, IsFeatured = false, CategoryId = modern.Id, ImageUrl = "https://images.unsplash.com/photo-1490645935967-10de6ba17061?auto=format&fit=crop&w=900&q=80" },
                    new Product { Name = "Chè khoai dẻo", Description = "Khoai dẻo bùi, sền sệt và đậm vị trong từng ngụm, rất được giới trẻ yêu thích.", Price = 26000m, IsAvailable = true, IsFeatured = false, CategoryId = traditional.Id, ImageUrl = "https://images.unsplash.com/photo-1504674900247-0877df9cc836?auto=format&fit=crop&w=900&q=80" },
                    new Product { Name = "Chè thái", Description = "Vị chua ngọt đậm đà, kết hợp đa dạng nguyên liệu và topping thơm ngon.", Price = 32000m, IsAvailable = true, IsFeatured = false, CategoryId = modern.Id, ImageUrl = "https://images.unsplash.com/photo-1559847844-5315695dadae?auto=format&fit=crop&w=900&q=80" },
                    new Product { Name = "Chè dừa dầm", Description = "Dừa nạo béo ngậy, sữa dừa ngọt dịu, là lựa chọn ưa chuộng của nhiều khách hàng.", Price = 27000m, IsAvailable = true, IsFeatured = false, CategoryId = traditional.Id, ImageUrl = "https://images.unsplash.com/photo-1551024601-bec78aea704b?auto=format&fit=crop&w=900&q=80" }
                );

                await context.SaveChangesAsync();
            }
        }
    }
}
