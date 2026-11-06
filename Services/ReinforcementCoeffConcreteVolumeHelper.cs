using Autodesk.Revit.DB;

namespace SP_Addin.ReinforcementCoeff.Services
{
    /// <summary>Как в Dynamo: читаем «Объем» элемента без фильтра по материалам.</summary>
    internal static class ReinforcementCoeffConcreteVolumeHelper
    {
        public static bool IsQualifyingConcreteElement(Document doc, Element element) =>
            TryGetConcreteVolumeCubicMeters(doc, element, out _);

        public static bool TryGetConcreteVolumeCubicMeters(Document doc, Element element, out double volumeCubicMeters)
        {
            volumeCubicMeters = 0;
            Parameter? parameter = ReinforcementCoeffParameterHelper.FindParameter(element, "Объем")
                ?? ReinforcementCoeffParameterHelper.FindParameter(element, "Объём")
                ?? element.get_Parameter(BuiltInParameter.HOST_VOLUME_COMPUTED);
            if (parameter == null || !parameter.HasValue) return false;
            if (!ReinforcementCoeffParameterHelper.TryReadDoubleValue(element, parameter.Definition.Name, out double value))
                return false;
            volumeCubicMeters = parameter.StorageType == StorageType.Double &&
                parameter.Definition.GetDataType() == SpecTypeId.Volume
                    ? UnitUtils.ConvertFromInternalUnits(value, UnitTypeId.CubicMeters) : value;
            return !double.IsNaN(volumeCubicMeters) && !double.IsInfinity(volumeCubicMeters) && volumeCubicMeters >= 0;
        }
    }
}
