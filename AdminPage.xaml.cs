using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Npgsql;
using System.Windows.Controls;

namespace MyWpfApp
{
    public partial class AdminPage
    {
        // Строка подключения к базе данных
        private const string connString = "Host=shinkansen.proxy.rlwy.net;Port=44952;Username=postgres;Password=phugTZbbnUjmCaNFDounPPPeWBOBDUdJ;Database=railway";

        // Укажите свои ключи и имя бакета
        private const string BucketName = "storagemovies";
        private const string AccessKey = "YCAJETaUi1GTJuP_nqv8pDgwf";
        private const string SecretKey = "YCMzHFRHcYMojqLt1HqrjDW4IOJkYRdPD8dX9OOc";
        private const string EndpointUrl = "https://storage.yandexcloud.net";

        private string selectedImagePath;
        private string selectedTrailerPath;

        public AdminPage()
        {
            InitializeComponent();
        }

        private void SelectImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image Files|*.jpg;*.png;*.jpeg"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedImagePath = openFileDialog.FileName;
                ImageUrlTextBox.Text = selectedImagePath;
            }
        }

        private void SelectTrailerButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.mov;*.avi"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                selectedTrailerPath = openFileDialog.FileName;
                TrailerUrlTextBox.Text = selectedTrailerPath;
            }
        }

        private void DeleteMoviesButton_Click(object sender, RoutedEventArgs e)
        {
            var deleteMoviesPage = new DeleteMoviesPage();
            this.NavigationService.Navigate(deleteMoviesPage);
        }

        private async void AddMovieButton_Click(object sender, RoutedEventArgs e)
        {
            string title = TitleTextBox.Text;
            string description = DescriptionTextBox.Text;
            string imageUrl = ImageUrlTextBox.Text;
            string trailerUrl = TrailerUrlTextBox.Text;
            string hallIdText = HallIdTextBox.Text;
            string ratingText = RatingTextBox.Text;
            string genre = GenreTextBox.Text;
            string ageRestriction = (AgeRestrictionComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string durationText = DurationTextBox.Text;

            if (!decimal.TryParse(PriceTextBox.Text, out decimal price))
            {
                MessageBox.Show("Неверный формат цены.");
                return;
            }

            if (!float.TryParse(ratingText, out float rating) || rating < 0 || rating > 10)
            {
                MessageBox.Show("Рейтинг должен быть числом от 0 до 10.");
                return;
            }

            if (!int.TryParse(durationText, out int duration) || duration <= 0)
            {
                MessageBox.Show("Продолжительность должна быть положительным числом (в минутах).");
                return;
            }

            DateTime? startDate = ReleaseDatePicker.SelectedDate;
            if (!startDate.HasValue)
            {
                MessageBox.Show("Выберите дату начала сеанса.");
                return;
            }

            string selectedHour = (HourComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string selectedMinute = (MinuteComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();

            if (string.IsNullOrEmpty(selectedHour) || string.IsNullOrEmpty(selectedMinute))
            {
                MessageBox.Show("Пожалуйста, выберите время (часы и минуты).");
                return;
            }

            DateTime startDateTime = new DateTime(
                startDate.Value.Year,
                startDate.Value.Month,
                startDate.Value.Day,
                int.Parse(selectedHour),
                int.Parse(selectedMinute),
                0);

            if (string.IsNullOrEmpty(hallIdText) || !int.TryParse(hallIdText, out int hallId))
            {
                MessageBox.Show("Пожалуйста, введите правильный номер зала.");
                return;
            }

            try
            {
                // Загрузка изображения и трейлера в облако
                if (!string.IsNullOrEmpty(selectedImagePath))
                {
                    string imageFileName = $"movies/{Guid.NewGuid()}{Path.GetExtension(selectedImagePath)}";
                    imageUrl = await UploadFileToYandexCloud(selectedImagePath, imageFileName);
                }

                if (!string.IsNullOrEmpty(selectedTrailerPath))
                {
                    string trailerFileName = $"trailers/{Guid.NewGuid()}{Path.GetExtension(selectedTrailerPath)}";
                    trailerUrl = await UploadFileToYandexCloud(selectedTrailerPath, trailerFileName);
                }

                if (string.IsNullOrEmpty(imageUrl))
                {
                    MessageBox.Show("Не выбрано изображение фильма.");
                    return;
                }

                using (var conn = new NpgsqlConnection(connString))
                {
                    await conn.OpenAsync();

                    // Проверка на дубликат
                    string checkQuery = "SELECT COUNT(*) FROM auth.movies WHERE title = @Title AND start_date = @StartDate";
                    using (var checkCmd = new NpgsqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("Title", title);
                        checkCmd.Parameters.AddWithValue("StartDate", startDateTime);

                        long count = (long)await checkCmd.ExecuteScalarAsync();

                        if (count > 0)
                        {
                            MessageBox.Show("Фильм с таким названием и временем показа уже существует.");
                            return;
                        }
                    }

                    // Вставка нового фильма
                    var sql = @"INSERT INTO auth.movies 
                                (title, description, hall_id, price, start_date, image_url, trailer_url, 
                                 rating, genre, age_restriction, duration_minutes) 
                                VALUES (@Title, @Description, @HallId, @Price, @StartDate, @ImageUrl, @TrailerUrl,
                                        @Rating, @Genre, @AgeRestriction, @DurationMinutes)";

                    using (var cmd = new NpgsqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("Title", title);
                        cmd.Parameters.AddWithValue("Description", description);
                        cmd.Parameters.AddWithValue("HallId", hallId);
                        cmd.Parameters.AddWithValue("Price", price);
                        cmd.Parameters.AddWithValue("StartDate", startDateTime);
                        cmd.Parameters.AddWithValue("ImageUrl", imageUrl);
                        cmd.Parameters.AddWithValue("TrailerUrl", string.IsNullOrEmpty(trailerUrl) ? (object)DBNull.Value : trailerUrl);
                        cmd.Parameters.AddWithValue("Rating", rating);
                        cmd.Parameters.AddWithValue("Genre", genre);
                        cmd.Parameters.AddWithValue("AgeRestriction", ageRestriction);
                        cmd.Parameters.AddWithValue("DurationMinutes", duration);

                        await cmd.ExecuteNonQueryAsync();
                    }

                    MessageBox.Show("Фильм успешно добавлен в базу данных.");
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении фильма: {ex.Message}");
            }
        }

        private void ClearForm()
        {
            TitleTextBox.Text = "";
            DescriptionTextBox.Text = "";
            RatingTextBox.Text = "";
            GenreTextBox.Text = "";
            AgeRestrictionComboBox.SelectedIndex = 0;
            DurationTextBox.Text = "";
            HallIdTextBox.Text = "";
            PriceTextBox.Text = "";
            ReleaseDatePicker.SelectedDate = null;
            HourComboBox.SelectedIndex = 0;
            MinuteComboBox.SelectedIndex = 0;
            ImageUrlTextBox.Text = "";
            TrailerUrlTextBox.Text = "";
            selectedImagePath = null;
            selectedTrailerPath = null;
        }

        private void NavigateToAddSessionPage(object sender, RoutedEventArgs e)
        {
            NavigationService.Navigate(new AddSessionPage());
        }

        private async Task<string> UploadFileToYandexCloud(string filePath, string fileName)
        {
            try
            {
                var s3Client = new AmazonS3Client(
                    new BasicAWSCredentials(AccessKey, SecretKey),
                    new AmazonS3Config
                    {
                        RegionEndpoint = Amazon.RegionEndpoint.USEast1,
                        ServiceURL = EndpointUrl,
                        ForcePathStyle = true
                    }
                );

                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = BucketName,
                        Key = fileName,
                        InputStream = fileStream,
                        CannedACL = S3CannedACL.PublicRead
                    };

                    var response = await s3Client.PutObjectAsync(putRequest);
                    return $"https://{BucketName}.s3.yandexcloud.net/{fileName}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки файла: {ex.Message}");
                return null;
            }
        }
    }
}