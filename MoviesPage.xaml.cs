using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace MyWpfApp
{
    public partial class MoviesPage : Page
    {
        private const string connString = "Host=shinkansen.proxy.rlwy.net;Port=44952;Username=postgres;Password=phugTZbbnUjmCaNFDounPPPeWBOBDUdJ;Database=railway";

        public MoviesPage()
        {
            InitializeComponent();
            LoadMovies();
        }

        private async void LoadMovies()
        {
            await Task.Delay(300); // Короткая задержка

            try
            {
                List<Movie> movies = await Task.Run(async () =>
                {
                    var loadedMovies = new List<Movie>();

                    using (var conn = new NpgsqlConnection(connString))
                    {
                        await conn.OpenAsync();
                        string query = "SELECT id, title, description, price, image_url FROM auth.movies";

                        using (var cmd = new NpgsqlCommand(query, conn))
                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                loadedMovies.Add(new Movie
                                {
                                    Id = reader.GetInt32(reader.GetOrdinal("id")),
                                    Title = reader.GetString(reader.GetOrdinal("title")),
                                    Description = reader.GetString(reader.GetOrdinal("description")),
                                    Price = reader.GetDecimal(reader.GetOrdinal("price")),
                                    ImageUrl = reader.GetString(reader.GetOrdinal("image_url"))
                                });
                            }
                        }
                    }

                    return loadedMovies;
                });

                // Обновляем UI на главном потоке
                MoviesWrapPanel.Children.Clear();

                foreach (var movie in movies)
                {
                    var movieCopy = movie; // для замыкания
                    await Dispatcher.InvokeAsync(() =>
                    {
                        MoviesWrapPanel.Children.Add(CreateMovieCard(movieCopy));
                    });
                }

                LoadingContainer.Visibility = Visibility.Collapsed;
                MoviesScrollViewer.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки фильмов: {ex.Message}");
            }
        }

        private Border CreateMovieCard(Movie movie)
        {
            var card = new Border
            {
                Style = (Style)FindResource("MovieCardStyle"),
                Tag = movie.Id
            };

            card.MouseLeftButtonUp += (sender, e) => NavigateToMovieDetails(movie.Id);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(250) }); // Постер
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });     // Название
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Описание (занимает все оставшееся место)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });    // Цена

            // Постер фильма (без изменений)
            var imageBorder = new Border
            {
                CornerRadius = new CornerRadius(12, 12, 0, 0),
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0x0F, 0x34, 0x60)),
                Height = 250,
                ClipToBounds = true
            };

            var image = new Image
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Загрузка изображения (без изменений)
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(!string.IsNullOrEmpty(movie.ImageUrl) ? movie.ImageUrl : "pack://application:,,,/Images/default_movie.jpg");
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                image.Source = bitmap;
            }
            catch
            {
                image.Source = new BitmapImage(new Uri("pack://application:,,,/Images/default_movie.jpg"));
            }

            imageBorder.Child = image;
            Grid.SetRow(imageBorder, 0);
            grid.Children.Add(imageBorder);

            // Название фильма (с фиксированной высотой)
            var titleTextBlock = new TextBlock
            {
                Text = movie.Title,
                Style = (Style)FindResource("MovieCardTitleStyle")
            };
            Grid.SetRow(titleTextBlock, 1);
            grid.Children.Add(titleTextBlock);

            // Описание (с фиксированной высотой)
            var descriptionTextBlock = new TextBlock
            {
                Text = movie.Description.Length > 100 ? movie.Description.Substring(0, 100) + "..." : movie.Description,
                Style = (Style)FindResource("MovieCardDescriptionStyle")
            };
            Grid.SetRow(descriptionTextBlock, 2);
            grid.Children.Add(descriptionTextBlock);

            // Цена (теперь всегда будет видна)
            var priceTextBlock = new TextBlock
            {
                Text = $"{movie.Price:C}",
                Style = (Style)FindResource("MovieCardPriceStyle")
            };
            Grid.SetRow(priceTextBlock, 3);
            grid.Children.Add(priceTextBlock);

            card.Child = grid;
            return card;
        }

        private void NavigateToMovieDetails(int movieId)
        {
            NavigationService.Navigate(new MovieDetailsPage(movieId));
        }
    }
}