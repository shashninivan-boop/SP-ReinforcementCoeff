using System.Windows;

namespace SP_Addin.ReinforcementCoeff
{
    /// <summary>Ширины колонок таблицы отчёта «Средний расход арматуры».</summary>
    public static class ReinforcementCoeffReportLayout
    {
        public const double ColKitMinWidth = 100;
        public const double ColGroupingMinWidth = 150;
        public const double ColVolumeMinWidth = 120;
        public const double ColRebarMassMinWidth = 128;
        public const double ColCoefficientMinWidth = 80;
        public const double ColStatusMinWidth = 120;

        public const double TableMinWidth =
            ColKitMinWidth
            + ColGroupingMinWidth
            + ColVolumeMinWidth
            + ColRebarMassMinWidth
            + ColCoefficientMinWidth
            + ColStatusMinWidth;

        /// <summary>Отступы таблицы (margin ListView + padding строк).</summary>
        public const double TableHorizontalChrome = 32;

        /// <summary>Внешние поля окна и рамка панели отчёта.</summary>
        public const double WindowHorizontalChrome = 48;

        public const double DefaultWindowWidth = 1028;
        public const double DefaultWindowHeight = 640;
        public const double MinWindowWidth = TableMinWidth + TableHorizontalChrome + WindowHorizontalChrome;
        public const double MinWindowHeight = 580;
        public const double ReportPanelMinHeight = 180;
        public const double SettingsPanelMinWidth = 460;

        public static GridLength ColKitWidth => new GridLength(ColKitMinWidth);
        public static GridLength ColGroupingWidth => new GridLength(ColGroupingMinWidth);
        public static GridLength ColVolumeWidth => new GridLength(ColVolumeMinWidth);
        public static GridLength ColRebarMassWidth => new GridLength(ColRebarMassMinWidth);
        public static GridLength ColCoefficientWidth => new GridLength(ColCoefficientMinWidth);
    }
}
