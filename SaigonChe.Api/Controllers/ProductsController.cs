using Microsoft.AspNetCore.Mvc;
using SaigonChe.API.Models;

namespace SaigonChe.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private static readonly List<Product> Products = [
            new Product {
                Id = 1,
                Name = "Chè Thái",
                Price = 30000,
                Description = "chè thái từ sài gòn",
                IsAvailable = true
                
            },
            new Product {
                Id = 2,
                Name = "Chè Bưởi",
                Price = 25000,
                Description = "chè bưởi làm từ bưởi",
                IsAvailable = true
            }
        ];

        [HttpGet]
        public ActionResult<List<Product>> GetAll()
        {
            return Ok(Products);
        }
        [HttpGet("{id}")]
        public ActionResult<Product> GetbyId( int id)
        {
            var product = Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }
            return Ok(product);
        }

        [HttpPost]
        public ActionResult<Product> Create(Product product)
        {
            product.Id = Products.Max(p => p.Id) +1;
            Products.Add(product);
            return CreatedAtAction(
                nameof(GetbyId),
                new { id = product.Id},
                product
            );
        }

        [HttpPut("{id}")]
        public ActionResult<Product> Update(int id, Product updateProduct)
        {
            var product = Products.FirstOrDefault( p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }
            product.Name = updateProduct.Name;
            product.Price = updateProduct.Price;
            product.Description = updateProduct.Description;
            product.IsAvailable = updateProduct.IsAvailable;
            return Ok(product);
        }

        [HttpDelete("{id}")]
        public ActionResult<Product> Delete(int id)
        {
            var product = Products.FirstOrDefault( p => p.Id == id);
            if (product == null)
            {
                return NotFound();
            }
            Products.Remove(product);
            return NoContent();

        }

    }
}