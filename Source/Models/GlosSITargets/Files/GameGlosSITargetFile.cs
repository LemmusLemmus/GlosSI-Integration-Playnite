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

        /// <summary>
        /// Creates a GlosSITarget and Steam shortcut for a game, using the default .json structure.
        /// Already integrated games and games tagged for ignoring are ignored.
        /// </summary>
        /// <param name="iconPath">A path to the icon of the shortcut. The path can be <c>null</c> for no icon.</param>
        /// <returns>true if the GlosSITarget was created; false if creation was ignored.</returns>
        /// <exception cref="FileNotFoundException">If the default target json-file could not be found.</exception>
        /// <exception cref="DirectoryNotFoundException">If the glosSITargetsPath directory could not be found.</exception>
        /// <exception cref="UnsupportedCharacterException"><see cref="VerifyTargetCharacters"/></exception>
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

        /// <summary>
        /// Creates a GlosSITarget and Steam shortcut for a game, using the default .json structure.
        /// Already integrated games and games tagged for ignoring are ignored.
        /// Tries to use the same icon as the Playnite game.
        /// </summary>
        /// <returns>true if the GlosSITarget was created; false if creation was ignored.</returns>
        /// <exception cref="FileNotFoundException">If the default target json-file could not be found.</exception>
        /// <exception cref="DirectoryNotFoundException">If the glosSITargetsPath directory could not be found.</exception>
        /// <exception cref="GlosSITargetFile.UnsupportedCharacterException">
        /// <see cref="GlosSITargetFile.VerifyTargetCharacters"/></exception>
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
