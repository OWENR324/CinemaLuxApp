using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Npgsql;

namespace MyWpfApp
{
    public partial class TicketsPage : Page
    {
        private readonly string connectionString =
            "Host=shinkansen.proxy.rlwy.net;Port=44952;Username=postgres;Password=phugTZbbnUjmCaNFDounPPPeWBOBDUdJ;Database=railway";

        private ObservableCollection<Ticket> MyTickets = new();

        public TicketsPage()
        {
            InitializeComponent();
            LoadUserTickets();
        }

        private void LoadUserTickets()
        {
            if (!UserSession.IsLoggedIn || string.IsNullOrEmpty(UserSession.UserEmail))
            {
                MessageBox.Show("Вы не авторизованы!");
                return;
            }

            string userEmail = UserSession.UserEmail;
            var tickets = new ObservableCollection<Ticket>();

            const string query = @"
    SELECT id, movie_title, session_id, price, purchase_date, hall_id, seat_number, qr_code, session_datetime
    FROM auth.user_purchase_history
    WHERE user_email = @user_email
    ORDER BY purchase_date DESC";

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@user_email", userEmail);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                byte[] qrBytes = reader.IsDBNull(7) ? null : (byte[])reader["qr_code"];

                tickets.Add(new Ticket
                {
                    Id = reader.GetInt32(0),
                    MovieTitle = reader.GetString(1),
                    SessionId = reader.GetInt32(2),
                    Price = reader.GetDecimal(3),
                    PurchaseDate = reader.GetDateTime(4),
                    HallId = reader.GetInt32(5),
                    SeatNumber = reader.GetInt32(6),
                    QrCodeImage = ConvertByteArrayToImage(qrBytes),
                    SessionDatetime = reader.GetDateTime(8) // Добавлено получение даты сеанса
                });
            }

            MyTickets = tickets;
            TicketsList.ItemsSource = MyTickets;
        }

        private BitmapImage ConvertByteArrayToImage(byte[] byteArray)
        {
            if (byteArray == null || byteArray.Length == 0) return null;

            using var stream = new MemoryStream(byteArray);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private async void RefundButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not int ticketId) return;

            if (MessageBox.Show("Вы уверены?", "Подтверждение", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            var border = FindParent<Border>(button);
            if (border == null) return;

            var sb = new Storyboard();

            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            Storyboard.SetTarget(fade, border);
            Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));

            var shrink = new DoubleAnimation(1, 0.8, TimeSpan.FromMilliseconds(300));
            Storyboard.SetTarget(shrink, border);
            Storyboard.SetTargetProperty(shrink, new PropertyPath("RenderTransform.ScaleY"));

            border.RenderTransform = new ScaleTransform(1, 1);
            sb.Children.Add(fade);
            sb.Children.Add(shrink);

            sb.Completed += async (s, _) =>
            {
                if (RefundTicket(ticketId))
                {
                    var itemToRemove = MyTickets.FirstOrDefault(t => t.Id == ticketId);
                    if (itemToRemove != null)
                        MyTickets.Remove(itemToRemove);
                }
                else
                {
                    MessageBox.Show("Ошибка при возврате.");
                }
            };

            sb.Begin();
        }

        private bool RefundTicket(int ticketId)
        {
            const string query = "DELETE FROM auth.user_purchase_history WHERE id = @ticket_id";

            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("@ticket_id", ticketId);

            return command.ExecuteNonQuery() > 0;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService?.CanGoBack == true)
                NavigationService.GoBack();
        }

        private void QrCode_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Image image || image.Source is not BitmapImage qrImage) return;

            var window = new Window
            {
                Title = "QR-код билета",
                Width = 400,
                Height = 400,
                Content = new Image
                {
                    Source = qrImage,
                    Stretch = Stretch.Uniform
                },
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = Brushes.Black
            };

            window.ShowDialog();
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null && parent is not T)
                parent = VisualTreeHelper.GetParent(parent);
            return parent as T;
        }
    }

    public class Ticket
    {
        public int Id { get; set; }
        public string MovieTitle { get; set; }
        public int SessionId { get; set; }
        public int HallId { get; set; }
        public int SeatNumber { get; set; }
        public decimal Price { get; set; }
        public DateTime PurchaseDate { get; set; }
        public DateTime SessionDatetime { get; set; } // Добавлено новое свойство
        public BitmapImage QrCodeImage { get; set; }
    }
}
