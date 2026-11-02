using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.Models
{
    public class ApplicationUser : IdentityUser
    {
        [StringLength(100)]
        public string? FullName { get; set; }

        public ICollection<Order> AssignedOrders { get; set; } = new List<Order>();
        public ICollection<Order> CustomerOrders { get; set; } = new List<Order>();
        public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
        public ICollection<Review> CustomerReviews { get; set; } = new List<Review>();
        public ICollection<Review> RepliedReviews { get; set; } = new List<Review>();
    }
}
