using System.Collections.Generic;
using System.Globalization;

namespace SP_Addin.ReinforcementCoeff.Models
{
    /// <summary>Строка отчёта по одной группе (одна группировка или одна отметка).</summary>
    public sealed class ReinforcementCoeffGroupResult
    {
        private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

        /// <summary>ADSK_Комплект / RNF.Раздел.</summary>
        public string KitName { get; set; } = string.Empty;

        /// <summary>
        /// Колонка «Группирование»: отметка (+33.200), значение SP_ВРС_Группирование
        /// или исходный текст ADSK_Группирование / RNF.Марка конструкции.
        /// </summary>
        public string GroupingDisplay { get; set; } = string.Empty;
        public double ConcreteVolume { get; set; }
        public double RebarMass { get; set; }

        /// <summary>SP_Средний расход арматуры = RebarMass / ConcreteVolume.</summary>
        public double Coefficient { get; set; }

        public int ConcreteElementCount { get; set; }
        public int RebarElementCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }

        /// <summary>Бетонные элементы группы для выбора в модели.</summary>
        public List<long> ConcreteElementIds { get; set; } = new List<long>();

        /// <summary>Арматурные элементы группы для выбора в модели.</summary>
        public List<long> RebarElementIds { get; set; } = new List<long>();

        /// <summary>Поэлементный состав объёма бетона (по убыванию вклада).</summary>
        public List<ReinforcementCoeffElementContribution> ConcreteContributions { get; set; } =
            new List<ReinforcementCoeffElementContribution>();

        /// <summary>Поэлементный состав массы арматуры (по убыванию вклада).</summary>
        public List<ReinforcementCoeffElementContribution> RebarContributions { get; set; } =
            new List<ReinforcementCoeffElementContribution>();

        public bool HasConcreteElementIds => ConcreteElementIds.Count > 0;
        public bool HasRebarElementIds => RebarElementIds.Count > 0;
        public bool HasConcreteContributions => ConcreteContributions.Count > 0;
        public bool HasRebarContributions => RebarContributions.Count > 0;

        public string ConcreteVolumeDisplay =>
            ConcreteVolume.ToString("0.###", RuCulture) + " м³";
        public string RebarMassDisplay => RebarMass.ToString("0.###", RuCulture);
        public string CoefficientDisplay => Coefficient.ToString("0.###", RuCulture);
    }
}
