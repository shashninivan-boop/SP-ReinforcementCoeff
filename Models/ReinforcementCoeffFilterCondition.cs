namespace SP_Addin.ReinforcementCoeff.Models
{
    /// <summary>Оператор дополнительного условия фильтрации.</summary>
    public enum ReinforcementCoeffFilterOperator
    {
        /// <summary>Параметр заполнен (непустое значение).</summary>
        HasValue,

        /// <summary>Параметр отсутствует или пуст.</summary>
        IsEmpty,

        /// <summary>Точное совпадение значения (без учёта регистра).</summary>
        Equals,

        /// <summary>Значение не совпадает или параметр пуст.</summary>
        NotEquals,

        /// <summary>Значение содержит указанную подстроку (без учёта регистра).</summary>
        Contains,
    }

    /// <summary>К каким элементам применяется дополнительное условие.</summary>
    public enum ReinforcementCoeffFilterTarget
    {
        /// <summary>Только арматура узлов.</summary>
        Rebar,

        /// <summary>Только бетонные элементы.</summary>
        Concrete,

        /// <summary>И бетон, и арматура.</summary>
        Both,
    }

    /// <summary>Одно дополнительное условие фильтрации по параметру элемента.</summary>
    public sealed class ReinforcementCoeffFilterCondition
    {
        /// <summary>Имя параметра Revit.</summary>
        public string ParameterName { get; set; } = string.Empty;

        /// <summary>Способ сравнения.</summary>
        public ReinforcementCoeffFilterOperator Operator { get; set; } =
            ReinforcementCoeffFilterOperator.Equals;

        /// <summary>Ожидаемое значение для Equals / NotEquals / Contains.</summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>Область применения условия.</summary>
        public ReinforcementCoeffFilterTarget Target { get; set; } =
            ReinforcementCoeffFilterTarget.Rebar;
    }
}
