using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SP_Addin.ReinforcementCoeff.Services
{
    /// <summary>
    /// Объём бетона элемента: только материалы с «Мтрл.КодМатериала», начинающимся с 20, через Element.GetMaterialVolume.
    /// Из многоматериального элемента учитывается только бетонная часть; неизвестные материалы диагностируются.
    /// </summary>
    internal static class ReinforcementCoeffConcreteVolumeHelper
    {
        public static bool IsQualifyingConcreteElement(Document doc, Element element) =>
            TryGetConcreteVolumeCubicMeters(doc, element, out _);

        public static bool TryGetConcreteVolumeCubicMeters(
            Document doc,
            Element element,
            out double volumeCubicMeters) =>
            TryGetConcreteVolumeCubicMeters(doc, element, out volumeCubicMeters, out _);

        public static bool TryGetConcreteVolumeCubicMeters(Document doc, Element element,
            out double volumeCubicMeters, out string error)
        {
            error = string.Empty;
            volumeCubicMeters = 0;
            if (doc == null || element == null)
            {
                return false;
            }

            Category? category = element.Category;
            if (category == null || !category.HasMaterialQuantities)
            {
                return false;
            }

            ICollection<ElementId> materialIds = element.GetMaterialIds(false);
            if (materialIds == null || materialIds.Count == 0)
            {
                return false;
            }

            double totalInternalVolume = 0;
            bool hasPositiveVolume = false;

            foreach (ElementId materialId in materialIds)
            {
                if (materialId == null || materialId == ElementId.InvalidElementId)
                {
                    continue;
                }

                double materialVolume;
                try
                {
                    materialVolume = element.GetMaterialVolume(materialId);
                }
                catch (ArgumentException)
                {
                    error = "Не удалось получить объём материала.";
                    return false;
                }

                if (materialVolume <= 0)
                {
                    continue;
                }

                hasPositiveVolume = true;

                if (doc.GetElement(materialId) is not Material material ||
                    string.IsNullOrWhiteSpace(ReadMaterialCode(material)))
                {
                    error = "У материала с положительным объёмом не заполнен Мтрл.КодМатериала.";
                    return false;
                }
                if (IsConcreteMaterial(material))
                    totalInternalVolume += materialVolume;
            }

            if (!hasPositiveVolume || totalInternalVolume <= 0)
            {
                return false;
            }

            volumeCubicMeters = UnitUtils.ConvertFromInternalUnits(totalInternalVolume, UnitTypeId.CubicMeters);
            return volumeCubicMeters > 0;
        }

        internal static bool IsConcreteMaterialCode(string? materialCode)
        {
            return !string.IsNullOrWhiteSpace(materialCode)
                && materialCode!.Trim().StartsWith(
                    ReinforcementCoeffConstants.ConcreteMaterialCodePrefix,
                    StringComparison.Ordinal);
        }

        private static bool IsConcreteMaterial(Material material) =>
            IsConcreteMaterialCode(ReadMaterialCode(material));

        private static string? ReadMaterialCode(Material material)
        {
            string? code = ReinforcementCoeffParameterHelper.ReadDisplayTextValue(
                material,
                ReinforcementCoeffConstants.ParamMaterialCode);
            if (string.IsNullOrWhiteSpace(code))
            {
                code = ReinforcementCoeffParameterHelper.ReadStringValue(
                    material,
                    ReinforcementCoeffConstants.ParamMaterialCode);
            }

            return code?.Trim();
        }
    }
}
