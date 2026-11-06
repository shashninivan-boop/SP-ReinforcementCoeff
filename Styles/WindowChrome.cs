using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shell;

namespace SP_Addin.ReinforcementCoeff.Styles
{
    internal static class SpSettingsWindowBehavior
    {
        public static void ApplyWindowChrome(Window window) => WindowChrome.SetWindowChrome(window,
            new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0) });
        public static void SetIsHitTestVisibleInChrome(IInputElement element, bool value) =>
            WindowChrome.SetIsHitTestVisibleInChrome(element, value);
        public static void ClipToRoundedRect(FrameworkElement root, Window window) =>
            root.Clip = new RectangleGeometry(new Rect(0, 0, root.ActualWidth, root.ActualHeight), 8, 8);
        public static void HandleHeaderMouseLeftButtonDown(Window window, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                for (DependencyObject? current = source; current != null; current = current is Visual || current is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
                    if (current is Button) return;
            }
            if (e.ClickCount == 2)
                window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else if (e.LeftButton == MouseButtonState.Pressed) window.DragMove();
        }
    }

    public sealed class SpSettingsCaptionButtons : StackPanel
    {
        public SpSettingsCaptionButtons()
        {
            Orientation = Orientation.Horizontal;
            AddButton("—", w => w.WindowState = WindowState.Minimized);
            AddButton("□", w => w.WindowState = w.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
        }
        private void AddButton(string text, System.Action<Window> action)
        {
            var button = new Button { Content = text, Width = 30, Margin = new Thickness(0, 0, 4, 0) };
            button.Click += (_, e) => { var window = Window.GetWindow(this); if (window != null) action(window); e.Handled = true; };
            Children.Add(button);
        }
    }
}
