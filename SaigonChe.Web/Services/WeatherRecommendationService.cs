using SaigonChe.Web.Models;
using SaigonChe.Web.ViewModels;

namespace SaigonChe.Web.Services
{
    public static class WeatherRecommendationService
    {
        private static readonly IReadOnlyDictionary<WeatherCondition, string[]> PreferredProducts =
            new Dictionary<WeatherCondition, string[]>
            {
                [WeatherCondition.Hot] = ["khúc bạch", "chè thái", "chè bưởi", "dừa dầm", "ba màu"],
                [WeatherCondition.Cool] = ["đậu xanh", "khoai dẻo", "ba màu", "sầu riêng"],
                [WeatherCondition.Rainy] = ["đậu xanh", "khoai dẻo", "ba màu"]
            };

        public static List<ProductViewModel> GetRecommendations(
            IEnumerable<ProductViewModel> products,
            WeatherCondition condition)
        {
            var preferredNames = PreferredProducts[condition];

            return products
                .OrderByDescending(product => GetPreferenceScore(product.Name, preferredNames))
                .ThenByDescending(product => product.IsFeatured)
                .ThenBy(product => product.Name)
                .Take(3)
                .ToList();
        }

        public static string GetDescription(WeatherCondition condition)
        {
            return condition switch
            {
                WeatherCondition.Hot => "Món thanh mát, dùng lạnh giúp giải nhiệt ngày nắng.",
                WeatherCondition.Cool => "Món chè béo bùi, đậm vị hợp với tiết trời mát.",
                WeatherCondition.Rainy => "Món chè ấm bụng, thơm bùi cho ngày mưa.",
                _ => throw new ArgumentOutOfRangeException(nameof(condition), condition, "Tình trạng thời tiết không hợp lệ.")
            };
        }

        private static int GetPreferenceScore(string productName, IReadOnlyList<string> preferredNames)
        {
            for (var index = 0; index < preferredNames.Count; index++)
            {
                if (productName.Contains(preferredNames[index], StringComparison.OrdinalIgnoreCase))
                {
                    return preferredNames.Count - index;
                }
            }

            return 0;
        }
    }
}
