using System.Windows;
using System.Windows.Controls;

namespace LayoutsFromModel
{
    /// <summary>
    /// Модальное окно выбора базового формата рамки.
    /// </summary>
    public sealed class PaperFormatSelectionDialog : Window
    {
        /// <summary>
        /// Размер короткой стороны выбранного формата в миллиметрах.
        /// </summary>
        public double SelectedShortSide { get; private set; }

        public PaperFormatSelectionDialog()
        {
            Title = "Выбор формата рамки";
            Width = 200;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            StackPanel panel = new StackPanel { Margin = new Thickness(12) };
            panel.Children.Add(new TextBlock
            {
                Text = "Выберите формат:",
                Margin = new Thickness(0, 0, 0, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            AddFormatButton(panel, "A0", 841);
            AddFormatButton(panel, "A1", 594);
            AddFormatButton(panel, "A2", 420);
            AddFormatButton(panel, "A3", 297);
            AddFormatButton(panel, "A4", 210);

            Button cancelButton = new Button
            {
                Content = "Отмена",
                Width = 100,
                Height = 25,
                Margin = new Thickness(0, 6, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                IsCancel = true
            };
            panel.Children.Add(cancelButton);
            Content = panel;
        }

        private void AddFormatButton(Panel panel, string caption, double shortSide)
        {
            Button button = new Button
            {
                Content = caption,
                Height = 28,
                Margin = new Thickness(0, 0, 0, 6),
                Tag = shortSide
            };
            button.Click += OnFormatClick;
            panel.Children.Add(button);
        }

        private void OnFormatClick(object sender, RoutedEventArgs e)
        {
            SelectedShortSide = (double)((Button)sender).Tag;
            DialogResult = true;
        }
    }
}
