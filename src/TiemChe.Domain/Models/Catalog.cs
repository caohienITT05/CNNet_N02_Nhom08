namespace TiemChe.Domain.Models;

public sealed class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Product
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public required string Name { get; set; }
    public required string Description { get; set; }
    public string? ImagePath { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<ProductSize> Sizes { get; } = new List<ProductSize>();
    public ICollection<ProductTopping> AllowedToppings { get; } = new List<ProductTopping>();
}

public sealed class ProductSize
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public required string Name { get; set; }
    public long PriceVnd { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Topping
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public long PriceVnd { get; set; }
    public string? ImagePath { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ProductTopping
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int ToppingId { get; set; }
    public Topping Topping { get; set; } = null!;
}

public sealed class Banner
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string ImagePath { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime? EndsAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
