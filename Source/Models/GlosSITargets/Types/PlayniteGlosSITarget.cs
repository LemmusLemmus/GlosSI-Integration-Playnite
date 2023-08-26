using GlosSIIntegration.Models.GlosSITargets.Files;
using System;

namespace GlosSIIntegration.Models.GlosSITargets.Types
{
    /// <summary>
    /// Represents a GlosSITarget used while browsing the Playnite library (in fullscreen mode).
    /// </summary>
    internal class PlayniteGlosSITarget : GlosSITarget
    {
        public PlayniteGlosSITarget(string name) : base(name) { }
        public PlayniteGlosSITarget() : base(
            GlosSIIntegration.GetSettings().PlayniteOverlayName ?? 
            throw new NotSupportedException("PlayniteOverlayName setting not set.")) { }

        public static bool Exists()
        {
            return !string.IsNullOrEmpty(GlosSIIntegration.GetSettings().PlayniteOverlayName);
        }

        protected internal override GlosSITargetSettings.LaunchOptions GetPreferredLaunchOptions()
        {
            return StartFromSteamLaunchOptions.GetLaunchPlayniteLibraryOptions();
        }
    }
}
