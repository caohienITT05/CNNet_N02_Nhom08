using Microsoft.AspNetCore.Mvc;
using SaigonChe.Web.ViewModels;
using System.Net.Http.Json;

namespace SaigonChe.web.Controllers
{
    public class ProductController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public ProductController(
            HttpClient httpClient,
            IConfiguration configuration
        )
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }
        
        public async Task<IActionResult> Index()
        {
            var baseUrl = _configuration["ApiSettings:BaseUrl"];

            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new Exception("Không tìm thấy ApiSettings:BaseUrl trong appsettings.json");
            }

            var products = await _httpClient
                .GetFromJsonAsync<List<ProductViewModel>>(
                    $"{baseUrl}/api/products"
                );

            return View(products);
        }
    }
}