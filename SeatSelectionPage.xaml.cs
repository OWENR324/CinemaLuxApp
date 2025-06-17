using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Threading.Tasks;

namespace MyWpfApp
{
    public partial class SeatSelectionPage : Page
    {
        private const string connString = "Host=shinkansen.proxy.rlwy.net;Port=44952;Username=postgres;Password=phugTZbbnUjmCaNFDounPPPeWBOBDUdJ;Database=railway";
        private int sessionId;
        private decimal ticketPrice;
        private string movieTitle;
        private int hallId;
        private string userEmail;
        private int selectedSeatNumber = -1;
        private int movieId;

        public SeatSelectionPage(int sessionId)
        {
            InitializeComponent();
            this.sessionId = sessionId;
            userEmail = UserSession.UserEmail;

            Loaded += SeatSelectionPage_Loaded;

            if (string.IsNullOrEmpty(userEmail))
            {
                MessageBox.Show("Пожалуйста, войдите в систему для бронирования мест.");
                // Убираем GoBack(), просто остаёмся на странице
                return;
            }
        }

        private void SeatSelectionPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSessionDetails();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private async void LoadSessionDetails()
        {
            try
            {
                Session session = await GetSessionDetails(sessionId);
                if (session == null)
                {
                    MessageBox.Show("Сеанс не найден!");
                    NavigationService?.GoBack();
                    return;
                }

                ticketPrice = session.Price;
                movieTitle = session.MovieTitle;
                hallId = session.HallId;
                movieId = session.MovieId;

                await DisplaySeats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}");
                NavigationService?.GoBack();
            }
        }

        private async Task<Session> GetSessionDetails(int sessionId)
        {
            using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            string query = @"SELECT s.session_id, s.price, m.title AS movie_title, s.hall_id, s.movie_id
                             FROM auth.sessions s
                             JOIN auth.movies m ON s.movie_id = m.id
                             WHERE s.session_id = @sessionId";

            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("sessionId", sessionId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Session
                {
                    SessionId = reader.GetInt32(0),
                    Price = reader.GetDecimal(1),
                    MovieTitle = reader.GetString(2),
                    HallId = reader.GetInt32(3),
                    MovieId = reader.GetInt32(4)
                };
            }
            return null;
        }

        public async Task DisplaySeats()
        {
            var bookedSeats = await GetBookedSeats(sessionId);

            var grid = new UniformGrid
            {
                Columns = 10,
                Rows = 10,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            for (int i = 1; i <= 100; i++)
            {
                var btn = new Button
                {
                    Content = i.ToString(),
                    Width = 30,
                    Height = 30,
                    Margin = new Thickness(2),
                    Tag = i,
                    FontSize = 10
                };

                if (bookedSeats.Contains(i))
                {
                    btn.Style = (Style)FindResource("OccupiedSeatStyle");
                    btn.IsEnabled = false;
                }
                else
                {
                    btn.Style = (Style)FindResource("AvailableSeatStyle");
                    btn.Click += SeatButton_Click;
                }

                grid.Children.Add(btn);
            }
            SeatsContainer.Content = grid;
        }

        private async Task<List<int>> GetBookedSeats(int sessionId)
        {
            var result = new List<int>();
            using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync();

            string query = "SELECT seat_number FROM auth.user_purchase_history WHERE session_id = @sessionId";
            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("sessionId", sessionId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0)) // Проверка на NULL, если seat_number может быть NULL
                    result.Add(reader.GetInt32(0));
            }

            return result;
        }


        private void SeatButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null) return;

            if (selectedSeatNumber != -1)
            {
                foreach (var child in ((UniformGrid)SeatsContainer.Content).Children)
                {
                    if (child is Button b && (int)b.Tag == selectedSeatNumber)
                    {
                        b.Style = (Style)FindResource("AvailableSeatStyle");
                        break;
                    }
                }
            }

            selectedSeatNumber = (int)btn.Tag;
            btn.Style = (Style)FindResource("SelectedSeatStyle");
            ConfirmButton.Visibility = Visibility.Visible;
            ConfirmButton.IsEnabled = true;
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            if (selectedSeatNumber == -1) return;

            var info = new SeatSelectionInfo
            {
                SessionId = sessionId,
                SeatNumber = selectedSeatNumber,
                UserEmail = userEmail,
                MovieTitle = movieTitle,
                HallId = hallId,
                MovieId = movieId,
                SessionPrice = ticketPrice // Используем SessionPrice вместо TicketPrice
            };

            NavigationService?.Navigate(new PaymentPage(info));
        }

        public class Session
        {
            public int SessionId { get; set; }
            public decimal Price { get; set; }
            public string MovieTitle { get; set; }
            public int HallId { get; set; }
            public int MovieId { get; set; }
        }
    }
}
