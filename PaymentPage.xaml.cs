using System;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Net.Mail;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Npgsql;
using QRCoder;

namespace MyWpfApp
{
    public partial class PaymentPage : Page
    {
        private bool _isFormatting = false;

        private readonly SeatSelectionInfo info;
        private const string connString = "Host=shinkansen.proxy.rlwy.net;Port=44952;Username=postgres;Password=phugTZbbnUjmCaNFDounPPPeWBOBDUdJ;Database=railway";

        public PaymentPage(SeatSelectionInfo seatInfo)
        {
            InitializeComponent();
            info = seatInfo;
            AmountTextBlock.Text = $"Сумма к оплате: {info.SessionPrice} ₽";
        }

        private void CardNumberBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Разрешаем только цифры, заменяем ввод, не добавляем если не цифра
            if (!char.IsDigit(e.Text, 0))
            {
                e.Handled = true;
            }
        }

        private void CvcBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsDigitsOnly(e.Text);
        }

        private bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (!char.IsDigit(c)) return false;
            }
            return true;
        }

        private void CardNumberBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormatting) return;
            _isFormatting = true;

            var tb = sender as TextBox;
            int selectionStart = tb.SelectionStart;
            int digitsBeforeCursor = 0;

            // Считаем, сколько цифр до курсора
            for (int i = 0; i < selectionStart; i++)
            {
                if (char.IsDigit(tb.Text[i]))
                    digitsBeforeCursor++;
            }

            // Извлекаем только цифры из текста
            string digitsOnly = new string(tb.Text.Where(char.IsDigit).ToArray());

            // Форматируем с пробелами через каждые 4 цифры
            var formatted = "";
            for (int i = 0; i < digitsOnly.Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                    formatted += " ";
                formatted += digitsOnly[i];
            }

            tb.Text = formatted;

            // Рассчитываем новую позицию курсора с учетом пробелов
            int newSelectionStart = digitsBeforeCursor + digitsBeforeCursor / 4;
            if (newSelectionStart > tb.Text.Length)
                newSelectionStart = tb.Text.Length;

            tb.SelectionStart = newSelectionStart;
            tb.SelectionLength = 0;

            // Обновляем счетчик введенных цифр
            CardNumberCounter.Text = $"Введено: {digitsOnly.Length} из 16";

            _isFormatting = false;
        }



        private void ExpiryBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !Regex.IsMatch(e.Text, @"[\d/]");
        }

        private void ExpiryBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var text = ExpiryBox.Text;
            if (text.Length == 2 && !text.Contains("/"))
            {
                ExpiryBox.Text = text + "/";
                ExpiryBox.SelectionStart = ExpiryBox.Text.Length;
            }
        }

        private async void PayButton_Click(object sender, RoutedEventArgs e)
        {
            PayButton.IsEnabled = false;
            LoadingOverlay.Visibility = Visibility.Visible;

            try
            {
                // Убираем пробелы, оставляем только цифры
                string cardNumber = new string(CardNumberBox.Text.Where(char.IsDigit).ToArray());
                string cvc = CvcBox.Password.Trim();

                if (cardNumber.Length != 16)
                {
                    MessageBox.Show("Номер карты должен содержать ровно 16 цифр.");
                    return;
                }

                if (cvc.Length != 3)
                {
                    MessageBox.Show("CVC должен содержать ровно 3 цифры.");
                    return;
                }

                // Продолжаем логику оплаты, например:
                await SaveBooking(info.UserEmail, info.MovieTitle, info.HallId, info.SeatNumber, info.SessionPrice);

                int purchaseId = await AddToPurchaseHistory(info.UserEmail, info.MovieId, info.MovieTitle, info.SessionPrice, info.HallId, info.SeatNumber, new byte[0]);

                string baseUrl = "https://ticket-checker-production.up.railway.app";
                string qrText = $"{baseUrl}/check-ticket.html?id={purchaseId}";

                byte[] qrCodeBytes = GenerateQrCode(qrText);

                await UpdateQrCodeInPurchaseHistory(purchaseId, qrCodeBytes);

                await SendTicketEmailAsync(info.UserEmail, info.MovieTitle, info.HallId, info.SeatNumber, info.SessionPrice, qrCodeBytes);

                MessageBox.Show($"Оплата прошла успешно! Билет отправлен на почту.");
                NavigationService.Navigate(new MoviesPage());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                PayButton.IsEnabled = true;
            }
        }

        private async Task UpdateQrCodeInPurchaseHistory(int purchaseId, byte[] qrCodeBytes)
        {
            using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            string updateQuery = @"
        UPDATE auth.user_purchase_history 
        SET qr_code = @qrCode 
        WHERE id = @id";
            using var cmd = new NpgsqlCommand(updateQuery, conn);
            cmd.Parameters.AddWithValue("qrCode", qrCodeBytes);
            cmd.Parameters.AddWithValue("id", purchaseId);

            await cmd.ExecuteNonQueryAsync();
        }


        private byte[] GenerateQrCode(string text)
        {
            using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
            using (QRCodeData qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q))
            using (QRCode qrCode = new QRCode(qrCodeData))
            using (Bitmap qrCodeImage = qrCode.GetGraphic(20))
            using (MemoryStream ms = new MemoryStream())
            {
                qrCodeImage.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        private async Task SendTicketEmailAsync(string toEmail, string movieName, int hallNumber, int seatNumber, decimal price, byte[] qrCodeBytes)
        {
            // Получаем дату сеанса из базы данных
            DateTime sessionDateTime;
            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();
                string query = "SELECT start_datetime FROM auth.sessions WHERE session_id = @sessionId";
                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("sessionId", info.SessionId);
                    sessionDateTime = (DateTime)await cmd.ExecuteScalarAsync();
                }
            }

            var fromAddress = new MailAddress("krupkodaniil119@gmail.com", "Cinema Tickets");
            var toAddress = new MailAddress(toEmail);
            const string fromPassword = "rgpj gsuc iwlr nisv"; // Пароль приложения Gmail

            string subject = $"Ваш билет на фильм {movieName}";
            string body = $@"
Здравствуйте!

Ваш билет на фильм ""{movieName}"".

<b>Дата и время сеанса:</b> {sessionDateTime:dd.MM.yyyy HH:mm}
<b>Зал:</b> {hallNumber}
<b>Место:</b> {seatNumber}
<b>Цена:</b> {price:F2} ₽
<b>Дата покупки:</b> {DateTime.Now:dd.MM.yyyy HH:mm}

QR-код билета прикреплён к этому письму. Пожалуйста, сохраните его и предъявите при входе.

Спасибо за покупку!
";

            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = subject,
                Body = body,
                IsBodyHtml = true // Включаем HTML-форматирование
            })
            {
                using (var ms = new MemoryStream(qrCodeBytes))
                {
                    var attachment = new Attachment(ms, "ticket_qr.png", "image/png");
                    message.Attachments.Add(attachment);

                    using (var smtp = new SmtpClient
                    {
                        Host = "smtp.gmail.com",
                        Port = 587,
                        EnableSsl = true,
                        DeliveryMethod = SmtpDeliveryMethod.Network,
                        UseDefaultCredentials = false,
                        Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
                    })
                    {
                        await smtp.SendMailAsync(message);
                    }
                }
            }
        }

        private async Task SaveBooking(string userEmail, string movieTitle, int hallId, int seatNumber, decimal price)
        {
            using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();
            string query = @"
                INSERT INTO auth.bookings (user_email, movie_title, hall_id, seat_number, price, session_id) 
                VALUES (@userEmail, @movieTitle, @hallId, @seatNumber, @price, @sessionId)";
            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("userEmail", userEmail);
            cmd.Parameters.AddWithValue("movieTitle", movieTitle);
            cmd.Parameters.AddWithValue("hallId", hallId);
            cmd.Parameters.AddWithValue("seatNumber", seatNumber);
            cmd.Parameters.AddWithValue("price", price);
            cmd.Parameters.AddWithValue("sessionId", info.SessionId);
            await cmd.ExecuteNonQueryAsync();
        }
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }
        private async Task<int> AddToPurchaseHistory(string userEmail, int movieId, string movieTitle, decimal price, int hallId, int seatNumber, byte[] qrCodeBytes)
        {
            using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            // Сначала получим дату сеанса
            DateTime sessionDateTime;
            string getSessionQuery = "SELECT start_datetime FROM auth.sessions WHERE session_id = @sessionId";
            using (var getSessionCmd = new NpgsqlCommand(getSessionQuery, conn))
            {
                getSessionCmd.Parameters.AddWithValue("sessionId", info.SessionId);
                sessionDateTime = (DateTime)await getSessionCmd.ExecuteScalarAsync();
            }

            string insertQuery = @"
    INSERT INTO auth.user_purchase_history 
    (user_email, movie_id, movie_title, price, session_id, hall_id, seat_number, qr_code, session_datetime)
    VALUES (@email, @movieId, @title, @price, @sessionId, @hallId, @seatNumber, @qrCode, @sessionDatetime)
    RETURNING id";

            using var cmd = new NpgsqlCommand(insertQuery, conn);
            cmd.Parameters.AddWithValue("email", userEmail);
            cmd.Parameters.AddWithValue("movieId", movieId);
            cmd.Parameters.AddWithValue("title", movieTitle);
            cmd.Parameters.AddWithValue("price", price);
            cmd.Parameters.AddWithValue("sessionId", info.SessionId);
            cmd.Parameters.AddWithValue("hallId", hallId);
            cmd.Parameters.AddWithValue("seatNumber", seatNumber);
            cmd.Parameters.AddWithValue("qrCode", qrCodeBytes);
            cmd.Parameters.AddWithValue("sessionDatetime", sessionDateTime);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
    }
}
