using System;
using System.Windows;
using System.Windows.Controls;
using Npgsql;

namespace MyWpfApp
{
    /// <summary>
    /// Логика взаимодействия для AddSessionPage.xaml
    /// </summary>
    public partial class AddSessionPage : Page
    {
        private const string connString = "Host=shinkansen.proxy.rlwy.net;Port=44952;Username=postgres;Password=phugTZbbnUjmCaNFDounPPPeWBOBDUdJ;Database=railway";

        public AddSessionPage()
        {
            InitializeComponent();
            LoadMovies();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.NavigationService != null)
            {
                this.NavigationService.GoBack();
            }
        }


        // Метод для загрузки списка фильмов из базы данных
        private async void LoadMovies()
        {
            try
            {
                using (var conn = new NpgsqlConnection(connString))
                {
                    await conn.OpenAsync();

                    string query = "SELECT id, title FROM auth.movies";
                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                // Добавляем фильмы в ComboBox
                                MoviesComboBox.Items.Add(new ComboBoxItem
                                {
                                    Content = reader["title"].ToString(),
                                    Tag = reader["id"]
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке фильмов: {ex.Message}");
            }
        }

        // Метод для добавления сеанса
        private async void AddSessionButton_Click(object sender, RoutedEventArgs e)
        {
            // Проверка, выбран ли фильм
            if (MoviesComboBox.SelectedItem == null)
            {
                MessageBox.Show("Выберите фильм.");
                return;
            }

            // Проверка, выбрана ли дата
            DateTime? sessionDate = SessionDatePicker.SelectedDate;
            if (!sessionDate.HasValue)
            {
                MessageBox.Show("Выберите дату сеанса.");
                return;
            }

            // Получение выбранного времени
            int hour = int.Parse(((ComboBoxItem)HourComboBox.SelectedItem).Content.ToString());
            int minute = int.Parse(((ComboBoxItem)MinuteComboBox.SelectedItem).Content.ToString());
            TimeSpan sessionTime = new TimeSpan(hour, minute, 0);

            // Поле для цены
            decimal price;
            if (!decimal.TryParse(SessionPriceTextBox.Text, out price))
            {
                MessageBox.Show("Неверный формат цены.");
                return;
            }

            // Получение выбранного зала
            int hallId = HallComboBox.SelectedIndex + 1;

            // Создание времени сеанса
            DateTime sessionStartDateTime = sessionDate.Value.Add(sessionTime);

            // Получение выбранного фильма
            var selectedMovie = (ComboBoxItem)MoviesComboBox.SelectedItem;
            int movieId = (int)selectedMovie.Tag;

            // Проверка на существование одинакового сеанса
            if (await SessionExists(movieId, sessionStartDateTime, hallId))
            {
                MessageBox.Show("Этот сеанс уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Запрос на добавление сеанса
                string query = "INSERT INTO auth.sessions (movie_id, start_datetime, hall_id, price) " +
                               "VALUES (@MovieId, @StartDatetime, @HallId, @Price)";

                using (var conn = new NpgsqlConnection(connString))
                {
                    await conn.OpenAsync();
                    using (var cmd = new NpgsqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("MovieId", movieId);
                        cmd.Parameters.AddWithValue("StartDatetime", sessionStartDateTime);
                        cmd.Parameters.AddWithValue("HallId", hallId);
                        cmd.Parameters.AddWithValue("Price", price);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                MessageBox.Show("Сеанс успешно добавлен.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении сеанса: {ex.Message}");
            }
        }

        // Метод для проверки существования сеанса
        private async Task<bool> SessionExists(int movieId, DateTime sessionStartDateTime, int hallId)
        {
            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();

                // Проверяем, существует ли уже такой сеанс в базе данных
                string query = "SELECT COUNT(*) FROM auth.sessions WHERE movie_id = @MovieId AND start_datetime = @StartDatetime AND hall_id = @HallId";
                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("MovieId", movieId);
                    cmd.Parameters.AddWithValue("StartDatetime", sessionStartDateTime);
                    cmd.Parameters.AddWithValue("HallId", hallId);

                    var result = await cmd.ExecuteScalarAsync();
                    return Convert.ToInt32(result) > 0; // Если больше 0, значит сеанс уже существует
                }
            }
        }
    }
}
