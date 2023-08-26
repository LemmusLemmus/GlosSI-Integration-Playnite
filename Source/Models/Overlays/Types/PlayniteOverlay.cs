using GlosSIIntegration.Models.GlosSITargets.Types;
using GlosSIIntegration.Models.SteamLauncher;
using Playnite.SDK;
using System;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models.Overlays.Types
{
    class PlayniteOverlay : SteamStartableOverlay
    {
        private PlayniteOverlay() : base(new PlayniteGlosSITarget()) { }

        /// <summary>
        /// Creates a Playnite (fullscreen mode) overlay, if one should exist.
        /// </summary>
        /// <returns>The overlay if Playnite should have an overlay; <c>null</c> otherwise.</returns>
        public static PlayniteOverlay Create()
        {
            if (GlosSIIntegration.Api.ApplicationInfo.Mode == ApplicationMode.Fullscreen
                && GlosSIIntegration.GetSettings().UsePlayniteOverlay)
            {
                try
                {
                    return new PlayniteOverlay();
                }
                catch (InvalidOperationException ex)
                {
                    logger.Warn($"Cannot create Playnite overlay: {ex.Message}");
                }
            }

            return null;
        }

        protected override async Task OnClosedCalled(int overlayExitCode)
        {
            // We switch to Steam Big Picture mode if the Playnite overlay is closed externally.
            // If Steam is already in big picture mode, this simply serves to switch quicker,
            // since Steam will take focus once it realizes that overlay has quit.
            if (!State.ClosedByExtension)
            {
                logger.Debug("The Playnite overlay was closed externally: " +
                    "starting Steam Big Picture mode.");
                SteamBigPictureMode.Open();
            }

            await base.OnClosedCalled(overlayExitCode).ConfigureAwait(false);
        }

        protected override void OnStartedCalled() { }

        protected override void BeforeClosedCalled() { }
    }
}
