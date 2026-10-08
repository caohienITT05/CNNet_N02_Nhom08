using System.Text.Json.Serialization;
namespace SaigonChe.Web.ViewModels
{
    public class ProductViewModel
    {
        public int Id {get; set;}
        public string Name{get; set;} = string.Empty;
        public decimal Price {get; set;}
        public string Description {get; set;} = string.Empty;
        public bool IsAvailable {get; set;}
        public int? CategoryId { get; set; }
        public string? ImageUrl { get; set; }
        [JsonIgnore]
        public IFormFile? ImageFile { get; set; }
    }
}