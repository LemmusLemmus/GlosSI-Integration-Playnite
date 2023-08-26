using Playnite.SDK;
using System.Windows;
using System.Windows.Controls;

namespace GlosSIIntegration
{
    /// <summary>
    /// Interaction logic for ShortcutCreationView.xaml
    /// </summary>
    public partial class ShortcutCreationView : UserControl
    {
        private readonly ShortcutCreationViewModel shortcutCreationModel;

        internal ShortcutCreationView(ShortcutCreationViewModel viewModel)
        {
            InitializeComponent();
            viewModel.SetIconPreview(IconPreview);
            shortcutCreationModel = viewModel;
            DataContext = shortcutCreationModel;
        }

        /// <summary>
        /// Opens a link to the "Configuring the overlay" section on the GitHub wiki.
        /// </summary>
        private void Help_Click(object sender, RoutedEventArgs e)
        {
            GlosSIIntegrationSettingsViewModel.OpenLink("https://github.com/LemmusLemmus/GlosSI-Integration-Playnite/wiki/Getting-started#configuring-the-overlay");
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (shortcutCreationModel.Create())
            {
                Window.GetWindow(this).DialogResult = true;
                Window.GetWindow(this).Close();
            }
        }
    }
}