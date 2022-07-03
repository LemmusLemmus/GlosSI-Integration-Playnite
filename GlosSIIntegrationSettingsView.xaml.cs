using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GlosSIIntegration
{
    public partial class GlosSIIntegrationSettingsView : UserControl
    {
        public GlosSIIntegrationSettingsView()
        {
            InitializeComponent();
            if(!GlosSIIntegration.GetSettings().UsePlayniteOverlay)
            {
                PlayniteOverlayNamePanel.IsEnabled = false;
            }
        }

        private void EditDefaultGlosSITarget_Click(object sender, RoutedEventArgs e)
        {
            // TODO: This would be better done via the GlosSI GUI, perphaps by adding a command line argument.
            System.Diagnostics.Process.Start(GlosSIIntegration.GetSettings().DefaultTargetPath);
        }

        private void UsePlayniteOverlay_Checked(object sender, RoutedEventArgs e)
        {
            PlayniteOverlayNamePanel.IsEnabled = true;
        }

        private void UsePlayniteOverlay_Unchecked(object sender, RoutedEventArgs e)
        {
            PlayniteOverlayNamePanel.IsEnabled = false;
        }
    }
}