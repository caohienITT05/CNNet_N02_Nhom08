using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using SaigonChe.Api.Data;
using SaigonChe.Api.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddDbContext<AppDbContext>(options => 
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

var app = builder.Build();

app.Use(async (context, next) =>
{
    try { await next(); }
    catch (Exception failure) when (!context.Response.HasStarted && FindSql(failure) is not null)
    {
        app.Logger.LogError("Catalog database unavailable (SQL {Number}).", FindSql(failure)!.Number);
        context.Response.Clear();
        context.Response.StatusCode = 503;
        await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = 503, Title = "Danh mục tạm thời chưa khả dụng.", Detail = "Vui lòng thử lại sau."
        });
    }
});
app.UseHttpsRedirection();
// This legacy API has no authentication store. Keep its read-only catalog
// endpoints available, but disable unauthenticated writes in every environment.
app.Use(async (context, next) =>
{
    var isWrite = HttpMethods.IsPost(context.Request.Method)
        || HttpMethods.IsPut(context.Request.Method)
        || HttpMethods.IsPatch(context.Request.Method)
        || HttpMethods.IsDelete(context.Request.Method);

    if (isWrite
        && context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});
app.UseAuthorization();
app.MapControllers();

app.Run();

static SqlException? FindSql(Exception exception)
{
    for (Exception? current = exception; current is not null; current = current.InnerException)
        if (current is SqlException sql) return sql;
    return null;
}
