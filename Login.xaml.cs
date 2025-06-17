using Npgsql;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MyWpfApp
{
    public partial class Login : Page
    {
        public Login()
        {
            InitializeComponent();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text.Trim();
            string password = PasswordBox.Password.Trim();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Пожалуйста, введите email и пароль.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetUIBusyState(true);
            await Task.Delay(1500);

            var (isValid, role) = ValidateUser(email, password);
            SetUIBusyState(false);

            if (isValid)
            {
                UserSession.Login(email, role);

                // Сохраняем email и роль, если выбрана галочка "Запомнить меня"
                if (RememberMeCheckBox.IsChecked == true)
                {
                    Properties.Settings.Default.RememberMe = true;
                    Properties.Settings.Default.SavedEmail = email;
                    Properties.Settings.Default.SavedRole = role; // сохраняем роль
                    Properties.Settings.Default.Save();
                }
                else
                {
                    Properties.Settings.Default.RememberMe = false;
                    Properties.Settings.Default.SavedEmail = string.Empty;
                    Properties.Settings.Default.SavedRole = string.Empty; // сбрасываем роль
                    Properties.Settings.Default.Save();
                }

                string message = role == "admin" ? "Вы зашли как админ." : "Вы зашли как пользователь.";
                MessageBox.Show(message, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                ((MainWindow)Application.Current.MainWindow).UpdateUIAfterLogin();

                if (role == "admin")
                    NavigationService.Navigate(new AdminPage());
                else
                    NavigationService.Navigate(new ProfilePage());
            }
            else
            {
                MessageBox.Show("Неверные данные пользователя.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetUIBusyState(bool isBusy)
        {
            LoadingOverlay.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
            EmailTextBox.IsEnabled = !isBusy;
            PasswordBox.IsEnabled = !isBusy;

            var rootPanel = FindParent<Panel>(EmailTextBox);
            if (rootPanel != null)
            {
                foreach (UIElement child in rootPanel.Children)
                {
                    if (child is Button button)
                        button.IsEnabled = !isBusy;
                }
            }
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            while (parentObject != null && !(parentObject is T))
            {
                parentObject = VisualTreeHelper.GetParent(parentObject);
            }
            return parentObject as T;
        }

        private (bool isValid, string role) ValidateUser(string email, string password)
        {
            string connString = "Host=shinkansen.proxy.rlwy.net;Port=44952;Username=postgres;Password=phugTZbbnUjmCaNFDounPPPeWBOBDUdJ;Database=railway";

            using (var conn = new NpgsqlConnection(connString))
            {
                try
                {
                    conn.Open();
                    string query = "SELECT password, role FROM auth.users WHERE email = @Email";
                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("Email", email);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                string hashedPassword = reader.GetString(0);
                                string role = reader.GetString(1);
                                bool passwordValid = BCrypt.Net.BCrypt.Verify(password, hashedPassword);
                                return (passwordValid, passwordValid ? role : null);
                            }

                            return (false, null);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка: {ex.Message}");
                    return (false, null);
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.NavigationService.Navigate(new Regis());
        }
    }
}
