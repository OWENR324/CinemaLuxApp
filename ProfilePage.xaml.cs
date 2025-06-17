using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Npgsql;

namespace MyWpfApp
{
    public partial class ProfilePage : Page
    {
        private const string connString = "Host=shinkansen.proxy.rlwy.net;Port=44952;Username=postgres;Password=phugTZbbnUjmCaNFDounPPPeWBOBDUdJ;Database=railway";

        public ProfilePage()
        {
            InitializeComponent();
            LoadUserData();
        }

        private void LoadUserData()
        {
            if (!UserSession.IsLoggedIn)
            {
                UserEmailText.Text = "Вы не вошли в систему.";
                return;
            }

            // Отображение роли и email
            string roleText = UserSession.UserRole == "admin" ? "Администратор" : "Пользователь";
            UserRoleText.Text = roleText;
            UserEmailText.Text = UserSession.UserEmail;

            List<PurchaseHistoryItem> history = new List<PurchaseHistoryItem>();

            using (var conn = new NpgsqlConnection(connString))
            {
                conn.Open();

                string query = @"
                    SELECT id, movie_title, hall_id, seat_number, price, session_id
                    FROM auth.bookings
                    WHERE user_email = @user_email
                    ORDER BY id DESC
                    LIMIT 30"; // Ограничиваем показ 30 последними записями

                using var cmd = new NpgsqlCommand(query, conn);
                cmd.Parameters.AddWithValue("user_email", UserSession.UserEmail);

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    history.Add(new PurchaseHistoryItem
                    {
                        BookingId = reader.GetInt32(0),
                        MovieTitle = reader.GetString(1),
                        HallId = reader.GetInt32(2),
                        SeatNumber = reader.GetInt32(3),
                        Price = reader.GetDecimal(4),
                        SessionId = reader.GetInt32(5)
                    });
                }
            }

            PurchaseHistoryList.ItemsSource = history;
        }

      
    }

    public class PurchaseHistoryItem
    {
        public int BookingId { get; set; }
        public string MovieTitle { get; set; }
        public int HallId { get; set; }
        public int SeatNumber { get; set; }
        public decimal Price { get; set; }
        public int SessionId { get; set; }

        // Для отображения даты сеанса (можно преобразовать SessionId в дату при необходимости)
        public DateTime SessionDate { get; set; } = DateTime.Now;
    }
}