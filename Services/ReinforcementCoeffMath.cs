using System;

namespace SP_Addin.ReinforcementCoeff.Services
{
    /// <summary>Расчёт в кг и метрах; не зависит от Revit API.</summary>
    internal static class ReinforcementCoeffMath
    {
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static bool TryCalculateMass(double unitMass, int count, int massType,
            double? lengthMeters, double? assemblies, out double mass, out string error)
        {
            mass = 0;
            error = string.Empty;
            if (!IsFinite(unitMass) || unitMass <= 0)
                error = "RNF.Масса элемента должна быть положительной.";
            else if (count <= 0)
                error = "RNF.Количество элементов должно быть положительным целым числом.";
            else if (massType == 1 && (!lengthMeters.HasValue || !IsFinite(lengthMeters.Value) || lengthMeters <= 0))
                error = "Для погонной массы требуется положительная RNF.Длина пм в метрах.";
            else if (assemblies.HasValue && (!IsFinite(assemblies.Value) || assemblies <= 0))
                error = "RNF.Количество сборок_число должно быть положительным; пустое значение означает 1.";

            if (error.Length > 0) return false;
            mass = unitMass * count * (massType == 1 ? lengthMeters!.Value : 1) * (assemblies ?? 1);
            if (!IsFinite(mass) || mass <= 0)
            {
                error = "Некорректная итоговая масса.";
                return false;
            }
            return true;
        }
    }
}
