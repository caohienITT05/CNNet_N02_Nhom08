using SaigonChe.Web.Models;

namespace SaigonChe.Web.ViewModels
{
    public class StaffOrdersViewModel
    {
        public OrderStatus? FilterStatus { get; set; }

        public List<Order> Orders { get; set; } = new();
    }
}
