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

        /// <summary>
        /// Shows a shortcut creation dialog.
        /// </summary>
        /// <param name="defaultName">The default name of the new shortcut. 
        /// Can be left as <c>null</c>.</param>
        /// <param name="defaultIconPath">The path to the default icon of the new shortcut. 
        /// Can be left as <c>null</c>.</param>
        /// <returns>The name of the created shortcut; <c>null</c> if no shortcut was created.</returns>
        public static string ShowDialog(string defaultName, string defaultIconPath)
        {
            Window dialogWindow = API.Instance.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowCloseButton = true,
                ShowMaximizeButton = true,
                ShowMinimizeButton = true
            });
            ShortcutCreationView shortcutCreationView = new ShortcutCreationView(defaultName, defaultIconPath);
            dialogWindow.Content = shortcutCreationView;
            dialogWindow.Title = ResourceProvider.GetString("LOC_GI_ShortcutCreationWindowTitle");
            dialogWindow.SizeToContent = SizeToContent.WidthAndHeight;

            if (dialogWindow.ShowDialog() == true)
            {
                return shortcutCreationView.shortcutCreationModel.ShortcutName;
            }
            else
            {
                return null;
            }
        }

        private ShortcutCreationView(string defaultName, string defaultIconPath)
        {
            InitializeComponent();
            shortcutCreationModel = new ShortcutCreationViewModel(defaultName, defaultIconPath, IconPreview);
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