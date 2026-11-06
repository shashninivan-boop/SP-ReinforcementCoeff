namespace SP_Addin.ReinforcementCoeff.Services
{
    /// <summary>Поэлементная формула из графа Dynamo; каждый тип массы проверяется отдельно.</summary>
    internal static class DynamoCalculation
    {
        public static double CalculateMass(double unitMass, int count, int massType,
            double? lengthMeters, double assemblyCount)
        {
            // В Python графа отсутствие длины оставляет обычное количество элементов.
            double quantity = massType == 1 && lengthMeters.HasValue ? count * lengthMeters.Value : count;
            return unitMass * quantity * assemblyCount;
        }
    }
}
