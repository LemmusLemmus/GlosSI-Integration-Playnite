using Playnite.SDK;
using System;
using System.Diagnostics;

namespace GlosSIIntegration.Models
{
    class PlayniteOverlay : Overlay
    {
        /// <inheritdoc/>
        private PlayniteOverlay(string overlayName) : base(overlayName) { }

        /// <summary>
        /// Creates a Playnite fullscreen mode overlay, if one should exist.
        /// </summary>
        /// <returns>If Playnite fullscreen mode should have an overlay, the overlay; <c>null</c> otherwise.</returns>
        public static PlayniteOverlay Create()
        {
            if (GlosSIIntegration.Api.ApplicationInfo.Mode == ApplicationMode.Fullscreen
                && GlosSIIntegration.GetSettings().UsePlayniteOverlay)
            {
                try
                {
                    return new PlayniteOverlay(GlosSIIntegration.GetSettings().PlayniteOverlayName);
                }
                catch (InvalidOperationException ex)
                {
                    logger.Warn($"Cannot create Playnite overlay: {ex.Message}");
                }
            }

            return null;
        }

        protected internal override void OnOverlayStarted(Process startedOverlay)
        {
            base.OnOverlayStarted(startedOverlay);

            // Not a great solution, since the overlay starting could be delayed
            // and Playnite might be focused when the user is doing something else...
            FocusRestorer.ReturnStolenFocusToProcess(startedOverlay, Process.GetCurrentProcess(), 5000);
        }
    }
}
