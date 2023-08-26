using GlosSIIntegration.Models.GlosSITargets.Files;
using System;

namespace GlosSIIntegration.Models.GlosSITargets.Types
{
    /// <summary>
    /// An unidentified type of GlosSITarget. It could be unrelated to this extension.
    /// </summary>
    internal class UnidentifiedGlosSITarget : GlosSITarget
    {
        public UnidentifiedGlosSITarget(string name) : base(name) { }

        protected internal override GlosSITargetSettings.LaunchOptions GetPreferredLaunchOptions()
        {
            // Does not really adhere to the Liskov substitution principle,
            // but this overlay should really not be used for creating and verifing GlosSITargetFiles,
            // since its type is unknown.
            throw new NotSupportedException("The preferred launch options of the GlosSITarget is unknown.");
        }
    }
}
