using Playnite.SDK;
using System;
using System.IO;

namespace GlosSIIntegration.Models
{
    class GlosSISteamShortcut : SteamShortcut
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
        /// <exception cref="InvalidOperationException">If the GlosSITarget process is not runnable 
        /// (i.e. <see cref="IsRunnable()"/> returns false) or if starting the process failed.</exception>
        /// <seealso cref="SteamShortcut.Run()"/>
        public override void Run()
        {
            LogManager.GetLogger().Debug($"Running GlosSITarget for {Name}...");
            VerifyRunnable();
            base.Run();
        }

        /// <summary>
        /// Verifies that the GlosSITarget shortcut is runnable. If not, throws an exception and displays an error message.
        /// </summary>
        /// <exception cref="InvalidOperationException">If the shortcut is not runnable (i.e. does not have a .json file).</exception>
        public void VerifyRunnable()
        {
            if (!GlosSITargetFile.HasJsonFile(Name))
            {
                string msg = ResourceProvider.GetString("LOC_GI_GlosSITargetNotFoundOnGameStartError");
                GlosSIIntegration.NotifyError(msg, "GlosSIIntegration-SteamGame-RunGlosSITarget");
                throw new InvalidOperationException(msg);
            }
        }
    }
}
