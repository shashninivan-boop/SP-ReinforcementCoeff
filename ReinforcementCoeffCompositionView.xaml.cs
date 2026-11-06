using System.Windows;
using System.Windows.Input;
using SP_Addin.Styles;

namespace SP_Addin.ReinforcementCoeff
{
    /// <summary>Окно поэлементного состава группы среднего расхода.</summary>
    public partial class ReinforcementCoeffCompositionView : Window
    {
        public ReinforcementCoeffCompositionView(ReinforcementCoeffCompositionViewModel viewModel)
        {
            WpfResourceHelper.EnsureResourceAssembly(typeof(ReinforcementCoeffCompositionView).Assembly);
            InitializeComponent();
            DataContext = viewModel;
            Loaded += (_, __) =>
            {
                SpSettingsWindowBehavior.ApplyWindowChrome(this);
                SpSettingsWindowBehavior.SetIsHitTestVisibleInChrome(HeaderCloseButton, true);
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
    }
}
