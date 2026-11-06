using System;
using System.Collections.Generic;
using SP_Addin.ReinforcementCoeff;

namespace SP_Addin.ReinforcementCoeff.Models
{
    /// <summary>Способ группировки элементов перед расчётом.</summary>
    public enum ReinforcementCoeffGroupingMode
    {
        /// <summary>Группировка по значениям указанного параметра (по умолчанию SP_ВРС_Группирование).</summary>
        ByGroupingValue,

        /// <summary>Автоматическое объединение: сначала отметка, затем комплект (ADSK_Комплект / RNF.Раздел).</summary>
        ByElevation,
    }

    /// <summary>Параметры запуска расчёта, передаваемые из UI в сервис.</summary>
    public sealed class ReinforcementCoeffContext
    {
        /// <summary>Значение ADSK_Комплект / RNF.Раздел.</summary>
        public string KitName { get; set; } = string.Empty;

        /// <summary>Имя параметра группировки; используется только в режиме ByGroupingValue.</summary>
        public string GroupingParameterName { get; set; } = ReinforcementCoeffConstants.ParamGrouping;

        public ReinforcementCoeffGroupingMode GroupingMode { get; set; }

        /// <summary>Дополнительные условия фильтрации (AND). Пустое имя параметра игнорируется.</summary>
        public IReadOnlyList<ReinforcementCoeffFilterCondition> AdditionalFilters { get; set; } =
            Array.Empty<ReinforcementCoeffFilterCondition>();
    }
}
