using GlosSIIntegration.Models.GlosSITargets.Files;
using GlosSIIntegration.Models.GlosSITargets.Shortcuts;

namespace GlosSIIntegration.Models.GlosSITargets.Types
{
    internal abstract class GlosSITarget : GlosSISteamShortcut // TODO: Composition instead of inheritance! Makes more sense as well.
    {
        public GlosSITargetFile File { get; }

        protected GlosSITarget(string name) : base(name)
        {
            File = GetGlosSITargetFile();
        }

        protected virtual GlosSITargetFile GetGlosSITargetFile()
        {
            return new GlosSITargetFile(this);
        }

        protected internal abstract GlosSITargetSettings.LaunchOptions GetPreferredLaunchOptions();
    }
}
