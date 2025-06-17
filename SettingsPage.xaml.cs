using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MyWpfApp
{
    public partial class SettingsPage : Page
    {
        public static bool IsAccessibilityModeEnabled { get; private set; }

        private static readonly Dictionary<DependencyObject, double> OriginalFontSizes = new();

        public SettingsPage()
        {
            InitializeComponent();

            // Восстанавливаем состояние галочки и масштабирования при загрузке
            AccessibilityModeCheckBox.IsChecked = IsAccessibilityModeEnabled;

            if (IsAccessibilityModeEnabled)
            {
                ApplyTextScaling(1.1);
            }
        }

        private void DarkThemeButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ApplyDarkTheme();
        }

        private void LightThemeButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ApplyLightTheme();
        }

        private void LightThemeButton1_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ApplyLightTheme1();
        }

        private void AccessibilityModeCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            IsAccessibilityModeEnabled = true;
            ApplyTextScaling(1.1);
        }

        private void AccessibilityModeCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            IsAccessibilityModeEnabled = false;
            ResetTextScaling();
        }

        public static void ApplyTextScaling(double scaleFactor)
        {
            var app = Application.Current;
            if (app == null) return;

            foreach (Window window in app.Windows)
            {
                ScaleFonts(window, scaleFactor);
            }
        }

        private static void ScaleFonts(DependencyObject parent, double scaleFactor)
        {
            if (parent == null) return;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                switch (child)
                {
                    case TextBlock textBlock:
                        SaveAndScale(textBlock, scaleFactor);
                        break;
                    case TextBox textBox:
                        SaveAndScale(textBox, scaleFactor);
                        break;
                    case Label label:
                        SaveAndScale(label, scaleFactor);
                        break;
                    case Button button:
                        SaveAndScale(button, scaleFactor);
                        break;
                    case CheckBox checkBox:
                        SaveAndScale(checkBox, scaleFactor);
                        break;
                }

                ScaleFonts(child, scaleFactor);
            }
        }

        private static void SaveAndScale(Control control, double scaleFactor)
        {
            if (!OriginalFontSizes.ContainsKey(control))
                OriginalFontSizes[control] = control.FontSize;

            control.FontSize = OriginalFontSizes[control] * scaleFactor;
        }

        private static void SaveAndScale(TextBlock tb, double scaleFactor)
        {
            if (!OriginalFontSizes.ContainsKey(tb))
                OriginalFontSizes[tb] = tb.FontSize;

            tb.FontSize = OriginalFontSizes[tb] * scaleFactor;
        }

        private static void ResetTextScaling()
        {
            foreach (var kvp in OriginalFontSizes)
            {
                switch (kvp.Key)
                {
                    case Control control:
                        control.FontSize = kvp.Value;
                        break;
                    case TextBlock tb:
                        tb.FontSize = kvp.Value;
                        break;
                }
            }
        }

        private void AccentThemeButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
