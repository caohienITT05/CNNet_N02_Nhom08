using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SaigonChe.Api.Data;
using SaigonChe.Api.Models;

namespace SaigonChe.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ProductsController(AppDbContext context)
        {
            _context = context;
        }
        

        [HttpGet]
        public async Task<ActionResult<List<Product>>> GetAll()
        {
            var products = await _context.Products
                .Include(p=> p.Category)
                .Select(p=>new
                {
                    p.Id,
                    p.Name,
                    p.Price,
                    p.Description,
                    p.IsAvailable,
                    p.ImageUrl,
                    p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null
                })
                .ToListAsync();
            return Ok(products);
        }
        [HttpGet("{id}")]
        public async Task<ActionResult<Product>> GetbyId(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Price,
                    p.Description,
                    p.IsAvailable,
                    p.ImageUrl,
                    p.CategoryId,
                    CategoryName = p.Category != null ? p.Category.Name : null
                })
                .FirstOrDefaultAsync();
            if (product == null)
            {
                return NotFound();
            }
            return Ok(product);
        }

    }
}
