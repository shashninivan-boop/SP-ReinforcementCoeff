using System;
using SP_Addin.ReinforcementCoeff.Services;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAILED: " + name);
        _checks++;
    }
    private static void Main()
    {
        Check(ReinforcementCoeffMath.TryCalculateMass(2, 10, 0, null, 2, out double mass, out _) && mass == 40,
            "Штучная масса с количеством сборок");
        Check(ReinforcementCoeffMath.TryCalculateMass(2, 10, 1, 6, 1, out mass, out _) && mass == 120,
            "Погонная масса с длиной");
        Check(ReinforcementCoeffMath.TryCalculateMass(2, 10, 0, null, null, out mass, out _) && mass == 20,
            "Пустое количество сборок означает 1");
        foreach (double? length in new double?[] { null, 0, -1, double.NaN, double.PositiveInfinity })
            Check(!ReinforcementCoeffMath.TryCalculateMass(2, 10, 1, length, 1, out _, out string error) && error.Length > 0,
                "Некорректная погонная длина: " + length);
        foreach (double assemblies in new[] { 0d, -1, double.NaN, double.PositiveInfinity })
            Check(!ReinforcementCoeffMath.TryCalculateMass(2, 10, 0, null, assemblies, out _, out _),
                "Некорректное количество сборок: " + assemblies);
        Check(!ReinforcementCoeffMath.TryCalculateMass(2, 0, 0, null, 1, out _, out _), "Нулевое количество элементов");
        Check(!ReinforcementCoeffMath.TryCalculateMass(double.NaN, 10, 0, null, 1, out _, out _), "NaN в массе");
        Check(!ReinforcementCoeffMath.TryCalculateMass(double.MaxValue, 10, 0, null, 1, out _, out _), "Переполнение массы");
        foreach (string mark in new[] { "+11.4", "+11.40", "+11,400", "11.400" })
            Check(ElevationMark.NormalizeElevationMark(mark) == "+11.4", "Одинаковая отметка: " + mark);
        Check(ElevationMark.NormalizeElevationMark("-0.000") == "+0", "Отрицательный ноль");
        Check(ElevationMark.NormalizeElevationMark("-3,200") == "-3.2", "Отрицательная отметка");
        Check(ElevationMark.NormalizeElevationMark("плита") == "", "Неверная отметка");
        Check(ElevationMark.TryExtractElevationFromText("Плита перекрытия +11,400", out string key) && key == "+11.4",
            "Извлечение из RNF.Марка конструкции");
        Check(ElevationMark.TryExtractElevationFromText("отм. 11.40", out key) && key == "+11.4",
            "Извлечение из ADSK_Группирование");
        Check(!ElevationMark.TryExtractElevationFromText("Стена КЖ4", out _), "Текст без отметки не становится числом");
        CheckSnapshot();
        Console.WriteLine($"PASS: {_checks} проверок.");
    }

    private static void Throws(Action action, string name)
    {
        try { action(); }
        catch (InvalidOperationException) { Check(true, name); return; }
        throw new Exception("FAILED: " + name);
    }

    private static void CheckSnapshot()
    {
        var application = new Autodesk.Revit.ApplicationServices.Application();
        var a = new Autodesk.Revit.DB.Document();
        var b = new Autodesk.Revit.DB.Document();
        using (var snapshot = new CalculationSnapshot(application, a))
        {
            snapshot.EnsureCurrent(a);
            Check(!snapshot.IsStale, "Исходный документ доступен");
            Throws(() => snapshot.EnsureCurrent(b), "Запись и выбор в другом документе запрещены");
            application.Change(b);
            Check(!snapshot.IsStale, "Изменения другого документа не инвалидируют расчёт");
            bool notified = false;
            snapshot.Invalidated += () => notified = true;
            application.Change(a);
            Check(snapshot.IsStale && notified, "Изменение исходного документа инвалидирует расчёт");
            Throws(() => snapshot.EnsureCurrent(a), "Запись устаревшего расчёта запрещена");
        }
        var disposed = new CalculationSnapshot(application, a);
        disposed.Dispose();
        Throws(() => disposed.EnsureCurrent(a), "Освобождённый расчёт недоступен");
        application.Change(a);
        Check(!disposed.IsStale, "Обработчик изменений отсоединён при закрытии");
        using (var snapshot = new CalculationSnapshot(application, a))
        {
            a.IsValidObject = false;
            Throws(() => snapshot.EnsureCurrent(a), "Закрытый документ недоступен");
        }
    }
}
