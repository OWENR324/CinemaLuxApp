using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;


namespace MyWpfApp
{
    public partial class MainWindow : Window
    {
        private bool isInternetAvailable = false;

        public MainWindow()
        {
            InitializeComponent();
            ThemeManager.Init(this);

            NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
            _ = CheckInternetAndUpdateUIAsync();

            // Восстановление сессии при запуске, если включено "Запомнить меня"
            if (Properties.Settings.Default.RememberMe &&
                !string.IsNullOrEmpty(Properties.Settings.Default.SavedEmail) &&
                !string.IsNullOrEmpty(Properties.Settings.Default.SavedRole))
            {
                UserSession.Login(Properties.Settings.Default.SavedEmail, Properties.Settings.Default.SavedRole);
            }

            UpdateUIAfterLogin();

            // Навигация в зависимости от сессии
            if (UserSession.IsLoggedIn)
            {
                if (UserSession.UserRole == "admin")
                    MainFrame.Navigate(new AdminPage());
                else
                    MainFrame.Navigate(new ProfilePage());
            }
            else
            {
                MainFrame.Navigate(new Login());
            }
        }

        private async void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            await CheckInternetAndUpdateUIAsync();
        }

        private async Task CheckInternetAndUpdateUIAsync()
        {
            bool currentStatus = await IsInternetAvailable();

            if (currentStatus != isInternetAvailable)
            {
                isInternetAvailable = currentStatus;
                Dispatcher.Invoke(() =>
                {
                    ShowLoading(!isInternetAvailable);
                    ToggleAllButtons(isInternetAvailable);

                    if (isInternetAvailable && MainFrame.Content == null && UserSession.IsLoggedIn)
                    {
                        MainFrame.Navigate(new MoviesPage());
                    }
                });
            }
        }

        private Task<bool> IsInternetAvailable()
        {
            return Task.Run(() =>
            {
                try
                {
                    using (var ping = new Ping())
                    {
                        var reply = ping.Send("8.8.8.8", 1000);
                        return reply.Status == IPStatus.Success;
                    }
                }
                catch
                {
                    return false;
                }
            });
        }

        private void ShowLoading(bool show)
        {
            LoadingContainer.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            MainFrame.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ToggleAllButtons(bool enabled)
        {
            foreach (var button in FindVisualChildren<Button>(this))
            {
                if (button.Name != "CloseButton" && button.Name != "MinimizeButton" && button.Name != "MaximizeButton")
                {
                    button.IsEnabled = enabled;
                    button.Opacity = enabled ? 1.0 : 0.5;
                }
            }
        }

        private static System.Collections.Generic.IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T obj)
                    yield return obj;

                foreach (T childOfChild in FindVisualChildren<T>(child))
                    yield return childOfChild;
            }
        }

        public void UpdateTheme()
        {
            LeftSidebar.Background = new SolidColorBrush((Color)Application.Current.Resources["SidebarBackgroundColor"]);
            TopBar.Background = new SolidColorBrush((Color)Application.Current.Resources["TopBarBackgroundColor"]);

            foreach (var child in LogicalTreeHelper.GetChildren(this))
            {
                if (child is FrameworkElement element)
                    element.Resources = Application.Current.Resources;
            }
        }

        // === Кнопки управления окном ===

        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => this.WindowState = WindowState.Minimized;

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                ((Button)sender).Content = "□";
                BorderThickness = new Thickness(0);
            }
            else
            {
                WindowState = WindowState.Maximized;
                ((Button)sender).Content = "❐";
                BorderThickness = new Thickness(7);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => this.Close();

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        // === Навигация ===

        private void MoviesButton_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(new MoviesPage());

        private void ProfileButton_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(new ProfilePage());

        private void TicketsButton_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(new TicketsPage());

        private void AboutButton_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(new AboutPage());

        private void SettingsButton_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(new SettingsPage());

        private void LoginButton_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(new Login());

        private void RegisterButton_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(new Regis());

        private void AdminButton_Click(object sender, RoutedEventArgs e) => MainFrame.Navigate(new AdminPage());

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            UserSession.Logout();

            RegisterButton.Visibility = Visibility.Visible;
            LoginButton.Visibility = Visibility.Visible;
            LogoutButton.Visibility = Visibility.Collapsed;
            AdminButton.Visibility = Visibility.Collapsed;

            Properties.Settings.Default.RememberMe = false;
            Properties.Settings.Default.SavedEmail = "";
            Properties.Settings.Default.SavedRole = "";
            Properties.Settings.Default.Save();

            MessageBox.Show("Вы успешно вышли из аккаунта.", "Выход", MessageBoxButton.OK, MessageBoxImage.Information);
            MainFrame.Navigate(new Login());
        }

        public void UpdateUIAfterLogin()
        {
            if (UserSession.IsLoggedIn)
            {
                RegisterButton.Visibility = Visibility.Collapsed;
                LoginButton.Visibility = Visibility.Collapsed;
                LogoutButton.Visibility = Visibility.Visible;
                AdminButton.Visibility = UserSession.UserRole == "admin" ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                RegisterButton.Visibility = Visibility.Visible;
                LoginButton.Visibility = Visibility.Visible;
                LogoutButton.Visibility = Visibility.Collapsed;
                AdminButton.Visibility = Visibility.Collapsed;
            }
        }

    }
}
