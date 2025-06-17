using Npgsql;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MyWpfApp
{
    public partial class MovieDetailsPage : Page
    {
        private const string connString = "Host=shinkansen.proxy.rlwy.net;Port=44952;Username=postgres;Password=phugTZbbnUjmCaNFDounPPPeWBOBDUdJ;Database=railway";
        private int movieId;
        private bool isPlaying = false;
        private bool isTrailerLoaded = false;
        private bool isFullscreen = false;

        public MovieDetailsPage(int movieId)
        {
            InitializeComponent();
            this.movieId = movieId;
            LoadMovieDetails();
            SetupVideoPlayer();
        }

        private void SetupVideoPlayer()
        {
            MovieTrailer.LoadedBehavior = MediaState.Manual;
            MovieTrailer.UnloadedBehavior = MediaState.Stop;
            MovieTrailer.Volume = 0.7;
            MovieTrailer.MediaOpened += MovieTrailer_MediaOpened;
            MovieTrailer.MediaEnded += MovieTrailer_MediaEnded;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        private async void LoadMovieDetails()
        {
            try
            {
                Movie movie = await GetMovieDetails(movieId);

                MovieTitle.Text = movie.Title;
                MovieDescription.Text = movie.Description;
                MoviePrice.Text = $"Цена: {movie.Price:C}";

                // Отображаем дополнительную информацию
                RatingText.Text = $"{movie.Rating:0.0}";
                GenreText.Text = movie.Genre;
                AgeRestrictionText.Text = movie.AgeRestriction;

                // Форматируем длительность фильма
                if (movie.DurationMinutes > 0)
                {
                    int hours = movie.DurationMinutes / 60;
                    int minutes = movie.DurationMinutes % 60;
                    DurationText.Text = $"{hours} ч {minutes} мин";
                }

                if (!string.IsNullOrEmpty(movie.ImageUrl))
                {
                    try
                    {
                        MovieImage.Source = new BitmapImage(new Uri(movie.ImageUrl));
                    }
                    catch
                    {
                        MovieImage.Source = new BitmapImage(new Uri("pack://application:,,,/Images/default_movie.jpg"));
                    }
                }

                if (!string.IsNullOrEmpty(movie.TrailerUrl))
                {
                    MovieTrailer.Tag = movie.TrailerUrl;

                    try
                    {
                        VideoPoster.Source = new BitmapImage(new Uri(movie.ImageUrl));
                    }
                    catch
                    {
                        VideoPoster.Source = new BitmapImage(new Uri("pack://application:,,,/Images/default_movie.jpg"));
                    }
                }
                

                List<Session> sessions = await GetMovieSessions(movieId);
                SessionsWrapPanel.Items.Clear();
                foreach (var session in sessions)
                {
                    var sessionCard = CreateSessionCard(session);
                    SessionsWrapPanel.Items.Add(sessionCard);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}");
            }
        }

        private void PlayTrailerButton_Click(object sender, RoutedEventArgs e)
        {
            var trailerUrl = MovieTrailer.Tag as string;

            if (string.IsNullOrEmpty(trailerUrl))
            {
                MessageBox.Show("Трейлер недоступен.");
                return;
            }

            if (!isTrailerLoaded)
            {
                try
                {
                    MovieTrailer.Source = new Uri(trailerUrl);
                    isTrailerLoaded = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки трейлера: {ex.Message}");
                    return;
                }
            }
            else
            {
                StartPlayback();
            }
        }

        private void StartPlayback()
        {
            VideoPoster.Visibility = Visibility.Collapsed;
            PlayTrailerButton.Visibility = Visibility.Collapsed;
            MovieTrailer.Visibility = Visibility.Visible;
            VideoControlsPanel.Visibility = Visibility.Visible;

            MovieTrailer.Play();
            isPlaying = true;
            PlayPauseButton.Content = "❚❚";
        }

        private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (isPlaying)
            {
                MovieTrailer.Pause();
                PlayPauseButton.Content = "▶";
                isPlaying = false;
            }
            else
            {
                MovieTrailer.Play();
                PlayPauseButton.Content = "❚❚";
                isPlaying = true;
            }
        }

        private void FullscreenButton_Click(object sender, RoutedEventArgs e)
        {
            if (!isTrailerLoaded || MovieTrailer.Source == null) return;

            isFullscreen = true;

            var fullscreenWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                WindowState = WindowState.Maximized,
                Background = Brushes.Black,
                Title = "Трейлер фильма"
            };

            var fullscreenMedia = new MediaElement
            {
                Source = MovieTrailer.Source,
                LoadedBehavior = MediaState.Manual,
                UnloadedBehavior = MediaState.Stop,
                Stretch = Stretch.Uniform,
                Volume = MovieTrailer.Volume,
                Position = MovieTrailer.Position
            };

            if (isPlaying)
            {
                fullscreenMedia.Play();
            }

            var closeButton = new Button
            {
                Content = "✕",
                Style = (Style)FindResource("VideoControlButtonStyle"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 20, 20, 0)
            };

            closeButton.Click += (s, args) =>
            {
                MovieTrailer.Position = fullscreenMedia.Position;
                fullscreenWindow.Close();
            };

            var grid = new Grid();
            grid.Children.Add(fullscreenMedia);
            grid.Children.Add(closeButton);

            fullscreenWindow.Content = grid;
            fullscreenWindow.ShowDialog();

            isFullscreen = false;
        }

        private void MovieTrailer_MediaOpened(object sender, RoutedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StartPlayback();
            }));
        }

        private void MovieTrailer_MediaEnded(object sender, RoutedEventArgs e)
        {
            isPlaying = false;
            PlayPauseButton.Content = "▶";
            MovieTrailer.Position = TimeSpan.Zero;

            VideoPoster.Visibility = Visibility.Visible;
            PlayTrailerButton.Visibility = Visibility.Visible;
            MovieTrailer.Visibility = Visibility.Collapsed;
            VideoControlsPanel.Visibility = Visibility.Collapsed;
        }

        private async System.Threading.Tasks.Task<Movie> GetMovieDetails(int movieId)
        {
            Movie movie = null;

            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();
                string query = @"SELECT id, title, description, price, image_url, trailer_url, 
                                       rating, genre, age_restriction, duration_minutes 
                                FROM auth.movies WHERE id = @movieId";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("movieId", movieId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            movie = new Movie
                            {
                                Id = reader.GetInt32(reader.GetOrdinal("id")),
                                Title = reader.GetString(reader.GetOrdinal("title")),
                                Description = reader.GetString(reader.GetOrdinal("description")),
                                Price = reader.GetDecimal(reader.GetOrdinal("price")),
                                ImageUrl = reader.IsDBNull(reader.GetOrdinal("image_url")) ? null : reader.GetString(reader.GetOrdinal("image_url")),
                                TrailerUrl = reader.IsDBNull(reader.GetOrdinal("trailer_url")) ? null : reader.GetString(reader.GetOrdinal("trailer_url")),
                                Rating = reader.IsDBNull(reader.GetOrdinal("rating")) ? 0 : reader.GetDecimal(reader.GetOrdinal("rating")),
                                Genre = reader.IsDBNull(reader.GetOrdinal("genre")) ? "Не указан" : reader.GetString(reader.GetOrdinal("genre")),
                                AgeRestriction = reader.IsDBNull(reader.GetOrdinal("age_restriction")) ? "0+" : reader.GetString(reader.GetOrdinal("age_restriction")),
                                DurationMinutes = reader.IsDBNull(reader.GetOrdinal("duration_minutes")) ? 0 : reader.GetInt32(reader.GetOrdinal("duration_minutes"))
                            };
                        }
                    }
                }
            }

            return movie;
        }

        private async System.Threading.Tasks.Task<List<Session>> GetMovieSessions(int movieId)
        {
            List<Session> sessions = new List<Session>();

            using (var conn = new NpgsqlConnection(connString))
            {
                await conn.OpenAsync();
                string query = "SELECT session_id, start_datetime, hall_id, price FROM auth.sessions WHERE movie_id = @movieId";

                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("movieId", movieId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            sessions.Add(new Session
                            {
                                SessionId = reader.GetInt32(reader.GetOrdinal("session_id")),
                                StartDatetime = reader.GetDateTime(reader.GetOrdinal("start_datetime")),
                                HallId = reader.GetInt32(reader.GetOrdinal("hall_id")),
                                Price = reader.GetDecimal(reader.GetOrdinal("price"))
                            });
                        }
                    }
                }
            }

            return sessions;
        }

        private Border CreateSessionCard(Session session)
        {
            var card = new Border
            {
                Style = (Style)FindResource("SessionCardStyle"),
                ToolTip = $"Кликните для выбора мест на сеанс {session.StartDatetime:HH:mm}"
            };

            card.MouseLeftButtonDown += OnSessionClick;
            card.Tag = session;

            var stackPanel = new StackPanel();

            var startDatetimeTextBlock = new TextBlock
            {
                Text = $"📅 {session.StartDatetime:dd.MM.yyyy HH:mm}",
                FontSize = 14,
                Foreground = Brushes.White,
                Margin = new Thickness(5),
                FontWeight = FontWeights.SemiBold
            };

            var hallTextBlock = new TextBlock
            {
                Text = $"🎬 Зал: {session.HallId}",
                FontSize = 14,
                Foreground = Brushes.White,
                Margin = new Thickness(5)
            };

            var priceTextBlock = new TextBlock
            {
                Text = $"💵 {session.Price:C}",
                FontSize = 16,
                Foreground = Brushes.LimeGreen,
                Margin = new Thickness(5),
                FontWeight = FontWeights.Bold
            };

            stackPanel.Children.Add(startDatetimeTextBlock);
            stackPanel.Children.Add(hallTextBlock);
            stackPanel.Children.Add(priceTextBlock);

            card.Child = stackPanel;
            return card;
        }

        private void OnSessionClick(object sender, MouseButtonEventArgs e)
        {
            if (!UserSession.IsLoggedIn || string.IsNullOrEmpty(UserSession.UserEmail))
            {
                MessageBox.Show("Чтобы выбрать место, необходимо авторизоваться.", "Авторизация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var session = (Session)((Border)sender).Tag;

            if (session != null)
            {
                var seatSelectionPage = new SeatSelectionPage(session.SessionId);
                NavigationService.Navigate(seatSelectionPage);
            }
            else
            {
                MessageBox.Show("Ошибка: не удалось получить информацию о сеансе.");
            }
        }

        public class Movie
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public decimal Price { get; set; }
            public string ImageUrl { get; set; }
            public string TrailerUrl { get; set; }
            public decimal Rating { get; set; }
            public string Genre { get; set; }
            public string AgeRestriction { get; set; }
            public int DurationMinutes { get; set; }
        }

        public class Session
        {
            public int SessionId { get; set; }
            public DateTime StartDatetime { get; set; }
            public int HallId { get; set; }
            public decimal Price { get; set; }
        }
    }

    public static class MediaElementExtensions
    {
        public static bool IsPlaying(this MediaElement mediaElement)
        {
            return mediaElement.Position > TimeSpan.Zero && mediaElement.Position < mediaElement.NaturalDuration.TimeSpan;
        }
    }
}