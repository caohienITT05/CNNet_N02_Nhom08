using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Models;

namespace SaigonChe.Web.Data
{
    public static class DbSeeder
    {
        private static readonly Dictionary<string, string> MenuImages = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Chè ba màu"] = "/images/menu/che-ba-mau.jpg",
            ["Chè khúc bạch"] = "/images/menu/che-khuc-bach.jpg",
            ["Chè sầu riêng"] = "/images/menu/che-sau-rieng.jpg",
            ["Chè đậu xanh"] = "/images/menu/che-dau-xanh.jpg",
            ["Chè bưởi"] = "/images/menu/che-buoi.jpg",
            ["Chè khoai dẻo"] = "/images/menu/che-khoai-deo.jpg",
            ["Chè thái"] = "/images/menu/che-thai.jpg",
            ["Chè dừa dầm"] = "/images/menu/che-dua-dam.jpg"
        };

        private static readonly HashSet<string> LegacyImageUrls = new(StringComparer.Ordinal)
        {
            "https://images.unsplash.com/photo-1563805042-7684c019e1cb?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1571091718767-18b5b1457add?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1511690743698-d9d85f2fbf38?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1490645935967-10de6ba17061?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1504674900247-0877df9cc836?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1559847844-5315695dadae?auto=format&fit=crop&w=900&q=80",
            "https://images.unsplash.com/photo-1551024601-bec78aea704b?auto=format&fit=crop&w=900&q=80",
            "/uploads/products/580d8058b41948058dda344bbbb9ea35.png",
            "/uploads/products/b7704274c250469db1d3e8f83ceaa54b.jpg",
            "/uploads/products/b196bcf23e8049fe858d6a7cf318b3ab.png"
        };

        public static async Task SeedAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var configuration = services.GetRequiredService<IConfiguration>();
            var environment = services.GetRequiredService<IHostEnvironment>();
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbSeeder));

            await context.Database.MigrateAsync();

            foreach (var roleName in new[] { "Admin", "Staff", "Customer" })
            {
                if (await roleManager.RoleExistsAsync(roleName))
                {
                    continue;
                }

                var roleResult = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!roleResult.Succeeded)
                {
                    var errors = string.Join("; ", roleResult.Errors.Select(error => error.Description));
                    throw new InvalidOperationException($"Could not create the {roleName} role: {errors}");
                }
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
                    new Product { Name = "Chè ba màu", Description = "Sữa dừa, đậu xanh và thạch ngọt, tạo nên vị ngọt thanh và màu sắc bắt mắt.", Price = 25000m, IsAvailable = true, IsFeatured = true, CategoryId = traditional.Id, ImageUrl = MenuImages["Chè ba màu"] },
                    new Product { Name = "Chè khúc bạch", Description = "Khúc bạch mềm mịn, hương thơm dịu nhẹ, phủ đầy sữa dừa đặc trưng.", Price = 30000m, IsAvailable = true, IsFeatured = true, CategoryId = traditional.Id, ImageUrl = MenuImages["Chè khúc bạch"] },
                    new Product { Name = "Chè sầu riêng", Description = "Vị sầu riêng đậm đà, quyện cùng đá xay mát lạnh cho hương vị hấp dẫn.", Price = 45000m, IsAvailable = true, IsFeatured = true, CategoryId = modern.Id, ImageUrl = MenuImages["Chè sầu riêng"] },
                    new Product { Name = "Chè đậu xanh", Description = "Đậu xanh mềm, đường phèn ngọt thanh, điểm xuyết dừa nạo thơm béo.", Price = 22000m, IsAvailable = true, IsFeatured = false, CategoryId = traditional.Id, ImageUrl = MenuImages["Chè đậu xanh"] },
                    new Product { Name = "Chè bưởi", Description = "Bưởi sợi tươi mát, nước chè thanh nhẹ và chút mùi thơm của hoa quế.", Price = 28000m, IsAvailable = true, IsFeatured = false, CategoryId = modern.Id, ImageUrl = MenuImages["Chè bưởi"] },
                    new Product { Name = "Chè khoai dẻo", Description = "Khoai dẻo bùi, sền sệt và đậm vị trong từng ngụm, rất được giới trẻ yêu thích.", Price = 26000m, IsAvailable = true, IsFeatured = false, CategoryId = traditional.Id, ImageUrl = MenuImages["Chè khoai dẻo"] },
                    new Product { Name = "Chè thái", Description = "Vị chua ngọt đậm đà, kết hợp đa dạng nguyên liệu và topping thơm ngon.", Price = 32000m, IsAvailable = true, IsFeatured = false, CategoryId = modern.Id, ImageUrl = MenuImages["Chè thái"] },
                    new Product { Name = "Chè dừa dầm", Description = "Dừa nạo béo ngậy, sữa dừa ngọt dịu, là lựa chọn ưa chuộng của nhiều khách hàng.", Price = 27000m, IsAvailable = true, IsFeatured = false, CategoryId = traditional.Id, ImageUrl = MenuImages["Chè dừa dầm"] }
                );

                await context.SaveChangesAsync();
            }

            await UpdateLegacyProductImagesAsync(context);

            if (environment.IsDevelopment())
            {
                await SeedDevelopmentReviewsAsync(context, userManager, configuration, logger);
            }
        }

        private static async Task SeedDevelopmentReviewsAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ILogger logger)
        {
            var samplePassword = configuration["Seed:SampleCustomerPassword"];
            if (string.IsNullOrWhiteSpace(samplePassword))
            {
                logger.LogInformation(
                    "Development sample customers and reviews were not seeded. Configure Seed:SampleCustomerPassword to enable them.");
                return;
            }

            var sampleCustomers = new[]
            {
                (Email: "minhanh@example.com", FullName: "Minh Anh"),
                (Email: "huyentran@example.com", FullName: "Huyền Trân"),
                (Email: "ducnam@example.com", FullName: "Đức Nam")
            };
            var customers = new List<ApplicationUser>();

            foreach (var sample in sampleCustomers)
            {
                var user = await userManager.FindByEmailAsync(sample.Email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = sample.Email,
                        Email = sample.Email,
                        FullName = sample.FullName,
                        EmailConfirmed = true
                    };

                    var createResult = await userManager.CreateAsync(user, samplePassword);
                    if (!createResult.Succeeded)
                    {
                        var errors = string.Join("; ", createResult.Errors.Select(error => error.Description));
                        throw new InvalidOperationException($"Could not create development customer {sample.Email}: {errors}");
                    }
                }

                if (!await userManager.IsInRoleAsync(user, "Customer"))
                {
                    var roleResult = await userManager.AddToRoleAsync(user, "Customer");
                    if (!roleResult.Succeeded)
                    {
                        var errors = string.Join("; ", roleResult.Errors.Select(error => error.Description));
                        throw new InvalidOperationException($"Could not assign the Customer role: {errors}");
                    }
                }

                customers.Add(user);
            }

            var products = await context.Products
                .AsNoTracking()
                .Where(product => new[] { "Chè ba màu", "Chè khúc bạch", "Chè sầu riêng" }.Contains(product.Name))
                .ToDictionaryAsync(product => product.Name);
            var sampleReviews = new[]
            {
                (CustomerIndex: 0, ProductName: "Chè ba màu", Rating: 5, Comment: "Vị ngọt vừa phải, nguyên liệu bùi và thơm."),
                (CustomerIndex: 1, ProductName: "Chè khúc bạch", Rating: 5, Comment: "Khúc bạch mềm mịn, ăn rất mát."),
                (CustomerIndex: 2, ProductName: "Chè sầu riêng", Rating: 4, Comment: "Mùi sầu riêng thơm, phần ăn khá đầy đặn.")
            };

            foreach (var sample in sampleReviews)
            {
                if (!products.TryGetValue(sample.ProductName, out var product))
                {
                    continue;
                }

                var customer = customers[sample.CustomerIndex];
                var alreadyReviewed = await context.Reviews.AnyAsync(review =>
                    review.ProductId == product.Id && review.UserId == customer.Id);
                if (!alreadyReviewed)
                {
                    context.Reviews.Add(new Review
                    {
                        ProductId = product.Id,
                        UserId = customer.Id,
                        Rating = sample.Rating,
                        Comment = sample.Comment,
                        CreatedAt = DateTime.UtcNow.AddDays(-sample.CustomerIndex - 1)
                    });
                }
            }

            await context.SaveChangesAsync();
        }

        private static async Task UpdateLegacyProductImagesAsync(ApplicationDbContext context)
        {
            var productNames = MenuImages.Keys.ToArray();
            var products = await context.Products
                .Where(product => productNames.Contains(product.Name))
                .ToListAsync();

            var hasChanges = false;
            foreach (var product in products)
            {
                if (product.ImageUrl != null && LegacyImageUrls.Contains(product.ImageUrl))
                {
                    product.ImageUrl = MenuImages[product.Name];
                    hasChanges = true;
                }
            }

            if (hasChanges)
            {
                await context.SaveChangesAsync();
            }
        }
    }
}
