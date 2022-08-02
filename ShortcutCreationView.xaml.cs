using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

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
            dialogWindow.Title = "Create a new Steam shortcut";
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

    public class ShortcutCreationViewModel : ObservableObject
    {
        private readonly Image iconPreview;

        private string shortcutName;
        public string ShortcutName { 
            get { return shortcutName; }
            set { if (shortcutName == value) return; SetValue(ref shortcutName, value); }
        }

        private string shortcutIconPath;
        public string ShortcutIconPath
        {
            get { return shortcutIconPath; }
            set
            {
                if (shortcutIconPath == value) return;
                SetValue(ref shortcutIconPath, value);

                if (!string.IsNullOrEmpty(value) && File.Exists(value))
                {
                    try
                    {
                        iconPreview.Source = new BitmapImage(new Uri(value, UriKind.RelativeOrAbsolute));
                        return;
                    }
                    catch { }
                }

                try
                {
                    iconPreview.Source = new BitmapImage(new Uri($"pack://application:,,,/" +
                        $"{Assembly.GetExecutingAssembly()};component/Resources/DefaultSteamShortcutIcon.png"));
                }
                catch (Exception e)
                {
                    // This could lead to log spamming.
                    LogManager.GetLogger().Error($"Failed to read \"DefaultSteamShortcutIcon.png\": {e}");
                    iconPreview.Source = null;
                }
            }
        }

        /// <summary>
        /// Creates a new <c>ShortcutCreationViewModel</c> object.
        /// </summary>
        /// <param name="defaultName">The default name of the new Steam shortcut.</param>
        /// <param name="defaultIconPath">The default icon path of the new Steam shortcut.</param>
        /// <param name="previewIconImage">The image used to display the currently selected icon.</param>
        public ShortcutCreationViewModel(string defaultName, string defaultIconPath, Image previewIconImage)
        {
            iconPreview = previewIconImage;
            ShortcutName = defaultName;
            ShortcutIconPath = defaultIconPath;
        }

        public RelayCommand<object> BrowseIcon
        {
            get => new RelayCommand<object>((o) =>
            {
                // TODO: All files are currently accepted,
                // as I am not aware of any exhaustive list of file types supported by Steam as icons.
                // It would probably suffice to simply verify that some common image formats
                // (i.e. primarily those supported by the Image class) work and filter those.
                string filePath = API.Instance.Dialogs.SelectFile("Image|*.*");
                if (!string.IsNullOrEmpty(filePath)) ShortcutIconPath = filePath;
            });
        }

        /// <summary>
        /// Verifies the Steam shortcut name.
        /// </summary>
        /// <param name="errors">The list of errors to which potential errors are added as descriptive messages.</param>
        /// <returns>true if the name is valid; false otherwise.</returns>
        private bool VerifyName(ref List<string> errors)
        {
            if (string.IsNullOrEmpty(ShortcutName))
            {
                errors.Add("The name of the Steam shortcut has not been set.");
                return false;
            }

            if (GlosSITarget.HasJsonFile(ShortcutName))
            {
                // TODO: Give the user the choice to use the already existing GlosSI target file instead.
                errors.Add("A GlosSI target file already exists with the chosen shortcut name.");
                LogManager.GetLogger().Warn("A GlosSI target file already exists with the chosen name.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Verifies the Steam shortcut icon path.
        /// </summary>
        /// <param name="errors">The list of errors to which potential errors are added as descriptive messages.</param>
        /// <returns>true if the name is valid; false otherwise.</returns>
        private bool VerifyIconPath(ref List<string> errors)
        {
            // The icon can be left unset if the user so wishes.
            if (string.IsNullOrEmpty(ShortcutIconPath))
            {
                return true;
            }

            if (!File.Exists(ShortcutIconPath))
            {
                errors.Add("The icon could not be found.");
                return false;
            }

            if (Path.GetFileName(ShortcutIconPath).EndsWith(".exe"))
            {
                List<MessageBoxOption> options = new List<MessageBoxOption>
                {
                    new MessageBoxOption("Continue", false, false),
                    new MessageBoxOption("Cancel", true, true)
                };

                if (API.Instance.Dialogs.ShowMessage("The icon path leads to an executable (.exe) file. Any transparency will be lost.",
                    "GlosSI Integration", MessageBoxImage.Warning, options).Equals(options[1]))
                {
                    return false;
                }
            }

            try
            {
                shortcutIconPath = Path.GetFullPath(ShortcutIconPath);
            }
            catch (Exception e)
            {
                LogManager.GetLogger().Error($"Unexpected error encountered when reading the icon path: {e.Message}\n{e}");
                errors.Add($"Unexpected error encountered when reading the icon path: {e.Message}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a Steam shortcut and a GlosSITarget.
        /// The new shortcut uses <see cref="ShortcutName"/> and <see cref="ShortcutIconPath"/>.
        /// Anything wrong with the name and icon is shown to the user.
        /// </summary>
        /// <returns>true if the name and icon was valid and a Steam shortcut was successfully created; false otherwise.</returns>
        public bool Create()
        {
            List<string> errors = new List<string>();

            if (VerifyName(ref errors) & VerifyIconPath(ref errors))
            {
                try
                {
                    GlosSITarget target = new GlosSITarget(ShortcutName, ShortcutIconPath);
                    target.Create();
                    return true;
                }
                catch (Exception ex)
                {
                    LogManager.GetLogger().Error($"Something went wrong when attempting to create the Steam shortcut: {ex.Message}\n{ex}");
                    API.Instance.Dialogs.ShowErrorMessage($"Something went wrong when attempting to create the Steam shortcut: {ex.Message}",
                        "GlosSI Integration");
                }
            }
            else if (errors.Count > 0)
            {
                API.Instance.Dialogs.ShowErrorMessage(string.Join("\n", errors), "GlosSI Integration");
            }

            return false;
        }
    }
}