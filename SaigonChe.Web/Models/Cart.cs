using System;
using System.Collections.Generic;

namespace SaigonChe.Web.Models
{
    public class Cart
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Null khi là khách vãng lai (Guest)
        public string? UserId { get; set; }

        // Session ID cho khách chưa đăng nhập
        public string? SessionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
    }
}