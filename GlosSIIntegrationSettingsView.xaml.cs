using System;
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
                System.Diagnostics.Process.Start(GlosSIIntegration.GetSettings().DefaultTargetPath);
            }
            catch (Exception ex)
            {
                GlosSIIntegration.API.Dialogs.ShowErrorMessage($"Failed to open the default target file: {ex.Message}", "GlosSI Integration");
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