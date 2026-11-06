using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SP_Addin.ReinforcementCoeff.Styles;

namespace SP_Addin.ReinforcementCoeff
{
    public partial class ReinforcementCoeffView : Window
    {
        private ScrollViewer? _reportRowsScrollViewer;

        public ReinforcementCoeffView(ReinforcementCoeffViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += (_, __) =>
            {
                SpSettingsWindowBehavior.ApplyWindowChrome(this);
                SpSettingsWindowBehavior.SetIsHitTestVisibleInChrome(HeaderCloseButton, true);
                AttachReportTableScrollSync();
            };
        }

        /// <summary>Клип скруглённого chrome.</summary>
        private void ChromeRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try { SpSettingsWindowBehavior.ClipToRoundedRect(ChromeRoot, this); } catch { }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SpSettingsWindowBehavior.HandleHeaderMouseLeftButtonDown(this, e);
        }

        /// <summary>Синхронизирует горизонтальный скролл заголовка и ширину под scrollbar строк.</summary>
        private void AttachReportTableScrollSync()
        {
            _reportRowsScrollViewer = FindVisualChildScrollViewer(ReportRowsListView);
            if (_reportRowsScrollViewer == null)
            {
                return;
            }

            _reportRowsScrollViewer.ScrollChanged += OnReportRowsScrollChanged;
            ReportRowsListView.SizeChanged += (_, __) => UpdateReportHeaderLayout();
            UpdateReportHeaderLayout();
        }

        /// <summary>Обрабатывает ScrollChanged таблицы отчёта.</summary>
        private void OnReportRowsScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_reportRowsScrollViewer == null)
            {
                return;
            }

            if (Math.Abs(e.HorizontalChange) > double.Epsilon)
            {
                ReportHeaderScrollViewer.ScrollToHorizontalOffset(_reportRowsScrollViewer.HorizontalOffset);
            }

            if (Math.Abs(e.ViewportWidthChange) > double.Epsilon
                || Math.Abs(e.ExtentHeightChange) > double.Epsilon
                || Math.Abs(e.ExtentWidthChange) > double.Epsilon)
            {
                UpdateReportHeaderLayout();
            }
        }

        /// <summary>Выравнивает padding заголовка под ширину вертикального scrollbar.</summary>
        private void UpdateReportHeaderLayout()
        {
            if (_reportRowsScrollViewer == null)
            {
                return;
            }

            double verticalScrollBarWidth = _reportRowsScrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible
                ? Math.Max(0, _reportRowsScrollViewer.ActualWidth - _reportRowsScrollViewer.ViewportWidth)
                : 0;

            Thickness headerPadding = ReportHeaderScrollViewer.Padding;
            if (Math.Abs(headerPadding.Right - verticalScrollBarWidth) > 0.5)
            {
                ReportHeaderScrollViewer.Padding = new Thickness(0, 0, verticalScrollBarWidth, 0);
            }
        }

        /// <summary>Ищет ScrollViewer в визуальном дереве.</summary>
        private static ScrollViewer? FindVisualChildScrollViewer(DependencyObject parent)
        {
            if (parent == null)
            {
                return null;
            }

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is ScrollViewer scrollViewer)
                {
                    return scrollViewer;
                }

                ScrollViewer? nested = FindVisualChildScrollViewer(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
