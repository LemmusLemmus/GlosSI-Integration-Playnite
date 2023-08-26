using GlosSIIntegration.Models.GlosSITargets.Types;
using Playnite.SDK.Models;

namespace GlosSIIntegration.Models.Overlays.Types
{
    /// <summary>
    /// Represents an used by default for Playnite games 
    /// without a specific overlay.
    /// </summary>
    internal class DefaultGameOverlay : GameOverlay
    {
        public DefaultGameOverlay(Game associatedGame) : base(associatedGame, new DefaultGlosSITarget()) { }
    }
}
