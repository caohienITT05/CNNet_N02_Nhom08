using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SaigonChe.Web.ViewModels;
using System.Buffers.Text;
using System.Net.Http.Json;
namespace SaigonChe.web.Controllers
{
    public class ProductController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public ProductController(
            HttpClient httpClient,
            IConfiguration configuration,
            IWebHostEnvironment environment
        )
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _environment = environment;
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
        public async Task<IActionResult> Create()
        {
            var baseUrl = _configuration["ApiSettings:BaseUrl"];
            var categories = 
            await _httpClient.GetFromJsonAsync<List<CategoryViewModel>>(
                $"{baseUrl}/api/categories"
                ) ?? new List<CategoryViewModel>();
        
            ViewBag.Categories = new SelectList(categories, "Id", "Name");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProductViewModel product)
        {
            var baseUrl = _configuration["ApiSettings:BaseUrl"];
            if (product.ImageFile is { Length:> 0 })
            {
                var allowedExtensions = new[] {".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(product.ImageFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(
                        nameof(product.ImageFile),
                        "Chỉ chấp nhận JPG, JPEG, PNG, WEBP"
                    );
                }
                else if(product.ImageFile.Length >5*1024*1024)
                {
                    ModelState.AddModelError(
                        nameof(product.ImageFile),
                        "Dung lượng ảnh tối đa là 5MB"
                    );
                }
                else
                {
                    var uploadDirectory = Path.Combine(
                        _environment.WebRootPath,
                        "uploads",
                        "products"
                    );
                    Directory.CreateDirectory(uploadDirectory);
                    var fileName = $"{Guid.NewGuid():N}{extension}";
                    var filePath = Path.Combine(uploadDirectory, fileName);
                    await using var stream = new FileStream(filePath, FileMode.Create);
                    await product.ImageFile.CopyToAsync(stream);
                    product.ImageUrl = $"/uploads/products/{fileName}";
                }
            }
            if (!ModelState.IsValid)
            {
                var categories = 
                    await _httpClient.GetFromJsonAsync<List<CategoryViewModel>>(
                        $"{baseUrl}/api/categories"
                    ) ?? [];
                ViewBag.Categories = new SelectList(categories, "Id", "Name");
                return View(product);
            }
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

            var categories =
                await _httpClient.GetFromJsonAsync<List<CategoryViewModel>>(
                    $"{baseUrl}/api/categories"
                ) ?? [];
            ViewBag.Categories = new SelectList(
                categories,
                "Id",
                "Name",
                product.CategoryId
            );

            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ProductViewModel product)
        {
            var baseUrl = _configuration["ApiSettings:BaseUrl"];
            var currentProduct = await _httpClient.GetFromJsonAsync<ProductViewModel>(
                $"{baseUrl}/api/products/{product.Id}"
            );

            if (currentProduct == null)
            {
                return NotFound();
            }

            var oldImageUrl = currentProduct.ImageUrl;
            string? newImageUrl = null;
            product.ImageUrl = oldImageUrl;

            if (product.ImageFile is { Length: > 0 })
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(product.ImageFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    ModelState.AddModelError(
                        nameof(product.ImageFile),
                        "Chỉ chấp nhận JPG, JPEG, PNG, WEBP"
                    );
                }
                else if (product.ImageFile.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError(
                        nameof(product.ImageFile),
                        "Dung lượng ảnh tối đa là 5MB"
                    );
                }
                else
                {
                    var uploadDirectory = Path.Combine(
                        _environment.WebRootPath,
                        "uploads",
                        "products"
                    );
                    Directory.CreateDirectory(uploadDirectory);

                    var fileName = $"{Guid.NewGuid():N}{extension}";
                    var filePath = Path.Combine(uploadDirectory, fileName);

                    await using var stream = new FileStream(filePath, FileMode.Create);
                    await product.ImageFile.CopyToAsync(stream);

                    newImageUrl = $"/uploads/products/{fileName}";
                    product.ImageUrl = newImageUrl;
                }
            }

            if (!ModelState.IsValid)
            {
                if (newImageUrl != null)
                {
                    DeleteLocalProductImage(newImageUrl);
                    product.ImageUrl = oldImageUrl;
                }

                await LoadCategoriesAsync(baseUrl, product.CategoryId);
                return View(product);
            }

            var response = await _httpClient.PutAsJsonAsync(
                $"{baseUrl}/api/products/{product.Id}",
                product
            );
            if (response.IsSuccessStatusCode)
            {
                if (newImageUrl != null)
                {
                    DeleteLocalProductImage(oldImageUrl);
                }

                return RedirectToAction(nameof(Index));
            }

            if (newImageUrl != null)
            {
                DeleteLocalProductImage(newImageUrl);
                product.ImageUrl = oldImageUrl;
            }

            ModelState.AddModelError(string.Empty, "Không thể cập nhật sản phẩm.");
            await LoadCategoriesAsync(baseUrl, product.CategoryId);
            return View(product);
        }

        private async Task LoadCategoriesAsync(string? baseUrl, int? selectedCategoryId)
        {
            var categories =
                await _httpClient.GetFromJsonAsync<List<CategoryViewModel>>(
                    $"{baseUrl}/api/categories"
                ) ?? [];

            ViewBag.Categories = new SelectList(
                categories,
                "Id",
                "Name",
                selectedCategoryId
            );
        }

        private void DeleteLocalProductImage(string? imageUrl)
        {
            const string localImagePrefix = "/uploads/products/";

            if (string.IsNullOrWhiteSpace(imageUrl) ||
                !imageUrl.StartsWith(localImagePrefix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var fileName = Path.GetFileName(imageUrl);
            var filePath = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "products",
                fileName
            );

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
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
