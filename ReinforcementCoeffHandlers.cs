using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SP_Addin.ReinforcementCoeff.Models;
using SP_Addin.ReinforcementCoeff.Services;

namespace SP_Addin.ReinforcementCoeff
{
    /// <summary>
    /// Выполняет расчёт в контексте Revit API (через ExternalEvent),
    /// т.к. из WPF нельзя напрямую открывать транзакции.
    /// </summary>
    internal sealed class ReinforcementCoeffRunHandler : IExternalEventHandler
    {
        public UIApplication? UIApp { get; set; }
        public ReinforcementCoeffContext? Context { get; set; }
        public Action<ReinforcementCoeffRunResult?, string?>? OnCompleted { get; set; }

        public void Execute(UIApplication app)
        {
            string? error = null;
            ReinforcementCoeffRunResult? result = null;

            try
            {
                UIApplication uiApp = UIApp ?? app;
                UIDocument? uiDoc = uiApp.ActiveUIDocument;
                if (uiDoc == null)
                {
                    error = "Не найден активный документ Revit.";
                    return;
                }

                Document doc = uiDoc.Document;
                if (doc.IsFamilyDocument)
                {
                    error = "Команда доступна только в проектном документе.";
                    return;
                }

#if !STANDALONE
                if (!RebarAutoupdateService.IsKrModel(doc))
                {
                    error = "Инструмент доступен только в моделях КР.";
                    return;
                }
#endif

                if (Context == null)
                {
                    error = "Не заданы параметры расчёта.";
                    return;
                }

                var snapshot = new CalculationSnapshot(uiApp.Application, doc);
                try
                {
                    result = new ReinforcementCoeffService().Run(doc, Context);
                    result.Snapshot = snapshot;
                }
                catch { snapshot.Dispose(); throw; }
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                OnCompleted?.Invoke(result, error);
            }
        }

        public string GetName() => "SP Reinforcement Coefficient Run";
    }

    /// <summary>
    /// Записывает в модель значения, подготовленные после расчёта.
    /// </summary>
    internal sealed class ReinforcementCoeffWriteHandler : IExternalEventHandler
    {
        public UIApplication? UIApp { get; set; }
        public IReadOnlyList<ReinforcementCoeffParameterWrite>? PendingWrites { get; set; }
        public CalculationSnapshot? Snapshot { get; set; }
        public Action<ReinforcementCoeffWriteResult?, string?>? OnCompleted { get; set; }

        public void Execute(UIApplication app)
        {
            string? error = null;
            ReinforcementCoeffWriteResult? result = null;

            try
            {
                UIApplication uiApp = UIApp ?? app;
                UIDocument? uiDoc = uiApp.ActiveUIDocument;
                if (uiDoc == null)
                {
                    error = "Не найден активный документ Revit.";
                    return;
                }

                Document doc = uiDoc.Document;
                if (doc.IsFamilyDocument)
                {
                    error = "Команда доступна только в проектном документе.";
                    return;
                }

#if !STANDALONE
                if (!RebarAutoupdateService.IsKrModel(doc))
                {
                    error = "Инструмент доступен только в моделях КР.";
                    return;
                }
#endif

                if (PendingWrites == null || PendingWrites.Count == 0)
                {
                    error = "Нет данных для записи. Сначала выполните расчёт.";
                    return;
                }

                if (Snapshot == null) throw new InvalidOperationException("Нет действующего расчёта.");
                Snapshot.EnsureCurrent(doc);
                result = new ReinforcementCoeffService().WriteParameters(doc, PendingWrites);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                OnCompleted?.Invoke(result, error);
            }
        }

        public string GetName() => "SP Reinforcement Coefficient Write";
    }

    /// <summary>
    /// Выбирает в модели элементы расчётной группы (бетон и арматура).
    /// При заданном TargetViewId сначала активирует owner-view узлов.
    /// </summary>
    internal sealed class ReinforcementCoeffSelectElementsHandler : IExternalEventHandler
    {
        public UIApplication? UIApp { get; set; }
        public List<long> ElementIdValues { get; set; } = new List<long>();
        public CalculationSnapshot? Snapshot { get; set; }

        /// <summary>Id вида для активации перед выделением; null или &lt;= 0 — без смены вида.</summary>
        public long? TargetViewId { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                UIApplication uiApp = UIApp ?? app;
                UIDocument? uiDoc = uiApp.ActiveUIDocument;
                if (uiDoc == null || ElementIdValues == null || ElementIdValues.Count == 0)
                {
                    return;
                }

                Document doc = uiDoc.Document;
                if (Snapshot == null) throw new InvalidOperationException("Нет действующего расчёта.");
                Snapshot.EnsureCurrent(doc);
                IList<ElementId> elementIds = ToExistingElementIds(doc, ElementIdValues);
                if (elementIds.Count == 0)
                {
                    return;
                }

                TryActivateTargetView(uiDoc, doc);

                try
                {
                    uiDoc.ShowElements(elementIds);
                }
                catch (Exception)
                {
                    // ShowElements может не сработать для view-owned узлов на чужом виде —
                    // после ActiveView достаточно SetElementIds.
                }

                uiDoc.Selection.SetElementIds(elementIds);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Средний расход арматуры", $"Ошибка при выборе элементов:\n{ex.Message}");
            }
            finally
            {
                TargetViewId = null;
            }
        }

        public string GetName() => "SP Reinforcement Coefficient Select Elements";

        /// <summary>Переключает активный вид на TargetViewId, если вид существует.</summary>
        private void TryActivateTargetView(UIDocument uiDoc, Document doc)
        {
            if (TargetViewId == null || TargetViewId.Value <= 0)
            {
                return;
            }

            if (!ReinforcementCoeffElementIds.TryParseElementId(
                    TargetViewId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    out ElementId viewElementId))
            {
                return;
            }

            if (doc.GetElement(viewElementId) is not View view || view.IsTemplate)
            {
                return;
            }

            if (uiDoc.ActiveView == null || uiDoc.ActiveView.Id != view.Id)
            {
                uiDoc.ActiveView = view;
            }
        }

        private static IList<ElementId> ToExistingElementIds(Document doc, IEnumerable<long> source)
        {
            var ids = new List<ElementId>();
            var seen = new HashSet<long>();

            foreach (long rawId in source ?? Enumerable.Empty<long>())
            {
                if (rawId <= 0 || !seen.Add(rawId))
                {
                    continue;
                }

                if (!ReinforcementCoeffElementIds.TryParseElementId(
                        rawId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        out ElementId elementId))
                {
                    continue;
                }

                if (doc.GetElement(elementId) != null)
                {
                    ids.Add(elementId);
                }
            }

            return ids;
        }
    }
}
