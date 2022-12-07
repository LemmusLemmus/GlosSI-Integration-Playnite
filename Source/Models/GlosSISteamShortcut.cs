using Playnite.SDK;
using System;
using System.IO;

namespace GlosSIIntegration
{
    class GlosSISteamShortcut : SteamGame
    {
        /// <summary>
        /// Instantiates a <c>GlosSISteamShortcut</c> object belonging to a GlosSITarget shortcut.
        /// </summary>
        /// <param name="name">The name of the shortcut.</param>
        /// <exception cref="InvalidOperationException">
        /// If the GlosSIPath setting is <c>null</c>.</exception>
        public GlosSISteamShortcut(string name) : base(name, GetGlosSITargetPath()) { }

        /// <summary>
        /// Gets the path to the GlosSITarget executable.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// If the GlosSIPath setting is <c>null</c>.</exception>
        /// <returns>The path to GlosSITarget.</returns>
        private static string GetGlosSITargetPath()
        {
            string glosSIFolderPath = GlosSIIntegration.GetSettings().GlosSIPath;

            if (glosSIFolderPath == null)
            {
                throw new InvalidOperationException("The path to GlosSI has not been set.");
            }

            return Path.Combine(glosSIFolderPath, "GlosSITarget.exe").Replace('\\', '/');
        }

        /// <summary>
        /// Runs the GlosSITarget associated with this object via Steam.
        /// If the GlosSI configuration file could not be found, 
        /// the method only displays an error notification.
        /// </summary>
        /// <returns>true if the process was started; false otherwise.</returns>
        /// <seealso cref="SteamGame.Run()"/>
        public override bool Run()
        {
            LogManager.GetLogger().Trace($"Running GlosSITarget for {GetName()}...");
            if (!GlosSITargetFile.HasJsonFile(GetName()))
            {
                GlosSIIntegration.NotifyError(
                    ResourceProvider.GetString("LOC_GI_GlosSITargetNotFoundOnGameStartError"),
                    "GlosSIIntegration-SteamGame-RunGlosSITarget");
                return false;
            }

            return base.Run();
        }
    }
}
