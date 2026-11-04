using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using TiemChe.Api;
using TiemChe.Api.Authorization;
using TiemChe.Api.Endpoints;
using TiemChe.Application.Interfaces;
using TiemChe.Application.Services;
using TiemChe.Application.Validation;
using TiemChe.Contracts.Auth;
using TiemChe.Domain.Enums;
using TiemChe.Infrastructure.Data;
using TiemChe.Infrastructure.Services;

var bootstrapAdmin = args is ["--bootstrap-admin"];
var builder = WebApplication.CreateBuilder(bootstrapAdmin ? [] : args);
var database = new SqliteConnectionStringBuilder(
    builder.Configuration.GetConnectionString("TiemChe")
    ?? "Data Source=tiemche.db")
{
    ForeignKeys = true
};

database.DataSource = Path.GetFullPath(database.DataSource, builder.Environment.ContentRootPath);
var searchInterceptor = new SqliteSearchInterceptor();
builder.Services.AddDbContext<TiemCheDbContext>(options =>
    options.UseSqlite(database.ToString()).AddInterceptors(searchInterceptor));
builder.Services.AddScoped<ITiemCheData>(services => services.GetRequiredService<TiemCheDbContext>());
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ManagementService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddSingleton(builder.Configuration.GetSection("Checkout").Get<CheckoutSettings>() ?? new CheckoutSettings());
builder.Services.AddSingleton(builder.Configuration.GetSection("Smtp").Get<SmtpSettings>() ?? new SmtpSettings());
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<IResetEmailSender, SmtpResetEmailSender>();
builder.Services.AddSingleton<IRevenueWorkbook, ExcelRevenueWorkbook>();
var imageDirectory = Path.GetFullPath(builder.Configuration["Images:Directory"] ?? ".tiemche/images",
    builder.Environment.ContentRootPath);
builder.Services.AddSingleton<IImageStore>(new LocalImageStore(imageDirectory));

builder.Services.AddDataProtection().SetApplicationName("TiemChe.Api");
builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken(options =>
{
    options.BearerTokenExpiration = TimeSpan.FromHours(2);
});
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(nameof(RoleName.Customer), policy => policy.RequireRole(nameof(RoleName.Customer)))
    .AddPolicy(nameof(RoleName.Admin), policy => policy.RequireRole(nameof(RoleName.Admin)))
    .AddPolicy(SessionClaims.Operators, policy => policy.RequireRole(nameof(RoleName.Staff), nameof(RoleName.Admin)));
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiErrors>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("account", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1)
        }));
});

var app = builder.Build();
if (bootstrapAdmin)
{
    await using var scope = app.Services.CreateAsyncScope();
    var service = scope.ServiceProvider.GetRequiredService<AccountService>();
    try
    {
        await service.BootstrapAdminAsync(new RegisterRequest(builder.Configuration["Seed:AdminEmail"],
            builder.Configuration["Seed:AdminPassword"], builder.Configuration["Seed:AdminName"] ?? "Quản trị viên", null),
            CancellationToken.None);
        Console.WriteLine("Đã tạo tài khoản quản trị.");
    }
    catch (BusinessException error)
    {
        Console.Error.WriteLine(error.Message);
        Environment.ExitCode = 1;
    }
    return;
}

app.UseExceptionHandler();
app.UseStatusCodePages(async status =>
{
    var context = status.HttpContext;
    var message = context.Response.StatusCode switch
    {
        400 => "Dữ liệu gửi lên không hợp lệ.",
        401 => "Vui lòng đăng nhập để tiếp tục.",
        403 => "Bạn không có quyền thực hiện thao tác này.",
        404 => "Không tìm thấy dữ liệu.",
        429 => "Bạn thao tác quá nhanh. Vui lòng thử lại sau.",
        _ => "Không thể xử lý yêu cầu."
    };
    await Results.Problem(statusCode: context.Response.StatusCode, detail: message).ExecuteAsync(context);
});
app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        if (!context.Request.IsHttps)
        {
            await Results.Problem(statusCode: 400, detail: "Vui lòng kết nối với API qua HTTPS.").ExecuteAsync(context);
            return;
        }
        await next(context);
    });
}
Directory.CreateDirectory(imageDirectory);
app.UseStaticFiles(new StaticFileOptions
{
    RequestPath = LocalImageStore.RequestPath,
    FileProvider = new PhysicalFileProvider(imageDirectory),
    OnPrepareResponse = context => context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff"
});
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var user = context.User;
    if (user.Identity?.IsAuthenticated == true)
    {
        var service = context.RequestServices.GetRequiredService<AccountService>();
        if (!Guid.TryParse(user.FindFirstValue(SessionClaims.Stamp), out var stamp)
            || !await service.IsSessionValidAsync(user.UserId(), stamp, context.RequestAborted))
        {
            await Results.Problem(statusCode: 401, detail: "Phiên đăng nhập đã hết hiệu lực.").ExecuteAsync(context);
            return;
        }
    }
    await next(context);
});
app.UseAuthorization();
var api = app.MapGroup("/api");
api.MapAccounts();
api.MapShopping();
api.MapManagement();
app.Run();
