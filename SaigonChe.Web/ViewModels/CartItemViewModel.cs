namespace SaigonChe.Web.ViewModels
{
    public class CartItemViewModel
    {
        public int ProductId { get; set; }

        // Tương thích cả Name (nhóm dùng) và ProductName
        public string Name { get; set; } = string.Empty;
        public string ProductName
        {
            get => Name;
            set => Name = value;
        }

        public string ImageUrl { get; set; } = string.Empty;

        // Tương thích cả Price (nhóm dùng) và UnitPrice
        public decimal Price { get; set; }
        public decimal UnitPrice
        {
            get => Price;
            set => Price = value;
        }

        public int Quantity { get; set; }

        // Tương thích cả LineTotal (CheckoutViewModel & View dùng) và TotalPrice
        public decimal LineTotal => Price * Quantity;
        public decimal TotalPrice => LineTotal;
    }
}