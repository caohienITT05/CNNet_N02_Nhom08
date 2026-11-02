using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace SaigonChe.Web.ViewModels
{
    public class ProductFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên món.")]
        [StringLength(120)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập mô tả món.")]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Range(1000, 1000000, ErrorMessage = "Giá phải từ 1.000 đến 1.000.000 VNĐ.")]
        public decimal Price { get; set; }

        public bool IsAvailable { get; set; } = true;
        public bool IsFeatured { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn danh mục.")]
        public int CategoryId { get; set; }
        public string? ImageUrl { get; set; }
        public IFormFile? ImageFile { get; set; }
        public IEnumerable<SelectListItem>? Categories { get; set; }
    }
}
