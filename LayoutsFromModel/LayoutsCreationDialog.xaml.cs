using System.Windows;

namespace LayoutsFromModel
{
    public enum LayoutsCreationAction
    {
        None,
        AutomaticBlocks,
        AutomaticSpdsFormats,
        ManualBlocks,
        ManualFrame,
        DeleteLayouts,
        Settings,
        Template
    }

    /// <summary>
    /// Главное окно выбора способа создания листов.
    /// </summary>
    public partial class LayoutsCreationDialog : Window
    {
        public LayoutsCreationAction SelectedAction { get; private set; }

        /// <summary>
        /// Создаёт окно выбора способа создания листов.
        /// </summary>
        public LayoutsCreationDialog()
        {
            InitializeComponent();
            SelectedAction = LayoutsCreationAction.None;
        }

        /// <summary>
        /// Выбирает автоматическую обработку блоков.
        /// </summary>
        private void OnAutomaticBlocksClick(object sender, RoutedEventArgs e)
        {
            Complete(LayoutsCreationAction.AutomaticBlocks);
        }

        /// <summary>
        /// Выбирает автоматическую обработку форматок СПДС.
        /// </summary>
        private void OnAutomaticSpdsFormatsClick(object sender, RoutedEventArgs e)
        {
            Complete(LayoutsCreationAction.AutomaticSpdsFormats);
        }

        /// <summary>
        /// Выбирает ручное указание блоков или форматок СПДС.
        /// </summary>
        private void OnManualBlocksClick(object sender, RoutedEventArgs e)
        {
            Complete(LayoutsCreationAction.ManualBlocks);
        }

        /// <summary>
        /// Выбирает ручное указание границ рамки.
        /// </summary>
        private void OnManualFrameClick(object sender, RoutedEventArgs e)
        {
            Complete(LayoutsCreationAction.ManualFrame);
        }

        /// <summary>
        /// Выбирает удаление существующих листов.
        /// </summary>
        private void OnDeleteLayoutsClick(object sender, RoutedEventArgs e)
        {
            Complete(LayoutsCreationAction.DeleteLayouts);
        }

        /// <summary>
        /// Открывает настройки создания листов.
        /// </summary>
        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            Complete(LayoutsCreationAction.Settings);
        }

        /// <summary>
        /// Открывает выбор шаблона листов.
        /// </summary>
        private void OnTemplateClick(object sender, RoutedEventArgs e)
        {
            Complete(LayoutsCreationAction.Template);
        }

        /// <summary>
        /// Сохраняет выбранное действие и закрывает окно.
        /// </summary>
        /// <param name="action">Выбранное действие.</param>
        private void Complete(LayoutsCreationAction action)
        {
            SelectedAction = action;
            DialogResult = true;
        }
    }
}
