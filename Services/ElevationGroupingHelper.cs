using System;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;
using SP_Addin;

namespace SP_Addin.ReinforcementCoeff.Services
{
    /// <summary>
    /// Извлечение отметки из строки параметра:
    /// «отм. +33,200» или «Плита перекрытия +36,950» → «+33.200» / «+36.950».
    /// Логика едина для ADSK_Группирование (бетон) и RNF.Марка конструкции (арматура узлов).
    /// </summary>
    internal static class ElevationGroupingHelper
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

        public static bool TryExtractElevationMark(string groupingValue, out string elevationMark)
        {
            return TryExtractElevationFromText(groupingValue, out elevationMark);
        }

        /// <summary>Ключ группы: нормализованная отметка или «(без отметки)».</summary>
        public static string GetElevationGroupKey(string? sourceValue)
        {
            return TryExtractElevationFromText(sourceValue ?? string.Empty, out string elevationMark)
                ? elevationMark
                : ReinforcementCoeffConstants.NoElevationGroupKey;
        }

        /// <summary>
        /// Текст для колонки «Группирование»: извлечённая отметка или исходное значение параметра.
        /// </summary>
        public static string GetGroupingDisplayText(string? sourceValue)
        {
            if (string.IsNullOrWhiteSpace(sourceValue))
            {
                return string.Empty;
            }

            return TryExtractElevationFromText(sourceValue, out string elevationMark)
                ? elevationMark
                : sourceValue.Trim();
        }

        /// <summary>
        /// Ключ отметки элемента в автоматическом режиме:
        /// бетон — ADSK_Группирование, арматура узлов — RNF.Марка конструкции.
        /// </summary>
        public static string GetElementElevationGroupKey(Element element)
        {
            string sourceValue = ReadElementElevationSourceValue(element);
            return GetElevationGroupKey(sourceValue);
        }

        /// <summary>Текст группирования элемента для отчёта в автоматическом режиме.</summary>
        public static string GetElementGroupingDisplayText(Element element) =>
            GetGroupingDisplayText(ReadElementElevationSourceValue(element));

        /// <summary>
        /// Сопоставление группы бетона (по SP_ВРС_Группирование) с RNF.Марка конструкции узла.
        /// </summary>
        public static bool StructureMarkMatchesGroupingValue(string concreteGroupingValue, string? structureMark)
        {
            if (string.IsNullOrWhiteSpace(concreteGroupingValue) || string.IsNullOrWhiteSpace(structureMark))
            {
                return false;
            }

            string normalizedGrouping = concreteGroupingValue.Trim();
            string normalizedMark = structureMark.Trim();

            if (string.Equals(normalizedMark, normalizedGrouping, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(
                    GetGroupingDisplayText(normalizedMark),
                    GetGroupingDisplayText(normalizedGrouping),
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string groupingElevationKey = GetElevationGroupKey(normalizedGrouping);
            string structureElevationKey = GetElevationGroupKey(normalizedMark);
            return groupingElevationKey != ReinforcementCoeffConstants.NoElevationGroupKey &&
                   structureElevationKey != ReinforcementCoeffConstants.NoElevationGroupKey &&
                   groupingElevationKey == structureElevationKey;
        }

        /// <summary>Приводит отметку к единому виду для группировки.</summary>
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

            return isNegative ? "-" + magnitude : "+" + magnitude;
        }

        private static bool TryExtractElevationFromText(string sourceValue, out string elevationMark)
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

        private static string ReadElementElevationSourceValue(Element element)
        {
            if (element?.Category == null)
            {
                return string.Empty;
            }

            string? parameterName = TryGetElevationSourceParameterName(element);
            if (string.IsNullOrEmpty(parameterName))
            {
                return string.Empty;
            }

            return ReinforcementCoeffParameterHelper.ReadDisplayTextValue(element, parameterName) ??
                   ReinforcementCoeffParameterHelper.ReadStringValue(element, parameterName) ??
                   string.Empty;
        }

        private static string? TryGetElevationSourceParameterName(Element element)
        {
            if (IsRebarElement(element))
            {
                return ReinforcementCoeffConstants.ParamRebarStructureMark;
            }

            if (IsConcreteElement(element))
            {
                return ReinforcementCoeffConstants.ParamAdskGrouping;
            }

            return null;
        }

        private static bool IsConcreteElement(Element element)
        {
            if (element?.Category == null)
            {
                return false;
            }

            ElementId categoryId = element.Category.Id;
            foreach (BuiltInCategory category in ReinforcementCoeffConstants.ConcreteCategories)
            {
                if (categoryId == new ElementId(category))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsRebarElement(Element element) =>
            element.Category.Id == new ElementId(ReinforcementCoeffConstants.RebarCategory);
    }
}
