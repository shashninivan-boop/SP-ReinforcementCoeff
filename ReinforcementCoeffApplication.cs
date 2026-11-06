using System;
using System.Linq;
using Autodesk.Revit.UI;

namespace SP_Addin.ReinforcementCoeff
{
    public sealed class ReinforcementCoeffApplication : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            const string tab = "SP_test";
            const string panelName = "Армирование";
            try { application.CreateRibbonTab(tab); }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Вкладку мог создать другой тестовый инструмент.
            }
            try
            {
                RibbonPanel panel = application.GetRibbonPanels(tab).FirstOrDefault(p => p.Name == panelName)
                    ?? application.CreateRibbonPanel(tab, panelName);
                if (!panel.GetItems().Any(i => i.Name == "SP_ReinforcementCoeff_Test"))
                {
                    var button = new PushButtonData("SP_ReinforcementCoeff_Test", "Средний расход\nарматуры",
                        typeof(ReinforcementCoeffApplication).Assembly.Location,
                        typeof(SP_ReinforcementCoeff).FullName);
                    button.ToolTip = "Расчёт массы RNF-арматуры на объём бетона по комплекту и группе (кг/м³).";
                    panel.AddItem(button);
                }
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("SP_test", "Не удалось создать кнопку: " + ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
    }
}
