using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SP_Addin.ReinforcementCoeff.Services
{
    internal static class ElevationMark
    {
        // Шаблон «отм.» / «отм » с необязательным знаком и дробной частью через точку или запятую.
        private static readonly Regex ElevationFromTextRegex = new Regex(
            @"отм\.?\s*([+-]?\d+(?:[.,]\d+)?)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // RNF.Марка конструкции: «Плита перекрытия +36,950» — отметка без префикса «отм.».
        private static readonly Regex SignedElevationInTextRegex = new Regex(
            @"(?<![\d.,])[+-]\s*\d+(?:[.,]\d+)?",
            RegexOptions.CultureInvariant);

        private static readonly Regex ElevationMagnitudeRegex = new Regex(
            @"^\d+(?:\.\d+)?$",
            RegexOptions.CultureInvariant);

        public static string NormalizeElevationMark(string rawValue)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return string.Empty;
            }

            string normalized = rawValue.Trim().Replace(',', '.');
            bool isNegative = normalized.StartsWith("-", StringComparison.Ordinal);
            bool hasExplicitSign = isNegative ||
                                   normalized.StartsWith("+", StringComparison.Ordinal);
            string magnitude = hasExplicitSign ? normalized.Substring(1) : normalized;

            if (!ElevationMagnitudeRegex.IsMatch(magnitude))
            {
                return string.Empty;
            }

            if (!decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out decimal elevation))
                return string.Empty;

            // Численно одинаковые отметки +11.4, +11.40 и +11,400 имеют один ключ.
            return (elevation < 0 ? "-" : "+") +
                   Math.Abs(elevation).ToString("0.############################", CultureInfo.InvariantCulture);
        }

        public static bool TryExtractElevationFromText(string sourceValue, out string elevationMark)
        {
            elevationMark = string.Empty;
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                return false;
            }

            Match otmetkaMatch = ElevationFromTextRegex.Match(sourceValue);
            if (otmetkaMatch.Success)
            {
                elevationMark = NormalizeElevationMark(otmetkaMatch.Groups[1].Value);
                return !string.IsNullOrEmpty(elevationMark);
            }

            Match signedMatch = SignedElevationInTextRegex.Match(sourceValue);
            if (signedMatch.Success)
            {
                elevationMark = NormalizeElevationMark(signedMatch.Value.Replace(" ", string.Empty));
                return !string.IsNullOrEmpty(elevationMark);
            }

            return false;
        }

    }
}
