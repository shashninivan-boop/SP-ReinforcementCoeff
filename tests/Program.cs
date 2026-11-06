using System;
using SP_Addin.ReinforcementCoeff.Services;
internal static class Program
{
    private static int checks;
    private static void Equal(double actual, double expected, string name)
    {
        if (Math.Abs(actual - expected) > 1e-9) throw new Exception(name + ": " + actual + " != " + expected);
        checks++;
    }
    private static void Main()
    {
        Equal(DynamoCalculation.CalculateMass(2, 10, 0, 6, 2), 40, "Штучная масса игнорирует длину");
        Equal(DynamoCalculation.CalculateMass(2, 10, 1, 6, 2), 240, "Погонная масса учитывает длину и сборки");
        Equal(DynamoCalculation.CalculateMass(2, 10, 1, null, 2), 40, "Нет длины: количество как в Python графа");
        Equal(DynamoCalculation.CalculateMass(2, 10, 0, null, 0), 0, "Ноль сборок не заменяется единицей");
        Equal(DynamoCalculation.CalculateMass(2, 10, 1, 0, 2), 0, "Нулевая длина");
        Equal(DynamoCalculation.CalculateMass(2, 10, 0, null, 3), 60, "Множитель сборок");
        Equal(DynamoCalculation.CalculateMass(2, 10, 0, 6, 1) + DynamoCalculation.CalculateMass(2, 10, 1, 6, 1), 140, "Смешанные типы массы");
        Console.WriteLine($"PASS: {checks} проверок формулы Dynamo.");
    }
}
