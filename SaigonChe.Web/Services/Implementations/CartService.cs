using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Web.Data;
using SaigonChe.Web.Extensions;
using SaigonChe.Web.Interfaces;
using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Services.Implementations
{
    public class CartService : ICartService
    {
        private const string SessionCartKey = "SAIGONCHE_SESSION_CART";
        private const string SessionCouponKey = "SAIGONCHE_APPLIED_COUPON";

        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CartService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        private ISession? Session => _httpContextAccessor.HttpContext?.Session;
        private string? CurrentUserId => _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);

        public async Task<CartViewModel> GetCartAsync()
        {
            var cartVm = new CartViewModel();
            var userId = CurrentUserId;

            if (string.IsNullOrEmpty(userId))
            {
                if (Session != null)
                {
                    var sessionItems = Session.GetObjectFromJson<List<CartItemViewModel>>(SessionCartKey) ?? new List<CartItemViewModel>();
                    var productIds = sessionItems.Select(i => i.ProductId).ToList();
                    var products = await _context.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

                    foreach (var item in sessionItems)
                    {
                        if (products.TryGetValue(item.ProductId, out var product) && product.IsAvailable)
                        {
                            item.UnitPrice = product.Price;
                            item.ProductName = product.Name;
                            item.ImageUrl = product.ImageUrl ?? "/images/default-che.jpg";
                            cartVm.Items.Add(item);
                        }
                    }
                }
            }
            else
            {
                var userCart = await _context.Carts
                    .Include(c => c.Items)
                    .ThenInclude(ci => ci.Product)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (userCart != null)
                {
                    cartVm.Items = userCart.Items
                        .Where(ci => ci.Product != null && ci.Product.IsAvailable)
                        .Select(ci => new CartItemViewModel
                        {
                            ProductId = ci.ProductId,
                            ProductName = ci.Product!.Name,
                            ImageUrl = ci.Product.ImageUrl ?? "/images/default-che.jpg",
                            UnitPrice = ci.Product.Price,
                            Quantity = ci.Quantity
                        }).ToList();
                }
            }

            if (Session != null)
            {
                string? appliedCoupon = Session.GetString(SessionCouponKey);
                if (!string.IsNullOrEmpty(appliedCoupon))
                {
                    cartVm.CouponCode = appliedCoupon;
                    cartVm.DiscountAmount = await CalculateDiscountAsync(appliedCoupon, cartVm.SubTotal);
                }
            }

            return cartVm;
        }

        public async Task<bool> AddToCartAsync(int productId, int quantity)
        {
            if (quantity <= 0) return false;

            var product = await _context.Products.FindAsync(productId);
            if (product == null || !product.IsAvailable) return false;

            var userId = CurrentUserId;

            if (string.IsNullOrEmpty(userId))
            {
                if (Session == null) return false;

                var items = Session.GetObjectFromJson<List<CartItemViewModel>>(SessionCartKey) ?? new List<CartItemViewModel>();
                var item = items.FirstOrDefault(i => i.ProductId == productId);

                if (item != null)
                {
                    item.Quantity += quantity;
                }
                else
                {
                    items.Add(new CartItemViewModel
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        ImageUrl = product.ImageUrl ?? "/images/default-che.jpg",
                        UnitPrice = product.Price,
                        Quantity = quantity
                    });
                }
                Session.SetObjectAsJson(SessionCartKey, items);
            }
            else
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null)
                {
                    cart = new Cart { UserId = userId };
                    _context.Carts.Add(cart);
                }

                var cartItem = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                if (cartItem != null)
                {
                    cartItem.Quantity += quantity;
                }
                else
                {
                    cart.Items.Add(new CartItem
                    {
                        ProductId = productId,
                        Quantity = quantity,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> UpdateQuantityAsync(int productId, int quantity)
        {
            var userId = CurrentUserId;

            if (string.IsNullOrEmpty(userId))
            {
                if (Session == null) return false;

                var items = Session.GetObjectFromJson<List<CartItemViewModel>>(SessionCartKey) ?? new List<CartItemViewModel>();
                var item = items.FirstOrDefault(i => i.ProductId == productId);
                if (item == null) return false;

                if (quantity <= 0)
                {
                    items.Remove(item);
                }
                else
                {
                    item.Quantity = quantity;
                }
                Session.SetObjectAsJson(SessionCartKey, items);
            }
            else
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null) return false;

                var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                if (item == null) return false;

                if (quantity <= 0)
                {
                    _context.CartItems.Remove(item);
                }
                else
                {
                    item.Quantity = quantity;
                }
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return true;
        }

        public async Task<bool> RemoveFromCartAsync(int productId)
        {
            return await UpdateQuantityAsync(productId, 0);
        }

        public async Task<bool> ApplyCouponAsync(string couponCode)
        {
            var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == couponCode && c.IsActive);
            if (coupon == null || coupon.EndDate < DateTime.UtcNow || coupon.UsedCount >= coupon.UsageLimit)
            {
                return false;
            }

            Session?.SetString(SessionCouponKey, coupon.Code);
            return true;
        }

        public async Task ClearCartAsync()
        {
            Session?.Remove(SessionCartKey);
            Session?.Remove(SessionCouponKey);

            var userId = CurrentUserId;
            if (!string.IsNullOrEmpty(userId))
            {
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart != null)
                {
                    _context.CartItems.RemoveRange(cart.Items);
                    await _context.SaveChangesAsync();
                }
            }
        }

        public async Task MergeSessionCartToUserAsync(string userId)
        {
            if (Session == null) return;

            var sessionItems = Session.GetObjectFromJson<List<CartItemViewModel>>(SessionCartKey);
            if (sessionItems == null || !sessionItems.Any()) return;

            var userCart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (userCart == null)
            {
                userCart = new Cart { UserId = userId };
                _context.Carts.Add(userCart);
            }

            foreach (var item in sessionItems)
            {
                var existingItem = userCart.Items.FirstOrDefault(i => i.ProductId == item.ProductId);
                if (existingItem != null)
                {
                    existingItem.Quantity += item.Quantity;
                }
                else
                {
                    userCart.Items.Add(new CartItem
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
            Session.Remove(SessionCartKey);
        }

        private async Task<decimal> CalculateDiscountAsync(string code, decimal subTotal)
        {
            var coupon = await _context.Coupons.FirstOrDefaultAsync(c => c.Code == code && c.IsActive);
            if (coupon == null || subTotal < coupon.MinOrderAmount) return 0;

            decimal discount = 0;
            if (coupon.DiscountType == DiscountType.Percentage)
            {
                discount = subTotal * (coupon.DiscountValue / 100m);
                if (coupon.MaxDiscountAmount.HasValue && discount > coupon.MaxDiscountAmount.Value)
                {
                    discount = coupon.MaxDiscountAmount.Value;
                }
            }
            else
            {
                discount = coupon.DiscountValue;
            }

            return discount;
        }
    }
}