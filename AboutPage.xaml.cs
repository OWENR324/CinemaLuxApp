using System.Windows;
using System.Windows.Controls;

namespace MyWpfApp
{
    public partial class AboutPage : Page
    {
        public AboutPage()
        {
            InitializeComponent();
        }

        // Обработчик нажатия на кнопку "Забронировать билет"
        private void BookTicketButton_Click(object sender, RoutedEventArgs e)
        {
            // Переход на страницу MoviesPage
            NavigationService.Navigate(new MoviesPage());
        }
    }
}