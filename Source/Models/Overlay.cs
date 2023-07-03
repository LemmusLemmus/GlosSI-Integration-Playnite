using Playnite.SDK;
using System.Diagnostics;

namespace GlosSIIntegration.Models
{
    abstract class Overlay
    {
        /// <summary>
        /// The GlosSI Steam shortcut associated with the overlay.
        /// </summary>
        public GlosSISteamShortcut SteamShortcut { get; }

        /// <summary>
        /// The GlosSITarget process associated with the overlay, if any.
        /// </summary>
        protected internal Process OverlayProcess { get; private set; }

        protected static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>
        /// Instantiates a new overlay object.
        /// <para>
        /// The object should be "disposed" of by calling <see cref="OnOverlayClosed"/> 
        /// or by having it replaced using <see cref="Replaces(Overlay)"/>.
        /// </para>
        /// </summary>
        /// <param name="overlayName">The name of the overlay to be created. 
        /// Must correspond to a known existing overlay.</param>
        /// <exception cref="System.InvalidOperationException">If the path to GlosSI 
        /// has not been set.</exception>
        public Overlay(string overlayName)
        {
            OverlayProcess = null;
            SteamShortcut = new GlosSISteamShortcut(overlayName);
        }

        /// <summary>
        /// Checks if an overlay starts the same Steam shortcut as this process was started from.
        /// </summary>
        /// <param name="otherOverlay">The other overlay to compare with.</param>
        /// <returns>True if both overlays start the same Steam shortcut; false otherwise.</returns>
        public bool StartsSameShortcutAs(Overlay otherOverlay)
        {
            return SteamShortcut.Equals(otherOverlay.SteamShortcut);
        }

        /// <summary>
        /// Called when the overlay process has started. 
        /// Updates the <see cref="OverlayProcess"/> property.
        /// </summary>
        /// <param name="startedOverlay">The process of the started overlay.</param>
        protected internal virtual void OnOverlayStarted(Process startedOverlay) {
            OverlayProcess = startedOverlay;
        }

        /// <summary>
        /// Called when the overlay process has closed. Disposes resources, if necessary.
        /// </summary>
        /// <param name="overlayExitCode">The exit code of the overlay process.</param>
        protected internal virtual void OnOverlayClosed(int overlayExitCode) {
            OverlayProcess.Dispose();
        }

        /// <summary>
        /// Called when the overlay is about to be purposefully closed. 
        /// The method should not be called if the overlay has already closed 
        /// or is closed externally.
        /// </summary>
        protected internal virtual void BeforeOverlayClosed() { }

        /// <summary>
        /// Called when this overlay replaces an old overlay.
        /// By default, moves the process from the old overlay to this overlay.
        /// </summary>
        /// <param name="otherOverlay">The old overlay that this overlay replaces.</param>
        /// <returns>This overlay (i.e. the new overlay that replaces the old overlay).</returns>
        protected internal virtual Overlay Replaces(Overlay otherOverlay)
        {
            OverlayProcess = otherOverlay.OverlayProcess;
            otherOverlay.OverlayProcess = null;
            return this;
        }
    }
}
