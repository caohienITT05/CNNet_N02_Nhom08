using Microsoft.AspNetCore.Mvc;
using SaigonChe.Web.ViewModels;
using System.Net.Http.Json;

namespace SaigonChe.Web.Controllers
{
    public class CategoryController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public CategoryController(
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
            var categories = await _httpClient.GetFromJsonAsync<List<CategoryViewModel>>(
                $"{baseUrl}/api/categories"
            ) ?? new List<CategoryViewModel>();
            return View(categories);

        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Create(CategoryViewModel category)
        {
            var baseUrl = _configuration["ApiSettings:BaseUrl"];
            var response = await _httpClient.PostAsJsonAsync(
                $"{baseUrl}/api/categories",category);
            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var baseUrl = _configuration["ApiSettings:BaseUrl"];
            var category = await _httpClient.GetFromJsonAsync<CategoryViewModel>(
                $"{baseUrl}/api/categories/{id}"
            );
            if (category == null)
            {
                return NotFound();
            }
            return View(category);
        }
        [HttpPost]
        public async Task<IActionResult> Edit(CategoryViewModel category)
        {
            var baseUrl = _configuration["ApiSettings:BaseUrl"];
            var response = await _httpClient.PutAsJsonAsync(
                $"{baseUrl}/api/categories/{category.Id}", category);
            if (response.IsSuccessStatusCode)
            {
                return RedirectToAction(nameof(Index));
            }
            return View(category);
        }
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var baseUrl = _configuration["ApiSettings:BaseUrl"];
            var response = await _httpClient.DeleteAsync(
                $"{baseUrl}/api/categories/{id}");
            return RedirectToAction(nameof(Index));
        }
    }
}
