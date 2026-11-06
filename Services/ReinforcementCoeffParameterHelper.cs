using System;
using System.Globalization;
using Autodesk.Revit.DB;

namespace SP_Addin.ReinforcementCoeff.Services
{
    /// <summary>Чтение и запись параметров элементов с учётом разных StorageType.</summary>
    internal static class ReinforcementCoeffParameterHelper
    {
        /// <summary>
        /// Ищет параметр по имени: сначала LookupParameter, затем перебор Parameters
        /// (на случай расхождения регистра или пробелов в имени).
        /// </summary>
        public static Parameter? FindParameter(Element element, string parameterName)
        {
            if (element == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return null;
            }

            Parameter? parameter = element.LookupParameter(parameterName);
            if (parameter != null)
            {
                return parameter;
            }

            string targetName = parameterName.Trim();
            foreach (Parameter candidate in element.Parameters)
            {
                if (candidate?.Definition?.Name == null)
                {
                    continue;
                }

                if (string.Equals(candidate.Definition.Name.Trim(), targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return null;
        }

        public static string? ReadStringValue(Element element, string parameterName)
        {
            Parameter? parameter = FindParameter(element, parameterName);
            if (parameter == null)
            {
                return null;
            }

            if (parameter.StorageType == StorageType.String)
            {
                string? text = parameter.AsString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            if (!parameter.HasValue)
            {
                return null;
            }

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    return parameter.AsString()?.Trim();
                case StorageType.Integer:
                    return parameter.AsInteger().ToString(CultureInfo.InvariantCulture);
                case StorageType.Double:
                    return parameter.AsDouble().ToString(CultureInfo.InvariantCulture);
                case StorageType.ElementId:
                    ElementId id = parameter.AsElementId();
                    return id != null && id != ElementId.InvalidElementId
                        ? SP_Addin.ReinforcementCoeffElementIds.ToStableString(id)
                        : null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Читает текст, как он отображается в Revit (ADSK_Группирование, RNF.Марка конструкции).
        /// Сначала AsValueString — для параметров, где AsString/HasValue не отражают видимое значение.
        /// </summary>
        public static string? ReadDisplayTextValue(Element element, string parameterName)
        {
            Parameter? parameter = FindParameter(element, parameterName);
            if (parameter == null)
            {
                return null;
            }

            string? displayValue = parameter.AsValueString()?.Trim();
            if (!string.IsNullOrWhiteSpace(displayValue))
            {
                return displayValue;
            }

            if (!parameter.HasValue)
            {
                return null;
            }

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    return parameter.AsString()?.Trim();
                case StorageType.Integer:
                    return parameter.AsInteger().ToString(CultureInfo.InvariantCulture);
                case StorageType.Double:
                    return parameter.AsDouble().ToString(CultureInfo.InvariantCulture);
                case StorageType.ElementId:
                    ElementId id = parameter.AsElementId();
                    return id != null && id != ElementId.InvalidElementId
                        ? SP_Addin.ReinforcementCoeffElementIds.ToStableString(id)
                        : null;
                default:
                    return null;
            }
        }

        /// <summary>RNF.Учёт в спецификации может храниться как int или string.</summary>
        public static bool TryReadBoolValue(Element element, string parameterName, out bool value)
        {
            value = false;
            Parameter? parameter = FindParameter(element, parameterName);
            if (parameter == null || !parameter.HasValue)
            {
                return false;
            }

            switch (parameter.StorageType)
            {
                case StorageType.Integer:
                    value = parameter.AsInteger() != 0;
                    return true;
                case StorageType.String:
                {
                    string text = parameter.AsString()?.Trim() ?? string.Empty;
                    if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(text, "да", StringComparison.OrdinalIgnoreCase) ||
                        text == "1")
                    {
                        value = true;
                        return true;
                    }

                    if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(text, "нет", StringComparison.OrdinalIgnoreCase) ||
                        text == "0")
                    {
                        value = false;
                        return true;
                    }

                    return false;
                }
                default:
                    return false;
            }
        }

        public static bool TryReadDoubleValue(Element element, string parameterName, out double value)
        {
            value = 0;
            Parameter? parameter = FindParameter(element, parameterName);
            if (parameter == null || !parameter.HasValue)
            {
                return false;
            }

            if (parameter.StorageType == StorageType.Double)
            {
                value = parameter.AsDouble();
                return true;
            }

            if (parameter.StorageType == StorageType.Integer)
            {
                value = parameter.AsInteger();
                return true;
            }

            if (parameter.StorageType == StorageType.String)
            {
                string? text = parameter.AsString()?.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    return false;
                }

                return double.TryParse(
                    text!.Replace(',', '.'),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out value);
            }

            return false;
        }

        public static bool TryReadIntValue(Element element, string parameterName, out int value)
        {
            value = 0;
            Parameter? parameter = FindParameter(element, parameterName);
            if (parameter == null || !parameter.HasValue)
            {
                return false;
            }

            if (parameter.StorageType == StorageType.Integer)
            {
                value = parameter.AsInteger();
                return true;
            }

            if (parameter.StorageType == StorageType.Double)
            {
                double raw = parameter.AsDouble();
                if (!ReinforcementCoeffMath.IsFinite(raw) || raw < int.MinValue || raw > int.MaxValue || raw != Math.Truncate(raw))
                    return false;
                value = (int)raw;
                return true;
            }

            if (parameter.StorageType == StorageType.String &&
                int.TryParse(parameter.AsString()?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
            {
                value = parsed;
                return true;
            }

            return false;
        }

        /// <summary>Параметр «Длина» хранится в футах; RNF-параметр «Число» уже задаётся в метрах.</summary>
        public static bool TryReadLengthMeters(Element element, string name, out double meters)
        {
            if (!TryReadDoubleValue(element, name, out meters)) return false;
            Parameter? parameter = FindParameter(element, name);
            if (parameter?.StorageType == StorageType.Double && parameter.Definition.GetDataType() == SpecTypeId.Length)
                meters = UnitUtils.ConvertFromInternalUnits(meters, UnitTypeId.Meters);
            return ReinforcementCoeffMath.IsFinite(meters);
        }

        /// <summary>Записывает число без неявного округления до целого.</summary>
        public static bool TrySetDoubleValue(Parameter parameter, double value)
        {
            if (parameter == null || parameter.IsReadOnly || !ReinforcementCoeffMath.IsFinite(value))
            {
                return false;
            }

            try
            {
                switch (parameter.StorageType)
                {
                    case StorageType.Double:
                        ForgeTypeId spec = parameter.Definition.GetDataType();
                        if (spec == SpecTypeId.MassDensity)
                            value = UnitUtils.ConvertToInternalUnits(value, UnitTypeId.KilogramsPerCubicMeter);
                        return parameter.AsDouble() == value || parameter.Set(value);
                    case StorageType.String:
                        string text = value.ToString("0.########", CultureInfo.InvariantCulture);
                        return parameter.AsString() == text || parameter.Set(text);
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
