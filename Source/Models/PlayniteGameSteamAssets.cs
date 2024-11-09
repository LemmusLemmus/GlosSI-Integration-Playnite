using GlosSIIntegration.Models.GlosSITargets.Shortcuts;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;

namespace GlosSIIntegration.Models
{
    internal class PlayniteGameSteamAssets : SteamGameAssets // TODO: Composition instead?
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly Game game;
        public PlayniteGameSteamAssets(Game playniteGame, SteamShortcut steamShortcut) : base(steamShortcut)
        {
            game = playniteGame;
        }

        /// <summary>
        /// Gets the Playnite logo path, from darklinkpower's Extra Metadata Loader extension.
        /// Note that the path to the file returned by this method might not actually exist.
        /// </summary>
        /// <returns>The path to where a Playnite logo file could be found.</returns>
        private string GetPlayniteLogoPath()
        {
            return Path.Combine(GlosSIIntegration.Instance.PlayniteApi.Paths.ConfigurationPath,
                "ExtraMetadata",
                "games",
                game.Id.ToString(),
                "Logo.png"); // Always a .png file.
        }

        private string GetPlayniteCoverPath()
        {
            return (game.CoverImage == null) ? null :
                API.Instance.Database.GetFullFilePath(game.CoverImage);
        }

        private string GetPlayniteBackgroundPath()
        {
            return (game.BackgroundImage == null) ? null :
                API.Instance.Database.GetFullFilePath(game.BackgroundImage);
        }

        // Note: Since Playnite does not have a concept of
        // separate horizontal and vertical cover images (grids),
        // only one can be set when calling this method.
        // Missing images would have to be fetched from elsewhere.
        public void SetFromPlayniteAssets(bool overwrite = false)
        {
            TrySetAsset(GetPlayniteCoverPath(), overwrite, SetGrid);
            TrySetAsset(GetPlayniteBackgroundPath(), overwrite, SetHero);
            TrySetAsset(GetPlayniteLogoPath(), overwrite, SetLogo);
        }

        private void TrySetAsset(string filePath, bool overwrite, Action<string, bool> setImageAction)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return;

            string fileExtension = Path.GetExtension(filePath);
            if (Asset.HasValidFileExtension(fileExtension))
            {
                setImageAction(filePath, overwrite);
            }
            else
            {
                logger.Warn($"Could not add shortcut image from Playnite image: " +
                    $"Steam does not support the file extension of the file " +
                    $"\"{Path.GetFileName(filePath)}\".");
            }
        }

        /// <summary>
        /// Reads the size of an image.
        /// </summary>
        /// <param name="filePath">The path to the image.</param>
        /// <returns>The size in pixels.</returns>
        /// <exception cref="NotSupportedException">If reading the size of the file 
        /// is not supported.</exception>
        private static Size GetImageSize(string filePath)
        {
            using (FileStream fileStream = File.OpenRead(filePath))
            {
                BitmapFrame bitmapFrame = BitmapFrame.Create(fileStream,
                    BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                return new Size(bitmapFrame.PixelWidth, bitmapFrame.PixelHeight);
            }
        }

        private void SetVerticalGrid(string filePath, bool overwrite)
        {
            if (overwrite || VerticalGrid.CurrentPath == null)
            {
                VerticalGrid.CurrentPath = filePath;
            }
        }

        private void SetHorizontalGrid(string filePath, bool overwrite)
        {
            if (overwrite || HorizontalGrid.CurrentPath == null)
            {
                HorizontalGrid.CurrentPath = filePath;
            }
        }

        private void SetHero(string filePath, bool overwrite)
        {
            if (overwrite || Hero.CurrentPath == null)
            {
                Hero.CurrentPath = filePath;
            }
        }

        private void SetLogo(string filePath, bool overwrite)
        {
            if (overwrite || Logo.CurrentPath == null)
            {
                Logo.CurrentPath = filePath;
            }
        }


        // TODO: Compare aspect ratios and overwrite the file if the
        // new image has a more conformant aspect ratio?
        /// <summary>
        /// Tries to set the vertical or horizontal grid image, depending on if the image is more tall or wide.
        /// Does nothing if the image is perfectly square.
        /// </summary>
        /// <param name="filePath">The path to the image file.</param>
        /// <param name="overwrite">true if any existing asset should be overwritten; false otherwise.</param>
        private void SetGrid(string filePath, bool overwrite)
        {
            Size imageSize;

            try
            {
                imageSize = GetImageSize(filePath);
            }
            catch (NotSupportedException ex)
            {
                logger.Warn(ex, "Could not read the size of the image: not adding Steam shortcut image.");
                return;
            }

            if (imageSize.Height > imageSize.Width)
            {
                SetVerticalGrid(filePath, overwrite);
            }
            else if (imageSize.Width > imageSize.Height)
            {
                SetHorizontalGrid(filePath, overwrite);
            }
            else
            {
                // Square, will not look good on both grids.
                logger.Info($"The grid image of the Playnite game \"{game.Name}\" is square " +
                    $"and will not be used for the Steam shortcut.");
            }
        }
    }
}
