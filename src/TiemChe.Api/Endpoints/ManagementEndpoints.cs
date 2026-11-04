using System.Security.Claims;
using TiemChe.Api.Authorization;
using TiemChe.Application.Interfaces;
using TiemChe.Application.Services;
using TiemChe.Application.Validation;
using TiemChe.Contracts.Auth;
using TiemChe.Contracts.Catalog;
using TiemChe.Contracts.Management;
using TiemChe.Contracts.Operations;
using TiemChe.Contracts.Shopping;
using TiemChe.Domain.Enums;

namespace TiemChe.Api.Endpoints;

internal static class ManagementEndpoints
{
    internal static void MapManagement(this IEndpointRouteBuilder api)
    {
        var operations = api.MapGroup("/operations").RequireAuthorization(SessionClaims.Operators);
        operations.MapGet("/summary", (ClaimsPrincipal user, OperationsService service, CancellationToken ct) =>
            service.SummaryAsync(user.UserId(), ct));
        operations.MapGet("/availability", (OperationsService service, CancellationToken ct) =>
            service.AvailabilityAsync(ct));
        operations.MapGet("/checkout-options", (OperationsService service) => service.CheckoutOptions());
        operations.MapGet("/shifts/current", async (ClaimsPrincipal user, OperationsService service, CancellationToken ct) =>
            new CashShiftStateDto(await service.CurrentShiftAsync(user.UserId(), ct)));
        operations.MapPost("/shifts/open", (OpenCashShiftRequest body, ClaimsPrincipal user, OperationsService service,
            CancellationToken ct) => service.OpenShiftAsync(user.UserId(), body.OpeningCashVnd, ct));
        operations.MapPost("/shifts/close", (CloseCashShiftRequest body, ClaimsPrincipal user, OperationsService service,
            CancellationToken ct) => service.CloseShiftAsync(user.UserId(), body.CountedCashVnd, body.Note, ct));
        operations.MapGet("/shifts", (ClaimsPrincipal user, int? page, int? pageSize, OperationsService service,
            CancellationToken ct) => service.ShiftHistoryAsync(user.UserId(), null, page, pageSize, ct));
        operations.MapGet("/customers", (string? search, OperationsService service, CancellationToken ct) =>
            service.CustomersAsync(search, ct));
        operations.MapGet("/customers/{id:int}/orders", (int id, int? page, int? pageSize, OrderService service,
            CancellationToken ct) => service.CustomerOrdersAsync(id, page, pageSize, ct));
        operations.MapGet("/reviews", (int? page, int? pageSize, bool? visible, ManagementService service,
            CancellationToken ct) => service.ReviewsAsync(page, pageSize, visible, ct));
        operations.MapPut("/reviews/{id:int}/visibility", async (int id, ReviewVisibilityRequest body,
            ManagementService service, CancellationToken ct) =>
        {
            await service.SetReviewVisibilityAsync(id, body.IsVisible, ct);
            return Results.NoContent();
        });
        operations.MapGet("/availability/log", (int? page, OperationsService service, CancellationToken ct) =>
            service.AvailabilityLogsAsync(page, ct));
        operations.MapPut("/products/{id:int}/availability", async (int id, AvailabilityRequest body,
            ClaimsPrincipal user, OperationsService service, CancellationToken ct) =>
        {
            await service.SetProductAvailabilityAsync(id, body.IsActive, user.UserId(), ct);
            return Results.NoContent();
        });
        operations.MapPut("/product-sizes/{id:int}/availability", async (int id, AvailabilityRequest body,
            ClaimsPrincipal user, OperationsService service, CancellationToken ct) =>
        {
            await service.SetProductSizeAvailabilityAsync(id, body.IsActive, user.UserId(), ct);
            return Results.NoContent();
        });
        operations.MapPut("/toppings/{id:int}/availability", async (int id, AvailabilityRequest body,
            ClaimsPrincipal user, OperationsService service, CancellationToken ct) =>
        {
            await service.SetToppingAvailabilityAsync(id, body.IsActive, user.UserId(), ct);
            return Results.NoContent();
        });

        var orders = operations.MapGroup("/orders");
        orders.MapPost("/quote", (StaffOrderRequest body, OrderService service, CancellationToken ct) =>
            service.StaffQuoteAsync(body, ct));
        orders.MapPost("/{id:int}/notes", (int id, OrderNoteRequest body, ClaimsPrincipal user, OrderService service,
            CancellationToken ct) => service.AddInternalNoteAsync(id, user.UserId(), body, ct));
        orders.MapPost("", (StaffOrderRequest body, ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
            service.CreateStaffOrderAsync(user.UserId(), body, ct));
        orders.MapGet("", (OrderStatus? status, OrderChannel? channel, string? search, int? page, int? pageSize, DateOnly? from, DateOnly? to,
            PaymentMethod? paymentMethod, PaymentStatus? paymentStatus, bool? oldestFirst,
            OrderService service, CancellationToken ct) =>
            service.ListAsync(null, status, search, page, pageSize, ct, from, to, paymentMethod, paymentStatus,
                oldestFirst ?? false, channel));
        orders.MapGet("/{id:int}", (int id, OrderService service, CancellationToken ct) => service.GetAsync(id, null, ct));
        orders.MapPut("/{id:int}/status", (int id, OrderStatusRequest body, ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
            service.UpdateStatusAsync(id, user.UserId(), body, ct));
        orders.MapPost("/{id:int}/confirm-transfer", (int id, ConfirmTransferRequest body, ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
            service.ConfirmTransferAsync(id, user.UserId(), body, ct));

        var admin = api.MapGroup("/admin").RequireAuthorization(nameof(RoleName.Admin));
        admin.MapGet("/customers", (int? page, int? pageSize, string? search, AccountService service, CancellationToken ct) =>
            service.ListAsync(RoleName.Customer, page, pageSize, search, ct));
        admin.MapGet("/staff", (int? page, int? pageSize, string? search, AccountService service, CancellationToken ct) =>
            service.ListAsync(RoleName.Staff, page, pageSize, search, ct));
        admin.MapGet("/shifts", (int? staffId, int? page, int? pageSize, OperationsService service,
            CancellationToken ct) => service.ShiftHistoryAsync(null, staffId, page, pageSize, ct));
        admin.MapPost("/staff", (RegisterRequest body, AccountService service, CancellationToken ct) =>
            service.CreateStaffAsync(body, ct));
        admin.MapPut("/accounts/{id:int}/status", async (int id, AccountStatusRequest body, AccountService service, CancellationToken ct) =>
        {
            await service.SetActiveAsync(id, body.IsActive, ct);
            return Results.NoContent();
        });

        admin.MapGet("/categories", (CatalogService service, CancellationToken ct) => service.CategoriesAsync(true, ct));
        admin.MapPost("/categories", (CategoryRequest body, ManagementService service, CancellationToken ct) => service.SaveCategoryAsync(null, body, ct));
        admin.MapPut("/categories/{id:int}", (int id, CategoryRequest body, ManagementService service, CancellationToken ct) => service.SaveCategoryAsync(id, body, ct));
        admin.MapDelete("/categories/{id:int}", async (int id, ManagementService service, CancellationToken ct) =>
        {
            await service.DisableCategoryAsync(id, ct);
            return Results.NoContent();
        });

        admin.MapGet("/products", (int? page, int? pageSize, string? search, ManagementService service, CancellationToken ct) =>
            service.ProductsAsync(page, pageSize, search, ct));
        admin.MapGet("/products/{id:int}", (int id, CatalogService service, CancellationToken ct) => service.DetailsAsync(id, true, ct));
        admin.MapPost("/products", async (ProductRequest body, ManagementService service, CatalogService catalog, CancellationToken ct) =>
            await catalog.DetailsAsync(await service.SaveProductAsync(null, body, ct), true, ct));
        admin.MapPut("/products/{id:int}", async (int id, ProductRequest body, ManagementService service, CatalogService catalog, CancellationToken ct) =>
            await catalog.DetailsAsync(await service.SaveProductAsync(id, body, ct), true, ct));
        admin.MapDelete("/products/{id:int}", async (int id, ManagementService service, CancellationToken ct) =>
        {
            await service.DisableProductAsync(id, ct);
            return Results.NoContent();
        });

        admin.MapGet("/toppings", (ManagementService service, CancellationToken ct) => service.ToppingsAsync(ct));
        admin.MapPost("/toppings", (ToppingRequest body, ManagementService service, CancellationToken ct) => service.SaveToppingAsync(null, body, ct));
        admin.MapPut("/toppings/{id:int}", (int id, ToppingRequest body, ManagementService service, CancellationToken ct) => service.SaveToppingAsync(id, body, ct));
        admin.MapDelete("/toppings/{id:int}", async (int id, ManagementService service, CancellationToken ct) =>
        {
            await service.DisableToppingAsync(id, ct);
            return Results.NoContent();
        });

        admin.MapGet("/coupons", (ManagementService service, CancellationToken ct) => service.CouponsAsync(ct));
        admin.MapPost("/coupons", (CouponRequest body, ManagementService service, CancellationToken ct) => service.SaveCouponAsync(null, body, ct));
        admin.MapPut("/coupons/{id:int}", (int id, CouponRequest body, ManagementService service, CancellationToken ct) => service.SaveCouponAsync(id, body, ct));
        admin.MapDelete("/coupons/{id:int}", async (int id, ManagementService service, CancellationToken ct) =>
        {
            await service.DisableCouponAsync(id, ct);
            return Results.NoContent();
        });

        admin.MapGet("/banners", (ManagementService service, CancellationToken ct) => service.BannersAsync(ct));
        admin.MapPost("/banners", (BannerRequest body, ManagementService service, CancellationToken ct) => service.SaveBannerAsync(null, body, ct));
        admin.MapPut("/banners/{id:int}", (int id, BannerRequest body, ManagementService service, CancellationToken ct) => service.SaveBannerAsync(id, body, ct));
        admin.MapDelete("/banners/{id:int}", async (int id, ManagementService service, CancellationToken ct) =>
        {
            await service.DeleteBannerAsync(id, ct);
            return Results.NoContent();
        });

        admin.MapGet("/reviews", (int? page, int? pageSize, bool? visible, ManagementService service, CancellationToken ct) =>
            service.ReviewsAsync(page, pageSize, visible, ct));
        admin.MapPut("/reviews/{id:int}/visibility", async (int id, ReviewVisibilityRequest body, ManagementService service, CancellationToken ct) =>
        {
            await service.SetReviewVisibilityAsync(id, body.IsVisible, ct);
            return Results.NoContent();
        });
        admin.MapDelete("/reviews/{id:int}", async (int id, ManagementService service, CancellationToken ct) =>
        {
            await service.DeleteReviewAsync(id, ct);
            return Results.NoContent();
        });

        admin.MapPost("/images", async (HttpRequest request, IImageStore images, CancellationToken ct) =>
        {
            if (request.ContentLength is not long length)
                throw new BusinessException(411, "Vui lòng gửi kích thước tệp ảnh.");
            return new UploadedImage(await images.SaveAsync(request.Body, length, ct));
        });
        admin.MapGet("/dashboard", (DateOnly? from, DateOnly? to, bool? byMonth, ReportService service, CancellationToken ct) =>
            service.DashboardAsync(from, to, byMonth ?? false, ct));
        admin.MapGet("/reports/revenue.xlsx", async (DateOnly? from, DateOnly? to, bool? byMonth,
            ReportService service, IRevenueWorkbook workbook, CancellationToken ct) =>
        {
            var dashboard = await service.DashboardAsync(from, to, byMonth ?? false, ct);
            return Results.File(workbook.Create(dashboard),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "TiemChe-DoanhThu.xlsx");
        });
    }
}
