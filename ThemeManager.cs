using System.Windows;
using System.Windows.Media;

namespace MyWpfApp
{
    public static class ThemeManager
    {
        private static MainWindow _mainWindow;

        public static void Init(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public static void ApplyDarkTheme()
        {
            Application.Current.Resources["DarkBackgroundColor"] = ColorConverter.ConvertFromString("#FF1E1E2F");
            Application.Current.Resources["SidebarBackgroundColor"] = ColorConverter.ConvertFromString("#FF333333"); // Тёмный бар
            Application.Current.Resources["TopBarBackgroundColor"] = ColorConverter.ConvertFromString("#FF333333");

            UpdateBrushes();
            _mainWindow?.UpdateTheme();
        }

        public static void ApplyLightTheme()
        {
            Application.Current.Resources["DarkBackgroundColor"] = ColorConverter.ConvertFromString("#FF1E1E2F");
            Application.Current.Resources["SidebarBackgroundColor"] = ColorConverter.ConvertFromString("#F0F0F0"); // Белый бар
            Application.Current.Resources["TopBarBackgroundColor"] = ColorConverter.ConvertFromString("#E0E0E0");

            UpdateBrushes();
            _mainWindow?.UpdateTheme();
        }
        public static void ApplyLightTheme1()
        {
            Application.Current.Resources["DarkBackgroundColor"] = ColorConverter.ConvertFromString("#FFFFD600");
            Application.Current.Resources["SidebarBackgroundColor"] = ColorConverter.ConvertFromString("#FFFFD600"); // Желтая тема
            Application.Current.Resources["TopBarBackgroundColor"] = ColorConverter.ConvertFromString("#FFFFD600");

            UpdateBrushes();
            _mainWindow?.UpdateTheme();
        }
        private static void UpdateBrushes()
        {
            Application.Current.Resources["DarkBackground"] = new SolidColorBrush((Color)Application.Current.Resources["DarkBackgroundColor"]);
            Application.Current.Resources["SidebarBackground"] = new SolidColorBrush((Color)Application.Current.Resources["SidebarBackgroundColor"]);
            Application.Current.Resources["TopBarBackground"] = new SolidColorBrush((Color)Application.Current.Resources["TopBarBackgroundColor"]);
        }
    }
}
