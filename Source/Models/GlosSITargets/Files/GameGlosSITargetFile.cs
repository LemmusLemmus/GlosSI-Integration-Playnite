using GlosSIIntegration.Models.GlosSITargets.Types;
using System.IO;

namespace GlosSIIntegration.Models.GlosSITargets.Files
{
    internal class GameGlosSITargetFile : GlosSITargetFile
    {
        private readonly GameGlosSITarget target;

        public GameGlosSITargetFile(GameGlosSITarget target) : base(target)
        {
            this.target = target;
        }

        private string GetPathToGameIcon()
        {
            if (string.IsNullOrEmpty(target.AssociatedGame.Icon)) return null;

            return Path.Combine(GlosSIIntegration.Api.Paths.ConfigurationPath, @"library\files\", target.AssociatedGame.Icon);
        }

        public override bool Create(string iconPath)
        {
            if (!GlosSIIntegration.GameHasIgnoredTag(target.AssociatedGame) &&
                !GlosSIIntegration.GameHasIntegratedTag(target.AssociatedGame) &&
                base.Create(iconPath))
            {
                GlosSIIntegration.AddTagToGame(GlosSIIntegration.LOC_INTEGRATED_TAG, target.AssociatedGame);
                return true;
            }

            return false;
        }

        public override bool Create()
        {
            return Create(GetPathToGameIcon());
        }

        public override bool Remove()
        {
            if (GlosSIIntegration.GameHasIntegratedTag(target.AssociatedGame))
            {
                GlosSIIntegration.RemoveTagFromGame(GlosSIIntegration.LOC_INTEGRATED_TAG, target.AssociatedGame);
                GlosSIIntegration.RemoveTagFromGame(GlosSIIntegration.SRC_INTEGRATED_TAG, target.AssociatedGame);
                return base.Remove();
            }

            return false;
        }
    }
}
