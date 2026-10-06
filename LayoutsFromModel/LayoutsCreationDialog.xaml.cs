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
        Settings,
        Template
    }

    /// <summary>
    /// Главное окно выбора способа создания листов.
    /// </summary>
    public partial class LayoutsCreationDialog : Window
    {
        public LayoutsCreationAction SelectedAction { get; private set; }

        public LayoutsCreationDialog()
        {
            InitializeComponent();
            SelectedAction = LayoutsCreationAction.None;
        }

        private void OnAutomaticBlocksClick(object sender, RoutedEventArgs e)
        {
            Complete(LayoutsCreationAction.AutomaticBlocks);
        }

        private void OnAutomaticSpdsFormatsClick(object sender, RoutedEventArgs e)
        {
            Complete(LayoutsCreationAction.AutomaticSpdsFormats);
        }

        private void OnManualBlocksClick(object sender, RoutedEventArgs e)
        {
            Complete(LayoutsCreationAction.ManualBlocks);
        }

        private void OnManualFrameClick(object sender, RoutedEventArgs e)
        {
            Complete(LayoutsCreationAction.ManualFrame);
        }

        private void OnSettingsClick(object sender, RoutedEventArgs e)
        {
            Complete(LayoutsCreationAction.Settings);
        }

        private void OnTemplateClick(object sender, RoutedEventArgs e)
        {
            Complete(LayoutsCreationAction.Template);
        }

        private void Complete(LayoutsCreationAction action)
        {
            SelectedAction = action;
            DialogResult = true;
        }
    }
}
