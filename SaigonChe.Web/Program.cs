using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SaigonChe.Web.Data;
using SaigonChe.Web.Models;
using SaigonChe.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=SaigonCheDb;Trusted_Connection=True;TrustServerCertificate=True;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddScoped<OrderCompletionService>();
builder.Services.AddScoped<ProductImageService>();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Apply changed security stamps immediately so a locked staff account cannot
// continue using a previously issued cookie for the default validation window.
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.Zero);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ReturnUrlParameter = "returnUrl";
    options.SlidingExpiration = true;
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddHttpClient();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await DbSeeder.SeedAsync(services);
    }
    catch (SqlException exception)
    {
        app.Logger.LogCritical("Database initialization failed (SQL {Number}). Check the configured SQL Server/LocalDB instance and migration permissions. No database was recreated.", exception.Number);
        Environment.ExitCode = 1;
        return;
    }
    catch (InvalidOperationException exception)
    {
        app.Logger.LogCritical("Database seeding failed: {Message}", exception.Message);
        Environment.ExitCode = 1;
        return;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.Use(async (context, next) =>
{
    try
    {
        var path = context.Request.Path.Value?.TrimEnd('/');
        if (string.Equals(path, "/Identity/Account/Register", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, "/Identity/Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            if (HttpMethods.IsGet(context.Request.Method))
                context.Response.Redirect("/Account/" + (path!.EndsWith("Register", StringComparison.OrdinalIgnoreCase) ? "Register" : "Login") + context.Request.QueryString);
            else
                context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next();
    }
    catch (Exception failure) when (!context.Response.HasStarted && DatabaseErrors.FindSqlException(failure) is not null)
    {
        var exception = DatabaseErrors.FindSqlException(failure)!;
        app.Logger.LogError("Database request failed (SQL {Number}, trace {TraceId}).", exception.Number, context.TraceIdentifier);
        context.Response.Clear();
        context.Response.StatusCode = exception.Number == 1205 ? 409 : 503;
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync("<!doctype html><html lang=\"vi\"><meta charset=\"utf-8\"><title>Tạm thời gián đoạn</title><body><h1>Chưa thể hoàn tất thao tác</h1><p>Dịch vụ đang bận hoặc tạm thời gián đoạn. Vui lòng tải lại trang và kiểm tra kết quả trước khi thử lại.</p></body></html>");
    }
});
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
