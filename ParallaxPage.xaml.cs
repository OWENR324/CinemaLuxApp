using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace MyWpfApp
{
    public partial class ParallaxPage : Page
    {
        public ParallaxPage()
        {
            InitializeComponent(); // Инициализация XAML-компонентов
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Пример анимации
            var deepLayerAnimation = new DoubleAnimation
            {
                From = 0,
                To = 200,
                Duration = TimeSpan.FromSeconds(10),
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = true
            };
          

            var midLayerAnimation = new DoubleAnimation
            {
                From = 0,
                To = 100,
                Duration = TimeSpan.FromSeconds(15),
                RepeatBehavior = RepeatBehavior.Forever,
                AutoReverse = true
            };
            midTransform.BeginAnimation(TranslateTransform.XProperty, midLayerAnimation);
        }
    }
}
