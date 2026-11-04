using Microsoft.EntityFrameworkCore;
using TiemChe.Application.Interfaces;
using TiemChe.Application.Validation;
using TiemChe.Contracts.Catalog;
using TiemChe.Contracts.Shopping;
using TiemChe.Domain.Models;

namespace TiemChe.Application.Services;

public sealed class CustomerService(ITiemCheData data)
{
    public async Task<IReadOnlyList<AddressDto>> AddressesAsync(int userId, CancellationToken ct) =>
        await data.Addresses.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.IsDefault).ThenBy(x => x.Id)
            .Select(x => new AddressDto(x.Id, x.RecipientName, x.Phone, x.AddressLine, x.IsDefault)).ToListAsync(ct);

    public async Task<AddressDto> SaveAddressAsync(int userId, int? id, AddressRequest request, CancellationToken ct)
    {
        var name = Check.Text(request.RecipientName, 120, "người nhận");
        var phone = Check.Text(request.Phone, 24, "số điện thoại");
        var line = Check.Text(request.AddressLine, 500, "địa chỉ");
        if (phone.Count(char.IsAsciiDigit) is < 8 or > 15
            || phone.Any(c => !char.IsAsciiDigit(c) && c is not ('+' or ' ' or '-' or '(' or ')')))
            throw Check.Invalid("Số điện thoại giao hàng không hợp lệ.");

        await using var transaction = await data.BeginWriteAsync(ct);
        if (request.IsDefault)
        {
            await data.Addresses.Where(x => x.UserId == userId && x.IsDefault && x.Id != id)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsDefault, false), ct);
        }
        Address address;
        if (id.HasValue)
        {
            address = await data.Addresses.SingleOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct)
                ?? throw Check.Missing("Không tìm thấy địa chỉ.");
            address.RecipientName = name;
            address.Phone = phone;
            address.AddressLine = line;
        }
        else
        {
            address = new Address { UserId = userId, RecipientName = name, Phone = phone, AddressLine = line };
            data.Addresses.Add(address);
        }
        address.IsDefault = request.IsDefault;
        await data.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new AddressDto(address.Id, name, phone, line, address.IsDefault);
    }

    public async Task DeleteAddressAsync(int userId, int id, CancellationToken ct)
    {
        if (await data.Addresses.Where(x => x.Id == id && x.UserId == userId).ExecuteDeleteAsync(ct) == 0)
            throw Check.Missing("Không tìm thấy địa chỉ.");
    }

    public async Task<IReadOnlyList<ProductCard>> WishlistAsync(int userId, CancellationToken ct) =>
        await data.Wishlists.AsNoTracking()
            .Where(x => x.UserId == userId && x.Product.IsActive && x.Product.Category.IsActive
                && x.Product.Sizes.Any(s => s.IsActive))
            .OrderByDescending(x => x.AddedAtUtc).ThenByDescending(x => x.ProductId)
            .Select(x => new ProductCard(x.ProductId, x.Product.CategoryId, x.Product.Name, x.Product.ImagePath,
                x.Product.Sizes.Where(s => s.IsActive).Min(s => s.PriceVnd))).ToListAsync(ct);

    public async Task SetWishlistAsync(int userId, int productId, bool saved, CancellationToken ct)
    {
        if (!saved)
        {
            await data.Wishlists.Where(x => x.UserId == userId && x.ProductId == productId).ExecuteDeleteAsync(ct);
            return;
        }
        await using var transaction = await data.BeginWriteAsync(ct);
        if (!await data.Products.AnyAsync(x => x.Id == productId && x.IsActive && x.Category.IsActive
                && x.Sizes.Any(s => s.IsActive), ct))
            throw Check.Missing("Không tìm thấy món.");
        if (!await data.Wishlists.AnyAsync(x => x.UserId == userId && x.ProductId == productId, ct))
        {
            data.Wishlists.Add(new Wishlist { UserId = userId, ProductId = productId });
            await data.SaveChangesAsync(ct);
        }
        await transaction.CommitAsync(ct);
    }
}
