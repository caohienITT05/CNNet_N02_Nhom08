using Microsoft.AspNetCore.Http;
namespace SaigonChe.Web.Services;

public sealed class ProductImageException(string message) : Exception(message);

public sealed class ProductImageService(IWebHostEnvironment environment, ILogger<ProductImageService> logger)
{
    private const string Prefix = "/uploads/products/";
    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file.Length is < 12 or > 5 * 1024 * 1024)
            throw new ProductImageException("Ảnh phải hợp lệ và có dung lượng tối đa 5 MiB.");
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var header = new byte[12];
        await using (var source = file.OpenReadStream())
            await source.ReadAtLeastAsync(header, header.Length, false, cancellationToken);
        var mime = file.ContentType.ToLowerInvariant();
        var valid = extension switch
        {
            ".jpg" or ".jpeg" => mime == "image/jpeg" && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff,
            ".png" => mime == "image/png" && header.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}),
            ".webp" => mime == "image/webp" && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF"
                && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP",
            _ => false
        };
        if (!valid) throw new ProductImageException("Chỉ chấp nhận ảnh JPG, PNG hoặc WebP có nội dung phù hợp.");
        var directory = Path.Combine(environment.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(directory);
        var name = Guid.NewGuid().ToString("N") + extension;
        var path = Path.Combine(directory, name);
        try
        {
            await using var destination = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
            await file.CopyToAsync(destination, cancellationToken);
            return Prefix + name;
        }
        catch
        {
            Delete(Prefix + name);
            throw;
        }
    }

    public void Delete(string? url)
    {
        if (url is null || !url.StartsWith(Prefix, StringComparison.Ordinal)) return;
        var name = url[Prefix.Length..];
        if (name.Length == 0 || name.Contains('/') || name.Contains('\\') || name != Path.GetFileName(name)) return;
        try { File.Delete(Path.Combine(environment.WebRootPath, "uploads", "products", name)); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning("Could not remove an obsolete product image ({ErrorType}).", exception.GetType().Name);
        }
    }
}
