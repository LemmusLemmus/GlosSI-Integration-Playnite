using GlosSIIntegration.Models.GlosSITargets.Types;
using Playnite.SDK;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models.Overlays.Types
{
    internal abstract class Overlay
    {
        protected static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>
        /// The GlosSI target associated with the overlay.
        /// </summary>
        public GlosSITarget Target { get; }

        /// <summary>
        /// If <see cref="State"/> needs to be accessed from a separate thread or modified, 
        /// use this lock to ensure thread safe access to it.
        /// </summary>
        public readonly object stateLock = new object();
        private MutableOverlayState state;
        /// <summary>
        /// Reset when a new overlay is started.
        /// <c>null</c> if no overlay has ever started.
        /// GlosSITargetProcess property is <c>null</c> when the overlay has exited 
        /// or access to the process has been relinquished to a different overlay.
        /// </summary>
        public IOverlayState State => state;


        /// <summary>
        /// Instantiates a new overlay object.
        /// <para>
        /// The object should be "disposed" of by calling <see cref="OnClosed"/> 
        /// or by having it replaced using <see cref="Replaces(Overlay)"/>.
        /// </para>
        /// </summary>
        /// <exception cref="System.InvalidOperationException">If the path to GlosSI 
        /// has not been set.</exception>
        protected Overlay(GlosSITarget target)
        {
            Target = target;
            state = null;
        }

        /// <summary>
        /// Checks if an overlay starts the same Steam shortcut as this process was started from.
        /// </summary>
        /// <param name="otherOverlay">The other overlay to compare with.</param>
        /// <returns>True if both overlays start the same Steam shortcut; false otherwise.</returns>
        public bool StartsSameShortcutAs(Overlay otherOverlay)
        {
            return Target.Equals(otherOverlay.Target);
        }

        protected internal async Task BeforeStarted()
        {
            lock (stateLock)
            {
                state = new MutableOverlayState()
                {
                    StartedByExtension = true
                };
            }

            await BeforeStartedCalled().ConfigureAwait(false);

            // Try to prevent GlosSITarget from taking focus when it starts.
            // Note: This assumes that no game is started between BeforeOverlayClosed() and OnOverlayClosed().
            if (!WinWindow.LockSetForegroundWindow())
            {
                logger.Warn("Failed to prevent GlosSITarget from stealing focus: LockSetForegroundWindow failed.");
            }
        }

        protected abstract Task BeforeStartedCalled();

        /// <summary>
        /// Called when the overlay process and window has started.
        /// Updates the <see cref="OverlayProcess"/> property.
        /// </summary>
        /// <param name="startedOverlay">The process of the started overlay.</param>
        protected internal void OnStarted(Process startedOverlay)
        {
            lock (stateLock)
            {
                state.GlosSITargetProcess = startedOverlay;
            }
            WinWindow.UnlockSetForegroundWindow();
            OnStartedCalled();
        }

        // Note: Not called for overlays not started by the extension.
        protected abstract void OnStartedCalled();

        /// <summary>
        /// Called if the overlay was started externally.
        /// </summary>
        /// <param name="startedOverlay">The overlay process.</param>
        protected void StartedExternally(Process startedOverlay)
        {
            lock (stateLock)
            {
                state = new MutableOverlayState()
                {
                    GlosSITargetProcess = startedOverlay
                };
            }
        }

        /// <summary>
        /// Called when the overlay is about to be purposefully closed. 
        /// The method should not be called if the overlay has already closed 
        /// or is closed externally.
        /// </summary>
        protected internal void BeforeClosed()
        {
            lock (stateLock)
            {
                state.ClosedByExtension = true;
            }
            BeforeClosedCalled();
        }

        protected abstract void BeforeClosedCalled();

        // TODO: Remove the now unused exit code parameter.
        /// <summary>
        /// Called when the overlay process has closed. Disposes resources, if necessary.
        /// </summary>
        /// <param name="overlayExitCode">The exit code of the overlay process.</param>
        protected internal async Task OnClosed(int overlayExitCode) {
            try
            {
                await OnClosedCalled(overlayExitCode).ConfigureAwait(false);
            }
            finally
            {
                lock (stateLock)
                {
                    state.GlosSITargetProcess?.Dispose();
                    state.GlosSITargetProcess = null;
                }
            }
        }

        protected abstract Task OnClosedCalled(int overlayExitCode);

        /// <summary>
        /// Called when this overlay replaces an old overlay.
        /// By default, moves the process from the old overlay to this overlay.
        /// Note: The overlay should not be replaced between BeforeStarted and OnStarted, BeforeClosed and OnClosed
        /// Note: The old overlay should be considered as simply having been newly instantiated: 
        /// its state is left untouched.
        /// </summary>
        /// <param name="otherOverlay">The old overlay that this overlay replaces.</param>
        /// <returns>The new overlay that replaces the old overlay (i.e. this overlay).</returns>
        protected internal Overlay Replaces(Overlay otherOverlay)
        {
            if (this != otherOverlay)
            {
                lock (stateLock)
                {
                    if (state?.GlosSITargetProcess != null)
                    {
                        // Should not happen.
                        throw new InvalidOperationException("Tried to replace a running overlay " +
                            "with another supposedly running overlay: this should be impossible.");
                    }

                    lock (otherOverlay.stateLock)
                    {
                        state = new MutableOverlayState(otherOverlay.state);
                        // The other overlay may no longer mess with the process.
                        otherOverlay.state.GlosSITargetProcess = null;
                    }
                }
            }
            else
            {
                // Should not happen?
                logger.Warn("Tried to replace an overlay with the same overlay.");
            }

            return this;
        }

        public interface IOverlayState
        {
            /// <summary>
            /// The GlosSITarget process associated with the overlay, if any.
            /// If null, the overlay no longer owns the process, 
            /// the process has been closed or it was never ran to begin with.
            /// </summary>
            Process GlosSITargetProcess { get; }
            bool StartedByExtension { get; }
            bool ClosedByExtension { get; }
        }

        private class MutableOverlayState : IOverlayState
        {
            public Process GlosSITargetProcess { get; set; }
            public bool StartedByExtension { get; set; }
            public bool ClosedByExtension { get; set; }

            public MutableOverlayState()
            {
                GlosSITargetProcess = null;
                StartedByExtension = false;
                ClosedByExtension = false;
            }

            public MutableOverlayState(IOverlayState otherState)
            {
                GlosSITargetProcess = otherState.GlosSITargetProcess;
                StartedByExtension = otherState.StartedByExtension;
                ClosedByExtension = otherState.ClosedByExtension;
            }
        }
    }
}
