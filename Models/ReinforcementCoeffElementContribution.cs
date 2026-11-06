using System.Globalization;

namespace SP_Addin.ReinforcementCoeff.Models
{
    /// <summary>Тип вклада элемента в сумму группы.</summary>
    public enum ReinforcementCoeffContributionKind
    {
        Concrete,
        Rebar,
    }

    /// <summary>Поэлементный вклад в объём бетона или массу арматуры группы.</summary>
    public sealed class ReinforcementCoeffElementContribution
    {
        private static readonly CultureInfo RuCulture = CultureInfo.GetCultureInfo("ru-RU");

        public long ElementId { get; set; }

        /// <summary>Отображаемое имя (семейство/тип/категория).</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Объём (м³) или масса арматуры.</summary>
        public double Contribution { get; set; }

        public ReinforcementCoeffContributionKind Kind { get; set; }

        /// <summary>OwnerViewId для view-owned узлов; 0 если вида нет.</summary>
        public long OwnerViewId { get; set; }

        /// <summary>Имя owner-view или «(без вида)» / «Модель».</summary>
        public string OwnerViewName { get; set; } = string.Empty;

        public string ContributionDisplay =>
            Kind == ReinforcementCoeffContributionKind.Concrete
                ? Contribution.ToString("0.###", RuCulture) + " м³"
                : Contribution.ToString("0.###", RuCulture);

        public string ElementIdDisplay =>
            ElementId.ToString(CultureInfo.InvariantCulture);

        public bool HasOwnerView => OwnerViewId > 0;
    }
}
