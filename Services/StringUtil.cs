using System.Globalization;
using System;
using System.Collections.Generic;
using System.Text;

namespace AppCore.Services
{
    public class StringUtil
    {
        public static string NormalizeString(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;

            return string.Concat(
                input.Normalize(NormalizationForm.FormD)
                     .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            ).ToLowerInvariant().Trim();
        }
    }
}