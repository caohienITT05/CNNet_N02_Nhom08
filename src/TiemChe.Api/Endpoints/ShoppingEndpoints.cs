using System.Security.Claims;
using TiemChe.Api.Authorization;
using TiemChe.Application.Services;
using TiemChe.Contracts.Catalog;
using TiemChe.Contracts.Shopping;
using TiemChe.Domain.Enums;

namespace TiemChe.Api.Endpoints;

internal static class ShoppingEndpoints
{
    internal static void MapShopping(this IEndpointRouteBuilder api)
    {
        var catalog = api.MapGroup("/catalog");
        catalog.MapGet("/home", (CatalogService service, CancellationToken ct) => service.HomeAsync(ct));
        catalog.MapGet("/categories", (CatalogService service, CancellationToken ct) => service.CategoriesAsync(false, ct));
        catalog.MapGet("/products", (string? search, int? categoryId, long? minPrice, long? maxPrice,
            ProductSort? sort, int? page, int? pageSize, CatalogService service, CancellationToken ct) =>
            service.ListAsync(search, categoryId, minPrice, maxPrice, sort, page, pageSize, ct));
        catalog.MapGet("/products/{id:int}", (int id, CatalogService service, CancellationToken ct) =>
            service.DetailsAsync(id, false, ct));

        var customer = api.MapGroup("/customer").RequireAuthorization(nameof(RoleName.Customer));
        customer.MapGet("/addresses", (ClaimsPrincipal user, CustomerService service, CancellationToken ct) =>
            service.AddressesAsync(user.UserId(), ct));
        customer.MapPost("/addresses", (AddressRequest body, ClaimsPrincipal user, CustomerService service, CancellationToken ct) =>
            service.SaveAddressAsync(user.UserId(), null, body, ct));
        customer.MapPut("/addresses/{id:int}", (int id, AddressRequest body, ClaimsPrincipal user, CustomerService service, CancellationToken ct) =>
            service.SaveAddressAsync(user.UserId(), id, body, ct));
        customer.MapDelete("/addresses/{id:int}", async (int id, ClaimsPrincipal user, CustomerService service, CancellationToken ct) =>
        {
            await service.DeleteAddressAsync(user.UserId(), id, ct);
            return Results.NoContent();
        });
        customer.MapGet("/wishlist", (ClaimsPrincipal user, CustomerService service, CancellationToken ct) =>
            service.WishlistAsync(user.UserId(), ct));
        customer.MapPut("/wishlist/{id:int}", async (int id, ClaimsPrincipal user, CustomerService service, CancellationToken ct) =>
        {
            await service.SetWishlistAsync(user.UserId(), id, true, ct);
            return Results.NoContent();
        });
        customer.MapDelete("/wishlist/{id:int}", async (int id, ClaimsPrincipal user, CustomerService service, CancellationToken ct) =>
        {
            await service.SetWishlistAsync(user.UserId(), id, false, ct);
            return Results.NoContent();
        });
        customer.MapPost("/products/{id:int}/reviews", (int id, ReviewRequest body, ClaimsPrincipal user, CatalogService service, CancellationToken ct) =>
            service.ReviewAsync(user.UserId(), id, body, ct));

        var cart = customer.MapGroup("/cart");
        cart.MapGet("", (ClaimsPrincipal user, CartService service, CancellationToken ct) => service.GetAsync(user.UserId(), ct));
        cart.MapPost("/items", (AddCartItemRequest body, ClaimsPrincipal user, CartService service, CancellationToken ct) =>
            service.AddAsync(user.UserId(), body, ct));
        cart.MapPut("/items/{id:int}", (int id, CartQuantityRequest body, ClaimsPrincipal user, CartService service, CancellationToken ct) =>
            service.SetQuantityAsync(user.UserId(), id, body, ct));
        cart.MapDelete("/items/{id:int}", (int id, Guid version, ClaimsPrincipal user, CartService service, CancellationToken ct) =>
            service.RemoveAsync(user.UserId(), id, version, ct));

        customer.MapGet("/checkout/options", (CheckoutSettings settings) =>
            new { settings.ShippingFeeVnd, settings.AllowsBankTransfer });
        customer.MapPost("/checkout/quote", (CheckoutRequest body, ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
            service.QuoteAsync(user.UserId(), body, ct));
        customer.MapPost("/checkout", (CheckoutRequest body, ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
            service.CheckoutAsync(user.UserId(), body, ct));

        var orders = customer.MapGroup("/orders");
        orders.MapGet("", (OrderStatus? status, string? search, int? page, int? pageSize,
            ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
            service.ListAsync(user.UserId(), status, search, page, pageSize, ct));
        orders.MapGet("/{id:int}", (int id, ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
            service.GetAsync(id, user.UserId(), ct));
        orders.MapPost("/{id:int}/cancel", (int id, VersionRequest body, ClaimsPrincipal user, OrderService service, CancellationToken ct) =>
            service.CancelAsync(id, user.UserId(), body, ct));
        orders.MapPost("/{id:int}/reorder", (int id, ReorderRequest body, ClaimsPrincipal user, CartService service, CancellationToken ct) =>
            service.ReorderAsync(user.UserId(), id, body, ct));
    }
}
