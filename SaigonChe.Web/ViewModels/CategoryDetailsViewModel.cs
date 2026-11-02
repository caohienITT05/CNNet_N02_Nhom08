namespace SaigonChe.Web.ViewModels
{
    public class CategoryDetailsViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<AdminProductListItemViewModel> Products { get; set; } = new();
    }
}
