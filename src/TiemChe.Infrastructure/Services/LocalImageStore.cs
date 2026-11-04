using TiemChe.Application.Interfaces;
using TiemChe.Application.Validation;

namespace TiemChe.Infrastructure.Services;

public sealed class LocalImageStore(string root) : IImageStore
{
    public const int MaxBytes = 5 * 1024 * 1024;
    public const string RequestPath = "/images";

    public async Task<string> SaveAsync(Stream source, long length, CancellationToken cancellationToken)
    {
        if (length is < 8 or > MaxBytes)
            throw new BusinessException(400, "Ảnh phải là PNG hoặc JPEG và có dung lượng tối đa 5 MB.");
        var bytes = new byte[(int)length];
        await source.ReadExactlyAsync(bytes, cancellationToken);
        byte[] png = [137, 80, 78, 71, 13, 10, 26, 10];
        var extension = bytes.AsSpan().StartsWith(png) ? ".png"
            : bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 ? ".jpg"
            : throw new BusinessException(400, "Định dạng ảnh phải là PNG hoặc JPEG.");
        var name = $"{Guid.NewGuid():N}{extension}";
        var tempPath = Path.Combine(root, name + ".tmp");
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken);
            File.Move(tempPath, Path.Combine(root, name));
        }
        finally
        {
            File.Delete(tempPath);
        }
        return $"{RequestPath}/{name}";
    }

    public bool Exists(string path)
    {
        if (!path.StartsWith(RequestPath + "/", StringComparison.Ordinal))
            return false;
        var name = path[(RequestPath.Length + 1)..];
        return name.Length == 36 && name[32..] is ".png" or ".jpg"
            && Guid.TryParseExact(name[..32], "N", out _) && File.Exists(Path.Combine(root, name));
    }
}
