using GlosSIIntegration.Models.GlosSITargets.Files;
using Playnite.SDK.Models;

namespace GlosSIIntegration.Models.GlosSITargets.Types
{
    /// <summary>
    /// Represents a GlosSITarget used for a specific Playnite game.
    /// </summary>
    internal class GameGlosSITarget : GlosSITarget
    {
        public Game AssociatedGame { get; }

        public GameGlosSITarget(Game game) : base(game.Name)
        {
            AssociatedGame = game;
        }

        protected override GlosSITargetFile GetGlosSITargetFile()
        {
            return new GameGlosSITargetFile(this);
        }

        protected internal override GlosSITargetSettings.LaunchOptions GetPreferredLaunchOptions()
        {
            return GetPreferredLaunchOptions(AssociatedGame);
        }

        private static GlosSITargetSettings.LaunchOptions GetPreferredLaunchOptions(Game game)
        {
            return StartFromSteamLaunchOptions.GetLaunchGameOptions(game);
        }
    }
}