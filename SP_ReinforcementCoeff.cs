using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Windows;
using System.Windows.Interop;

namespace SP_Addin.ReinforcementCoeff
{
    /// <summary>
    /// Команда ленты КР: расчёт и запись коэффициента армирования.
    /// Окно — singleton (повторный вызов активирует уже открытое).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SP_ReinforcementCoeff : IExternalCommand
    {
        private static ReinforcementCoeffView? _openView;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (RevitContext.UiApplication == null)
                {
                    RevitContext.Initialize(commandData);
                }

                Document? doc = RevitContext.Document;
                if (doc == null || doc.IsFamilyDocument)
                {
                    message = "Команда доступна только в проектном документе.";
                    return Result.Failed;
                }

                if (!RebarAutoupdateService.IsKrModel(doc))
                {
                    TaskDialog.Show("Средний расход арматуры", "Инструмент доступен только в моделях КР.");
                    return Result.Cancelled;
                }

                // Не открываем второе окно — поднимаем существующее.
                if (_openView != null)
                {
                    if (_openView.WindowState == WindowState.Minimized)
                    {
                        _openView.WindowState = WindowState.Normal;
                    }

                    _openView.Activate();
                    return Result.Succeeded;
                }

                var viewModel = new ReinforcementCoeffViewModel(commandData.Application);
                var view = new ReinforcementCoeffView(viewModel);
                var helper = new WindowInteropHelper(view)
                {
                    Owner = commandData.Application.MainWindowHandle,
                };

                _openView = view;
                view.Closed += (_, __) => _openView = null;
                view.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Средний расход арматуры", ex.ToString());
                return Result.Failed;
            }
        }
    }
}
