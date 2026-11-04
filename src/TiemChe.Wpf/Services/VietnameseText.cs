using System.Globalization;
using System.Text;

namespace TiemChe.Wpf.Services;

public static class VietnameseText
{
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
