using Playnite.SDK;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models.SteamLauncher
{
    internal class SteamBigPictureMode : ISteamMode
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        public WinWindow MainWindow { get; }

        internal SteamBigPictureMode(WinWindow mainWindow)
        {
            MainWindow = mainWindow;
        }

        /// <summary>
        /// Exits Big Picture mode. Steam desktop mode will eventually start.
        /// </summary>
        public void Exit()
        {
            try
            {
                MainWindow.Close();
            }
            catch (InvalidOperationException ex)
            {
                logger.Warn(ex, "Could not close Steam Big Picture mode.");
            }
        }

        /// <summary>
        /// Opens Steam Big Picture mode. If Big Picture Mode is not already running, starts it.
        /// </summary>
        public static void Open()
        {
            Process.Start("steam://open/bigpicture")?.Dispose();
        }

        /// <summary>
        /// Prevents focus theft from occuring by exiting Big Picture mode.
        /// This must be called a Steam shortcut that should not steal focus is closed.
        /// </summary>
        public async Task PreventFocusTheft()
        {
            // When Steam is in big picture mode, whenever a game is closed Steam will take focus (after roughly 3 seconds).
            // This is annoying and I cannot be bothered to deal with all particularities of when/how Steam Big Picture mode steals focus.
            // As such, we simply return Steam to desktop mode.
            // Steam desktop mode should provide mostly the same experience provided that the user has not disabled 
            // "Use the Big Picture Overlay when using a controller".

            try
            {
                await SteamDesktopMode.StealthilyReturnSteamToDesktopMode(this).ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                logger.Error(ex, "Failed to switch to Steam desktop mode. " +
                    "The extension switches to Steam desktop mode in order to avoid having to deal with " +
                    "Steam Big Picture mode force focusing itself when games are closed.");
            }

            // Proper handling of Steam Big Picture mode taking focus could be added later.
            // For example, Steam BPM does not take focus if there are any currently running games.
            // As such, merely switching overlays is fine.
            // A "dummy" Steam shortcut could also work.
            // Alternatively, the most effective but also most intrusive option would be to
            // hook Steam's RaiseWindow() (SDL) or AttachThreadInput() (Win32) calls.

            // Note that when a new Steam game is launched while in BPM, the current Steam overlay will display the game starting.
            // Hiding GlosSITarget before the overlay is switched solves that problem.

            // It is also worth noting that when Steam is in BPM, Steam will play a sound when a game is started.
        }
    }
}