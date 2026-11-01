using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.ViewModels
{
    public class MenuIndexViewModel
    {
        public string? Search { get; set; }
        public int? CategoryId { get; set; }

        [Range(0, 1000000, ErrorMessage = "Giá từ phải nằm trong khoảng 0 đến 1.000.000 đồng.")]
        public decimal? MinPrice { get; set; }

        [Range(0, 1000000, ErrorMessage = "Giá đến phải nằm trong khoảng 0 đến 1.000.000 đồng.")]
        public decimal? MaxPrice { get; set; }

        public string Status { get; set; } = "all";
        public string Sort { get; set; } = "name";

        [Range(1, int.MaxValue, ErrorMessage = "Số trang không hợp lệ.")]
        public int Page { get; set; } = 1;

        public int TotalProducts { get; set; }
        public int TotalPages { get; set; } = 1;
        public List<MenuCategoryOptionViewModel> Categories { get; set; } = new();
        public List<MenuProductItemViewModel> Products { get; set; } = new();
    }

    public class MenuCategoryOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class MenuProductItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsFavorite { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class MenuProductDetailsViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public bool IsFavorite { get; set; }
        public string? ImageUrl { get; set; }
        public ProductReviewsViewModel Reviews { get; set; } = new();
    }
}
