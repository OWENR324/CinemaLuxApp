using Npgsql;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using MimeKit;

namespace MyWpfApp
{
    public partial class Regis : Page
    {
        private bool isPasswordVisible = false;
        private TextBox visiblePasswordTextBox;
        private TextBox visibleConfirmPasswordTextBox;
        private string _pendingEmail;
        private DateTime _emailSentTime;
        private bool _emailVerified = false;

        public Regis()
        {
            InitializeComponent();
            RegisterButton.Visibility = Visibility.Collapsed;
            VerifyCodeButton.Visibility = Visibility.Visible;
            VerificationCodeTextBox.Visibility = Visibility.Collapsed;
        }

        private bool IsPasswordValid(string password)
        {
            return password.Length >= 8 && Regex.IsMatch(password, "[a-zA-Z]");
        }

        private string GetPassword()
        {
            return isPasswordVisible ? visiblePasswordTextBox.Text : PasswordBox.Password;
        }

        private string GetConfirmPassword()
        {
            return isPasswordVisible ? visibleConfirmPasswordTextBox.Text : ConfirmPasswordBox.Password;
        }

        private void TogglePasswordVisibility_Click(object sender, RoutedEventArgs e)
        {
            if (visiblePasswordTextBox == null)
            {
                visiblePasswordTextBox = new TextBox
                {
                    Width = PasswordBox.Width,
                    Height = PasswordBox.Height,
                    Margin = PasswordBox.Margin,
                    Padding = PasswordBox.Padding,
                    Background = PasswordBox.Background,
                    BorderBrush = PasswordBox.BorderBrush,
                    BorderThickness = PasswordBox.BorderThickness,
                    HorizontalAlignment = PasswordBox.HorizontalAlignment,
                    VerticalAlignment = PasswordBox.VerticalAlignment
                };
                var parent = PasswordBox.Parent as Panel;
                if (parent != null)
                {
                    int index = parent.Children.IndexOf(PasswordBox);
                    parent.Children.Insert(index, visiblePasswordTextBox);
                }
            }

            if (visibleConfirmPasswordTextBox == null)
            {
                visibleConfirmPasswordTextBox = new TextBox
                {
                    Width = ConfirmPasswordBox.Width,
                    Height = ConfirmPasswordBox.Height,
                    Margin = ConfirmPasswordBox.Margin,
                    Padding = ConfirmPasswordBox.Padding,
                    Background = ConfirmPasswordBox.Background,
                    BorderBrush = ConfirmPasswordBox.BorderBrush,
                    BorderThickness = ConfirmPasswordBox.BorderThickness,
                    HorizontalAlignment = ConfirmPasswordBox.HorizontalAlignment,
                    VerticalAlignment = ConfirmPasswordBox.VerticalAlignment
                };
                var parent = ConfirmPasswordBox.Parent as Panel;
                if (parent != null)
                {
                    int index = parent.Children.IndexOf(ConfirmPasswordBox);
                    parent.Children.Insert(index, visibleConfirmPasswordTextBox);
                }
            }

            if (!isPasswordVisible)
            {
                visiblePasswordTextBox.Text = PasswordBox.Password;
                visibleConfirmPasswordTextBox.Text = ConfirmPasswordBox.Password;

                visiblePasswordTextBox.Visibility = Visibility.Visible;
                visibleConfirmPasswordTextBox.Visibility = Visibility.Visible;

                PasswordBox.Visibility = Visibility.Collapsed;
                ConfirmPasswordBox.Visibility = Visibility.Collapsed;
            }
            else
            {
                PasswordBox.Password = visiblePasswordTextBox.Text;
                ConfirmPasswordBox.Password = visibleConfirmPasswordTextBox.Text;

                visiblePasswordTextBox.Visibility = Visibility.Collapsed;
                visibleConfirmPasswordTextBox.Visibility = Visibility.Collapsed;

                PasswordBox.Visibility = Visibility.Visible;
                ConfirmPasswordBox.Visibility = Visibility.Visible;
            }

            isPasswordVisible = !isPasswordVisible;
        }

        private async void VerifyCodeButton_Click(object sender, RoutedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            VerifyCodeButton.IsEnabled = false;

            string email = EmailTextBox.Text.Trim();
            string password = GetPassword();
            string confirmPassword = GetConfirmPassword();

            if (!IsValidEmail(email))
            {
                ShowError("Пожалуйста, введите корректный email!");
                return;
            }

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ShowError("Пожалуйста, заполните все поля!");
                return;
            }

            if (!IsPasswordValid(password))
            {
                ShowError("Пароль должен содержать минимум 8 символов и хотя бы одну букву!");
                return;
            }

            if (password != confirmPassword)
            {
                ShowError("Пароли не совпадают!");
                return;
            }

            try
            {
                if (await CheckEmailExistsInDatabase(email))
                {
                    ShowError("Пользователь с таким email уже существует.");
                    return;
                }

                _pendingEmail = email;
                _emailSentTime = DateTime.Now;
                _emailVerified = false;

                if (await SendConfirmationEmail(email))
                {
                    await Task.Run(async () => await CheckForBounceMessages());

                    if (!_emailVerified)
                    {
                        ShowError("Адрес электронной почты не существует или не принимает письма.");
                        return;
                    }

                    MessageBox.Show("Код подтверждения отправлен на ваш email.");
                    VerificationCodeTextBox.Visibility = Visibility.Visible;
                    VerifyCodeButton.Visibility = Visibility.Collapsed;
                    RegisterButton.Visibility = Visibility.Visible;
                }
                else
                {
                    ShowError("Не удалось отправить письмо. Проверьте почту или попробуйте позже.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка: {ex.Message}");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                VerifyCodeButton.IsEnabled = true;
            }
        }

        private async Task CheckForBounceMessages()
        {
            try
            {
                using (var client = new ImapClient())
                {
                    await client.ConnectAsync("imap.gmail.com", 993, true);
                    await client.AuthenticateAsync("krupkodaniil119@gmail.com", "rgpj gsuc iwlr nisv");

                    var inbox = client.Inbox;
                    await inbox.OpenAsync(FolderAccess.ReadWrite);

                    var searchQuery = SearchQuery.DeliveredAfter(_emailSentTime.AddMinutes(-1))
                        .And(SearchQuery.FromContains("mailer-daemon"))
                        .And(SearchQuery.SubjectContains("не доставлено"));

                    var uids = await inbox.SearchAsync(searchQuery);

                    foreach (var uid in uids)
                    {
                        var message = await inbox.GetMessageAsync(uid);
                        if (message.TextBody.Contains(_pendingEmail))
                        {
                            _emailVerified = false;
                            await inbox.AddFlagsAsync(uid, MessageFlags.Deleted, true);
                            return;
                        }
                    }

                    _emailVerified = true;
                }
            }
            catch
            {
                _emailVerified = true;
            }
        }

        private void ShowError(string message)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message);
                LoadingOverlay.Visibility = Visibility.Collapsed;
                VerifyCodeButton.IsEnabled = true;
            });
        }

        private async Task<bool> CheckEmailExistsInDatabase(string email)
        {
            try
            {
                string connectionString = "Host=shinkansen.proxy.rlwy.net;Port=44952;Username=postgres;Password=phugTZbbnUjmCaNFDounPPPeWBOBDUdJ;Database=railway";

                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    var checkCommand = new NpgsqlCommand("SELECT COUNT(*) FROM auth.users WHERE email = @email", connection);
                    checkCommand.Parameters.AddWithValue("email", email);
                    int userCount = Convert.ToInt32(await checkCommand.ExecuteScalarAsync());

                    return userCount > 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> SendConfirmationEmail(string email)
        {
            try
            {
                Random random = new Random();
                string confirmationCode = random.Next(10000, 100000).ToString();
                Application.Current.Properties["ConfirmationCode"] = confirmationCode;

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("MyWpfApp", "krupkodaniil119@gmail.com"));
                message.To.Add(new MailboxAddress("", email));
                message.Subject = "Подтверждение регистрации";

                message.Body = new TextPart("plain")
                {
                    Text = $"Ваш код подтверждения: {confirmationCode}"
                };

                using (var smtpClient = new MailKit.Net.Smtp.SmtpClient())
                {
                    await smtpClient.ConnectAsync("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                    await smtpClient.AuthenticateAsync("krupkodaniil119@gmail.com", "rgpj gsuc iwlr nisv");
                    await smtpClient.SendAsync(message);
                    await smtpClient.DisconnectAsync(true);
                }

                return true;
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка при отправке письма: {ex.Message}");
                return false;
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            string enteredCode = VerificationCodeTextBox.Text;
            string correctCode = Application.Current.Properties["ConfirmationCode"] as string;

            if (string.IsNullOrEmpty(enteredCode))
            {
                MessageBox.Show("Пожалуйста, введите код подтверждения.");
                return;
            }

            if (enteredCode != correctCode)
            {
                MessageBox.Show("Неверный код подтверждения.");
                return;
            }

            LoadingOverlay.Visibility = Visibility.Visible;
            RegisterButton.IsEnabled = false;

            try
            {
                AddUserToDatabase();
                MessageBox.Show("Регистрация успешно завершена!");

                EmailTextBox.Clear();
                PasswordBox.Clear();
                ConfirmPasswordBox.Clear();
                VerificationCodeTextBox.Clear();

                VerificationCodeTextBox.Visibility = Visibility.Collapsed;
                RegisterButton.Visibility = Visibility.Collapsed;
                VerifyCodeButton.Visibility = Visibility.Visible;

                NavigationService.Navigate(new Uri("Login.xaml", UriKind.Relative));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при регистрации: {ex.Message}");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                RegisterButton.IsEnabled = true;
            }
        }

        private void AddUserToDatabase()
        {
            string email = EmailTextBox.Text;
            string password = GetPassword();
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);

            string connectionString = "Host=shinkansen.proxy.rlwy.net;Port=44952;Username=postgres;Password=phugTZbbnUjmCaNFDounPPPeWBOBDUdJ;Database=railway";

            using (var connection = new NpgsqlConnection(connectionString))
            {
                connection.Open();

                var insertCommand = new NpgsqlCommand(
                    "INSERT INTO auth.users (email, password, role) VALUES (@email, @password, @role)",
                    connection);

                insertCommand.Parameters.AddWithValue("email", email);
                insertCommand.Parameters.AddWithValue("password", hashedPassword);
                insertCommand.Parameters.AddWithValue("role", "user");

                insertCommand.ExecuteNonQuery();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new Login());
        }
    }
}
