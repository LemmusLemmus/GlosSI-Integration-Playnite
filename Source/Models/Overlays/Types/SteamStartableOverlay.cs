using GlosSIIntegration.Models.GlosSITargets.Files;
using GlosSIIntegration.Models.GlosSITargets.Types;
using Playnite.SDK;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models.Overlays.Types
{
    /// <summary>
    /// Represents an overlay that can be started from Steam such that something is launched, 
    /// that should otherwise not be launched when the overlay is started from this extension.
    /// </summary>
    internal abstract class SteamStartableOverlay : Overlay
    {
        protected SteamStartableOverlay(GlosSITarget target) : base(target) { }

        protected override async Task BeforeStartedCalled()
        {
            await SetDoLaunchGame(false).ConfigureAwait(false);
        }

        protected override async Task OnClosedCalled(int overlayExitCode)
        {
            await SetDoLaunchGame(true).ConfigureAwait(false);
        }

        private async Task SetDoLaunchGame(bool doLaunch)
        {
            LogManager.GetLogger().Trace($"SetDoLaunchGame({doLaunch})");
            GlosSITargetSettings settings = await GlosSITargetSettings.ReadFromAsync(Target.File.FullPath).ConfigureAwait(false);

            if (settings.Launch == null)
            {
                logger.Error("Launch options of found already running overlay is null!");
                return;
            }

            // Check if there is anything to launch.
            if (string.IsNullOrEmpty(settings.Launch.LaunchPath))
            {
                logger.Trace("LaunchPath of found already running overlay is missing: not updating launch property.");
                return;
            }

            if (settings.Launch.Launch == doLaunch)
            {
                logger.Trace("Launch.Launch is already correctly set.");
                return;
            }

            settings.Launch.Launch = doLaunch;

            await settings.WriteToAsync().ConfigureAwait(false);
        }
    }
}
