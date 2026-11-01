namespace SaigonChe.Web.ViewModels
{
    public class AdminDashboardViewModel
    {
        public DateOnly FromDate { get; set; }
        public DateOnly ToDate { get; set; }
        public decimal Revenue { get; set; }
        public int CompletedOrderCount { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int ProcessingOrderCount { get; set; }
        public int NewReviewCount { get; set; }
        public int CustomerCount { get; set; }
        public List<DailyRevenueViewModel> DailyRevenue { get; set; } = new();
        public List<PopularProductViewModel> PopularProducts { get; set; } = new();
    }

    public class DailyRevenueViewModel
    {
        public DateOnly Date { get; set; }
        public decimal Revenue { get; set; }
    }

    public class PopularProductViewModel
    {
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Revenue { get; set; }
    }
}
