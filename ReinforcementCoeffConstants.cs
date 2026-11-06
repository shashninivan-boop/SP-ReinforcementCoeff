using Autodesk.Revit.DB;

namespace SP_Addin.ReinforcementCoeff
{
    /// <summary>
    /// Имена параметров и категорий для расчёта коэффициента армирования.
    /// Соответствует логике Dynamo-скрипта «Наработки скрипта по подсчету Коэф арм».
    /// </summary>
    internal static class ReinforcementCoeffConstants
    {
        /// <summary>Фильтр бетонных элементов по комплекту.</summary>
        public const string ParamKit = "ADSK_Комплект";

        /// <summary>Фильтр арматуры по разделу (значение совпадает с комплектом).</summary>
        public const string ParamRebarSection = "RNF.Раздел";

        /// <summary>Группировка бетона и арматуры для расчёта (режим по значению).</summary>
        public const string ParamGrouping = "SP_ВРС_Группирование";

        /// <summary>Источник отметки для плит и прочего бетона в автоматическом режиме.</summary>
        public const string ParamAdskGrouping = "ADSK_Группирование";

        /// <summary>Источник отметки для арматуры узлов в автоматическом режиме.</summary>
        public const string ParamRebarStructureMark = "RNF.Марка конструкции";

        /// <summary>Код материала бетона: параметр материала «Мтрл.КодМатериала» (AsValueString, напр. 204006100).</summary>
        public const string ParamMaterialCode = "Мтрл.КодМатериала";

        /// <summary>Префикс кода бетона в «Мтрл.КодМатериала».</summary>
        public const string ConcreteMaterialCodePrefix = "20";

        /// <summary>Масса одного стержня из Renga/RNF.</summary>
        public const string ParamRebarMass = "RNF.Масса элемента";

        /// <summary>Записываемая масса арматуры на элемент (числитель по стержню).</summary>
        public const string ParamMassPerElement = "SP_Масса арматуры на элемент";

        /// <summary>Записываемый средний расход: сумма масс / сумма объёмов группы.</summary>
        public const string ParamAvgConsumption = "SP_Средний расход арматуры";

        /// <summary>Арматура должна участвовать в спецификации (значение true/1).</summary>
        public const string ParamSpecAccount = "RNF.Учёт в спецификации";

        /// <summary>Тип массы: 1 — погонный метраж (кол-во × длина пм).</summary>
        public const string ParamMassType = "RNF.Тип массы";

        public const string ParamElementCount = "RNF.Количество элементов";
        public const string ParamLengthPm = "RNF.Длина пм";
        public const string ParamAssemblyCount = "RNF.Количество сборок_число";

        /// <summary>Пометка типа семейства; «Каркас» исключается из расчёта.</summary>
        public const string ParamKeyMark = "Ключевая пометка";
        public const string FrameMarkValue = "Каркас";

        /// <summary>Ключ группы для элементов без отметки в параметрах группировки.</summary>
        public const string NoElevationGroupKey = "(без отметки)";
        public const string TransactionName = "SP: Средний расход арматуры";

        /// <summary>Категории бетона — как в Python-узле фильтрации скрипта.</summary>
        public static readonly BuiltInCategory[] ConcreteCategories =
        {
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_GenericModel,
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming,
        };

        /// <summary>Арматура Renga/RNF — категория «Компоненты узла».</summary>
        public const BuiltInCategory RebarCategory = BuiltInCategory.OST_DetailComponents;
    }
}
