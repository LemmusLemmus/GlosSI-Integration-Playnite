using System;
using System.Diagnostics;
using System.Threading.Tasks;
using GlosSIIntegration.Models.GlosSITargets.Files;
using GlosSIIntegration.Models.GlosSITargets.Types;
using GlosSIIntegration.Models.SteamLauncher;

namespace GlosSIIntegration.Models.Overlays.Types
{
    /// <summary>
    /// Represents an unidentified overlay, started from outside this extension.
    /// </summary>
    internal class ExternallyStartedOverlay : Overlay, IDisposable
    {
        private ExternallyStartedOverlay(string name, Process process) : base(new UnidentifiedGlosSITarget(name))
        {
            StartedExternally(process);
        }

        /// <summary>
        /// Does nothing if overlay has been replaced or closed, otherwise disposes the process.
        /// </summary>
        public void Dispose()
        {
            State.GlosSITargetProcess?.Dispose();
        }

        public Task<int> WaitForExit()
        {
            return State.GlosSITargetProcess.WaitForExitAsyncSafe();
        }

        public static async Task<ExternallyStartedOverlay> GetCurrent()
        {
            return await GetExternallyStartedOverlay(GlosSITargetProcess.GetRunning()).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the externally started overlay from the GlosSITarget process <paramref name="alreadyRunningProcess"/>.
        /// Passing <paramref name="alreadyRunningProcess"/> means giving away the responsibility 
        /// of disposing the process to the overlay.
        /// </summary>
        /// <param name="alreadyRunningProcess"></param>
        /// <returns>The externally started overlay, or <c>null</c> if no overlay could be found or 
        /// if the <paramref name="alreadyRunningProcess"/> is <c>null</c>.</returns>
        public static async Task<ExternallyStartedOverlay> GetExternallyStartedOverlay(Process alreadyRunningProcess)
        {
            if (alreadyRunningProcess == null) return null;

            GlosSITargetSettings currentSettings;

            try
            {
                currentSettings = await GlosSITargetSettings.ReadCurrent().ConfigureAwait(false);
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                // Should not happen since a process was found (unless the process happened to close inbetween).
                logger.Error(ex, "Failed to read currently running GlosSITarget settings.");
                alreadyRunningProcess.Dispose();
                return null;
            }

            string overlayName = currentSettings.Name;

            if (string.IsNullOrEmpty(overlayName))
            {
                logger.Error("The name of the currently running overlay is missing.");
                alreadyRunningProcess.Dispose();
                return null;
            }

            // Hopefully a temporary solution to focus loss from Steam BPM detecting games as having closed.
            if (StartFromSteamLaunchOptions.LaunchesPlaynite(currentSettings.Launch) &&
                Steam.Mode is SteamBigPictureMode bpmMode)
            {
                logger.Debug("Preventing eventual focus loss by switching to Steam desktop mode.");
                await bpmMode.PreventFocusTheft().ConfigureAwait(false);
            }

            return new ExternallyStartedOverlay(overlayName, alreadyRunningProcess);
        }

        protected override Task BeforeStartedCalled()
        {
            return Task.CompletedTask;
        }

        protected override void OnStartedCalled() { }

        protected override void BeforeClosedCalled() { }

        protected override Task OnClosedCalled(int overlayExitCode)
        {
            return Task.CompletedTask;
        }
    }
}
