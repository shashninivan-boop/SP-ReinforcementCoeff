using System.Globalization;
using Autodesk.Revit.DB;

namespace SP_Addin
{
    internal static class ReinforcementCoeffElementIds
    {
        public static long GetValue(this ElementId id)
        {
#if REVIT2024_OR_GREATER
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }
        public static string ToStableString(ElementId id) => id.GetValue().ToString(CultureInfo.InvariantCulture);
        public static bool TryParseElementId(string text, out ElementId id)
        {
            id = ElementId.InvalidElementId;
            if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value)) return false;
#if REVIT2024_OR_GREATER
            id = new ElementId(value);
#else
            if (value < int.MinValue || value > int.MaxValue) return false;
            id = new ElementId((int)value);
#endif
            return true;
        }
    }
}
