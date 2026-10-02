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

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProductViewModel product)
        {
            var baseUrl = _configuration["ApiSettings:BaseUrl"];

            var response = await _httpClient.PostAsJsonAsync(
                $"{baseUrl}/api/products",
                product
            );
            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var baseUrl = _configuration["ApiSettings:BaseUrl"];
            var product = await _httpClient.GetFromJsonAsync<ProductViewModel>(
                $"{baseUrl}/api/products/{id}"
            );
            if (product == null)
            {
                return NotFound();

            }
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ProductViewModel product)
        {
            var baseUrl = _configuration["ApiSettings:BaseUrl"];
            var response = await _httpClient.PutAsJsonAsync(
                $"{baseUrl}/api/products/{product.Id}",
                product
            );
            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }
        
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var baseUrl = _configuration["ApiSettings:BaseUrl"];
            var response = await _httpClient.DeleteAsync(
                $"{baseUrl}/api/products/{id}"
            );
            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction(nameof(Index));
        }
    }

}