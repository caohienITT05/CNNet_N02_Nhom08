using System.Globalization;
using System.Text;

namespace TiemChe.Application.Services;

public static class VietnameseSearch
{
    public const string SqlName = "tiemche_contains";

    public static bool Contains(string value, string normalizedTerm) =>
        Normalize(value).Contains(normalizedTerm, StringComparison.Ordinal);

    public static string Normalize(string value)
    {
        var text = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                text.Append(character is 'đ' or 'Đ' ? 'D' : char.ToUpperInvariant(character));
        }
        return text.ToString();
    }
}
