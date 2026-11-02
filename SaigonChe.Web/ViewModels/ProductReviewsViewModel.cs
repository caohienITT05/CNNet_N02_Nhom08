namespace SaigonChe.Web.ViewModels
{
    public class ProductReviewsViewModel
    {
        public int ProductId { get; set; }
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }
        public bool IsAuthenticated { get; set; }
        public ReviewFormViewModel? CurrentUserReview { get; set; }
        public List<ProductReviewItemViewModel> Reviews { get; set; } = new();
    }

    public class ProductReviewItemViewModel
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? AdminReply { get; set; }
        public DateTime? RepliedAt { get; set; }
        public bool IsCurrentUserReview { get; set; }
    }
}
