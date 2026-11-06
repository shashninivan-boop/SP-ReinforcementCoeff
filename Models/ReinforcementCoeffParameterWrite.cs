using Autodesk.Revit.DB;

namespace SP_Addin.ReinforcementCoeff.Models
{
    /// <summary>Одна запись параметра элемента, подготовленная после расчёта.</summary>
    public sealed class ReinforcementCoeffParameterWrite
    {
        public ElementId ElementId { get; set; } = ElementId.InvalidElementId;
        public string ElementUniqueId { get; set; } = string.Empty;
        public string ParameterName { get; set; } = string.Empty;
        public double Value { get; set; }
    }
}
