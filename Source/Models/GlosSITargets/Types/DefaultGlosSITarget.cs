using GlosSIIntegration.Models.GlosSITargets.Files;
using System;

namespace GlosSIIntegration.Models.GlosSITargets.Types
{
    /// <summary>
    /// Represents a GlosSITarget used by default for Playnite games 
    /// without a specific GlosSITarget.
    /// </summary>
    internal class DefaultGlosSITarget : GlosSITarget
    {
        public DefaultGlosSITarget(string name) : base(name) { }

        public DefaultGlosSITarget() : base(
            GlosSIIntegration.GetSettings().DefaultOverlayName ??
            throw new NotSupportedException("DefaultOverlayName setting not set.")) { }

        public static bool Exists()
        {
            return !string.IsNullOrEmpty(GlosSIIntegration.GetSettings().DefaultOverlayName);
        }

        protected internal override GlosSITargetSettings.LaunchOptions GetPreferredLaunchOptions()
        {
            // If the same shortcut is used for the Playnite and Default overlay,
            // launch options should be the same as the Playnite target, since those actually do something.
            // Otherwise, simply launch nothing. That way this default overlay can be launched from Steam
            // to be used as simply an overlay, with no particular process associated with it.
            if (PlayniteGlosSITarget.Exists())
            {
                PlayniteGlosSITarget playniteTarget = new PlayniteGlosSITarget();

                if (playniteTarget.File.Name == File.Name)
                {
                    return playniteTarget.GetPreferredLaunchOptions();
                }
            }

            return new GlosSITargetSettings.LaunchOptions();
        }
    }
}
