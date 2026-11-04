using System.ComponentModel.DataAnnotations;
using System.Text;

namespace TiemChe.Application.Validation;

public sealed class BusinessException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

internal static class Check
{
    internal const long MaxPrice = 100_000_000;
    internal static BusinessException Invalid(string message) => new(400, message);
    internal static BusinessException Missing(string message) => new(404, message);
    internal static BusinessException Conflict(string message) => new(409, message);

    internal static string Text(string? text, int maxLength, string label)
    {
        var value = Optional(text, maxLength, label);
        return value ?? throw Invalid($"Vui lòng nhập {label}.");
    }

    internal static string? Optional(string? text, int maxLength, string label)
    {
        var value = text?.Trim();
        if (value?.Length > maxLength)
            throw Invalid($"{label} tối đa {maxLength} ký tự.");
        return string.IsNullOrEmpty(value) ? null : value;
    }

    internal static string Email(string? email)
    {
        var value = Text(email, 254, "email");
        if (!new EmailAddressAttribute().IsValid(value) || value.Any(char.IsWhiteSpace))
            throw Invalid("Địa chỉ email không hợp lệ.");
        return value;
    }

    internal static string Password(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 10 || !CanVerifyPassword(password))
            throw Invalid("Mật khẩu cần ít nhất 10 ký tự và tối đa 72 byte UTF-8.");
        return password;
    }

    internal static bool CanVerifyPassword(string? password) =>
        !string.IsNullOrEmpty(password) && password.Length <= 72
        && !password.Contains('\0') && Encoding.UTF8.GetByteCount(password) <= 72;

    internal static long Price(long value)
    {
        if (value is < 0 or > MaxPrice)
            throw Invalid("Số tiền phải từ 0 đến 100.000.000 đồng.");
        return value;
    }

    internal static (int Page, int Size) Page(int? page, int? size)
    {
        if (page is < 1 or > 100_000 || size is < 1 or > 100)
            throw Invalid("Số trang hoặc kích thước trang không hợp lệ.");
        return (page ?? 1, size ?? 20);
    }
}
