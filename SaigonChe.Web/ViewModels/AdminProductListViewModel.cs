namespace SaigonChe.Web.ViewModels
{
    public class AdminProductListViewModel
    {
        public string? Search { get; set; }
        public int? CategoryId { get; set; }
        public bool? IsAvailable { get; set; }
        public int Page { get; set; }
        public int TotalPages { get; set; }
        public int TotalProducts { get; set; }
        public List<AdminProductListItemViewModel> Products { get; set; } = new();
        public List<CategoryOptionViewModel> Categories { get; set; } = new();
    }

    public class AdminProductListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public decimal Price { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsFeatured { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class CategoryOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
