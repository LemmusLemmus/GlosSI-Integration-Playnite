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

        private void EditDefaultGlosSITarget_Click(object sender, RoutedEventArgs e)
        {
            // TODO: This would be better done via the GlosSI GUI, perphaps by implementing a command line argument.
            try
            {
                Process.Start(GlosSIIntegration.GetSettings().DefaultTargetPath);
            }
            catch (Exception ex)
            {
                LogManager.GetLogger().Error("Failed to open the default target file: " + ex);
                GlosSIIntegration.Api.Dialogs.ShowErrorMessage($"Failed to open the default target file: {ex.Message}",
                    "GlosSI Integration");
            }
            
        }

        private void Help_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("https://github.com/LemmusLemmus/GlosSI-Integration-Playnite#readme");
            }
            catch (Exception ex)
            {
                LogManager.GetLogger().Error("Failed to open the help link: " + ex);
                GlosSIIntegration.Api.Dialogs.ShowErrorMessage("Failed to open the help link " +
                    $"\"https://github.com/LemmusLemmus/GlosSI-Integration-Playnite#readme\": {ex.Message}",
                    "GlosSI Integration");
            }
        }

        private void UpdateIsEnabled(object sender, RoutedEventArgs e)
        {
            UpdateIsEnabled();
        }

        private void UpdateIsEnabled()
        {
            // "?? true" should not be reachable.
            UsePlayniteOverlayCheckBox.IsEnabled = UseIntegrationFullscreenCheckBox.IsChecked ?? true;
            PlayniteOverlayNamePanel.IsEnabled = UsePlayniteOverlayCheckBox.IsEnabled && (UsePlayniteOverlayCheckBox.IsChecked ?? true);
        }
    }
}