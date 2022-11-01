using Playnite.SDK;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace GlosSIIntegration
{
    public partial class GlosSIIntegrationSettingsView : UserControl
    {
        public GlosSIIntegrationSettingsView()
        {
            InitializeComponent();
            UpdateIsEnabled();
        }

        /// <summary>
        /// Opens the default target .json file for the user to view/edit.
        /// </summary>
        private void EditDefaultGlosSITarget_Click(object sender, RoutedEventArgs e)
        {
            // TODO: This would be better done via the GlosSI GUI, perphaps by implementing a command line argument.
            try
            {
                Process.Start(GlosSIIntegration.GetSettings().DefaultTargetPath);
            }
            catch (Exception ex)
            {
                string message = string.Format(ResourceProvider.GetString("LOC_GI_ReadDefaultTargetUnexpectedError"), ex.Message);
                LogManager.GetLogger().Error(ex, message);
                GlosSIIntegration.Api.Dialogs.ShowErrorMessage(message, ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"));
            }
        }

        /// <summary>
        /// Opens a link to the "Tips and Tricks" page on the GitHub wiki.
        /// </summary>
        private void TipsAndTricks_Click(object sender, RoutedEventArgs e)
        {
            GlosSIIntegrationSettingsViewModel.OpenLink("https://github.com/LemmusLemmus/GlosSI-Integration-Playnite/wiki/Tips-and-tricks");
        }

        /// <summary>
        /// Opens a link to the "Configuring settings" page on the GitHub wiki.
        /// </summary>
        private void Help_Click(object sender, RoutedEventArgs e)
        {
            GlosSIIntegrationSettingsViewModel.OpenLink("https://github.com/LemmusLemmus/GlosSI-Integration-Playnite/wiki/Getting-started#configuring-settings");
        }

        /// <summary>
        /// Updates the <c>IsEnabled</c> property of relevant elements to match the current settings.
        /// </summary>
        private void UpdateIsEnabled(object sender, RoutedEventArgs e)
        {
            UpdateIsEnabled();
        }

        /// <summary>
        /// Updates the <c>IsEnabled</c> property of relevant elements to match the current settings.
        /// </summary>
        private void UpdateIsEnabled()
        {
            // "?? true" should not be reachable.
            UsePlayniteOverlayCheckBox.IsEnabled = UseIntegrationFullscreenCheckBox.IsChecked ?? true;
            PlayniteOverlayNamePanel.IsEnabled = UsePlayniteOverlayCheckBox.IsEnabled && (UsePlayniteOverlayCheckBox.IsChecked ?? true);

            DefaultOverlayNamePanel.IsEnabled = UseDefaultOverlayCheckBox.IsEnabled && (UseDefaultOverlayCheckBox.IsChecked ?? true);
        }
    }
}