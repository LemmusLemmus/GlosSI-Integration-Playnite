using GlosSIIntegration.Models.GlosSITargets.Shortcuts;
using Playnite.SDK;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;

namespace GlosSIIntegration.Models
{
    internal class SteamGameAssets
    {
        public Asset VerticalGrid { get; }
        public Asset HorizontalGrid { get; }
        public Asset Logo { get; }
        public Asset Hero { get; }

        public SteamGameAssets(SteamShortcut steamShortcut)
        {
            uint gameId = (uint)(steamShortcut.Id >> 32);
            VerticalGrid = new VerticalGridAsset(gameId);
            HorizontalGrid = new HorizontalGridAsset(gameId);
            Logo = new LogoAsset(gameId);
            Hero = new HeroAsset(gameId);
        }

        public virtual void DeleteAllAssets()
        {
            VerticalGrid.CurrentPath = null;
            HorizontalGrid.CurrentPath = null;
            Hero.CurrentPath = null;
            Logo.CurrentPath = null;
        }

        private class VerticalGridAsset : Asset
        {
            public VerticalGridAsset(uint gameId) : base(gameId, "p") { }
        }

        private class HorizontalGridAsset : Asset
        {
            public HorizontalGridAsset(uint gameId) : base(gameId, "") { }
        }

        private class LogoAsset : Asset
        {
            public LogoAsset(uint gameId) : base(gameId, "_logo") { }
        }

        private class HeroAsset : Asset
        {
            public HeroAsset(uint gameId) : base(gameId, "_hero") { }
        }

        public abstract class Asset
        {
            private static readonly ILogger logger = LogManager.GetLogger();
            // TODO: Support more common file extensions by converting those files.
            // Steam seems to also support .bmp images if renamed to one of the SupportedFileExtensions.
            /// <summary>
            /// Image file extensions supported by Steam.
            /// </summary>
            private static readonly string[] SupportedFileExtensions = { ".jpg", ".jpeg", ".png" };
            private readonly string fileNameWithoutExtension;
            private readonly string gridDirectoryPath;
            private string currentImagePath;

            /// <summary>
            /// The current path to the Steam asset. 
            /// Set it to update the image: the file in the provided path will be copied 
            /// (or, if possible, hard linked).
            /// Set it to <c>null</c> to simply delete the image.
            /// </summary>
            /// <exception cref="ArgumentOutOfRangeException">If set to a path with 
            /// an unsupported file extension.</exception>
            public string CurrentPath
            {
                get => currentImagePath;
                set
                {
                    if (currentImagePath != null)
                    {
                        DeleteCurrentImage();
                    }

                    if (value != null)
                    {
                        SetCurrentImage(value);
                    }
                }
            }

            /// <summary>
            /// Refreshes the value of <see cref="CurrentPath"/>, 
            /// in case it was changed from outside this object.
            /// <para>
            /// If this object is kept in memory for longer duration, 
            /// consider calling this method before accessing <see cref="CurrentPath"/>.
            /// </para>
            /// </summary>
            public void RefreshCurrentImagePath()
            {
                currentImagePath = GetExistingAssetPath();
            }

            protected Asset(uint gameId, string suffix)
            {
                fileNameWithoutExtension = gameId.ToString() + suffix;
                gridDirectoryPath = GetGridDirectoryPath();
                RefreshCurrentImagePath();
            }

            private void DeleteCurrentImage()
            {
                File.Delete(currentImagePath);
                logger.Info($"Deleted file \"{currentImagePath}\".");
                currentImagePath = null;
            }

            private void SetCurrentImage(string filePath)
            {
                string fileExtension = Path.GetExtension(filePath);
                if (!HasValidFileExtension(fileExtension))
                {
                    throw new ArgumentOutOfRangeException($"The file extension of the image ({fileExtension}) " +
                        $"is not supported by Steam.");
                }

                string toPath = Path.Combine(gridDirectoryPath, fileNameWithoutExtension + fileExtension);

                try
                {
                    HardLink.Create(toPath, filePath);
                }
                catch (Win32Exception hardLinkEx)
                {
                    logger.Warn($"Could not create a hard link: \"{hardLinkEx.Message}\", copying file instead.");
                    try
                    {
                        File.Copy(filePath, toPath, true);
                    }
                    catch (Exception ex) // TODO: Too general?
                    {
                        logger.Error(ex, $"Failed to copy image file, no Steam shortcut image was added.");
                        return;
                    }
                }

                currentImagePath = toPath;
            }

            private string GetExistingAssetPath()
            {
                // Assuming that there is not, for some reason,
                // multiple files for the same asset with different file extensions.
                return Directory.GetFiles(gridDirectoryPath).FirstOrDefault(
                    filePath => Path.GetFileName(filePath)
                    .StartsWith(fileNameWithoutExtension + ".", StringComparison.OrdinalIgnoreCase) &&
                    HasValidFileExtension(Path.GetExtension(filePath)));
            }

            /// <summary>
            /// Checks if the file extension is supported by Steam.
            /// Supported types: .png (including animated ones), .jpg and .jpeg.
            /// </summary>
            /// <param name="fileExtension">The file extension (including the initial dot).</param>
            /// <returns>true if valid; false otherwise.</returns>
            public static bool HasValidFileExtension(string fileExtension)
            {
                return SupportedFileExtensions.Any(
                    ext => ext.Equals(fileExtension, StringComparison.OrdinalIgnoreCase));
            }

            private static string GetGridDirectoryPath()
            {
                string dirPath = Path.Combine(
                    Path.GetDirectoryName(GlosSIIntegration.GetSettings().SteamShortcutsPath), "grid");
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }

                return dirPath;
            }
        }
    }
}
