namespace SaigonChe.Web.ViewModels
{
    public class LandingPageViewModel
    {
        public List<ProductViewModel> Products { get; set; } = new();
        public List<ProductViewModel> BestSellers { get; set; } = new();
        public List<string> Categories { get; set; } = new();
        public List<TestimonialViewModel> Testimonials { get; set; } = new();
    }
}
