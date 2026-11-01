namespace SaigonChe.Web.ViewModels
{
    public class FavoriteProductViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }
        public bool IsAvailable { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }
}
