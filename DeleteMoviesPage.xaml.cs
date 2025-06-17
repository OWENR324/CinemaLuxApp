using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Npgsql;

namespace MyWpfApp
{
    public partial class DeleteMoviesPage : Page
    {
        private const string connString = "Host=shinkansen.proxy.rlwy.net;Port=44952;Username=postgres;Password=phugTZbbnUjmCaNFDounPPPeWBOBDUdJ;Database=railway";

        public class Movie
        {
            public int Id { get; set; }
            public string Title { get; set; }
        }

        public class Session
        {
            public int Id { get; set; }
            public DateTime StartDateTime { get; set; }
            public int HallId { get; set; }
            public decimal Price { get; set; }

            public string SessionTime => $"{StartDateTime} - Hall: {HallId} - Price: {Price}";
        }

        public DeleteMoviesPage()
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

        private async void LoadMovies()
        {
            try
            {
                MoviesComboBox.Items.Clear();

                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                string query = "SELECT id, title FROM auth.movies";
                using var cmd = new NpgsqlCommand(query, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    MoviesComboBox.Items.Add(new Movie
                    {
                        Id = reader.GetInt32(0),
                        Title = reader.GetString(1)
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке фильмов: {ex.Message}");
            }
        }

        private async void LoadSessions(int movieId)
        {
            SessionsComboBox.Items.Clear();

            try
            {
                using var conn = new NpgsqlConnection(connString);
                await conn.OpenAsync();

                string query = "SELECT session_id, start_datetime, hall_id, price FROM auth.sessions WHERE movie_id = @MovieId";
                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@MovieId", movieId);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    SessionsComboBox.Items.Add(new Session
                    {
                        Id = reader.GetInt32(0),
                        StartDateTime = reader.GetDateTime(1),
                        HallId = reader.GetInt32(2),
                        Price = reader.GetDecimal(3)
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке сеансов: {ex.Message}");
            }
        }

        private void MoviesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MoviesComboBox.SelectedItem is Movie selectedMovie)
            {
                LoadSessions(selectedMovie.Id);
            }
        }

        private async void DeleteMovieButton_Click(object sender, RoutedEventArgs e)
        {
            if (MoviesComboBox.SelectedItem is Movie selectedMovie)
            {
                int movieId = selectedMovie.Id;

                try
                {
                    using var conn = new NpgsqlConnection(connString);
                    await conn.OpenAsync();
                    using var transaction = await conn.BeginTransactionAsync();
                    try
                    {
                        string deletePurchasesQuery = "DELETE FROM auth.user_purchase_history WHERE movie_id = @MovieId";
                        using (var cmd = new NpgsqlCommand(deletePurchasesQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@MovieId", movieId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string deleteBookingsQuery = "DELETE FROM auth.bookings WHERE session_id IN (SELECT session_id FROM auth.sessions WHERE movie_id = @MovieId)";
                        using (var cmd = new NpgsqlCommand(deleteBookingsQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@MovieId", movieId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string deleteSessionsQuery = "DELETE FROM auth.sessions WHERE movie_id = @MovieId";
                        using (var cmd = new NpgsqlCommand(deleteSessionsQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@MovieId", movieId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string deleteMovieQuery = "DELETE FROM auth.movies WHERE id = @MovieId";
                        using (var cmd = new NpgsqlCommand(deleteMovieQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@MovieId", movieId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        await transaction.CommitAsync();
                        MessageBox.Show("Фильм, его сеансы, бронирования и покупки были удалены.");

                        MoviesComboBox.Items.Clear();
                        LoadMovies();
                        SessionsComboBox.Items.Clear();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        MessageBox.Show($"Ошибка при удалении: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при установке соединения: {ex.Message}");
                }
            }
        }

        private async void DeleteSessionButton_Click(object sender, RoutedEventArgs e)
        {
            if (SessionsComboBox.SelectedItem is Session selectedSession)
            {
                int sessionId = selectedSession.Id;

                try
                {
                    using var conn = new NpgsqlConnection(connString);
                    await conn.OpenAsync();
                    using var transaction = await conn.BeginTransactionAsync();
                    try
                    {
                        string deletePurchasesQuery = "DELETE FROM auth.user_purchase_history WHERE session_id = @SessionId";
                        using (var cmd = new NpgsqlCommand(deletePurchasesQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@SessionId", sessionId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string deleteBookingsQuery = "DELETE FROM auth.bookings WHERE session_id = @SessionId";
                        using (var cmd = new NpgsqlCommand(deleteBookingsQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@SessionId", sessionId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        string deleteSessionQuery = "DELETE FROM auth.sessions WHERE session_id = @SessionId";
                        using (var cmd = new NpgsqlCommand(deleteSessionQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@SessionId", sessionId);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        await transaction.CommitAsync();
                        MessageBox.Show("Сеанс, его бронирования и покупки были удалены.");

                        if (MoviesComboBox.SelectedItem is Movie selectedMovie)
                        {
                            LoadSessions(selectedMovie.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        MessageBox.Show($"Ошибка при удалении: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при установке соединения: {ex.Message}");
                }
            }
        }
    }
}