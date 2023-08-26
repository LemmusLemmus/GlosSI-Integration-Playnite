using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models.SteamLauncher
{
    internal class SteamDesktopMode : ISteamMode
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        public WinWindow MainWindow { get; }

        internal SteamDesktopMode(WinWindow mainWindow)
        {
            MainWindow = mainWindow;
        }

        /// <summary>
        /// Checks if Steam is configured to occasionally notify the user about available games (marketing messages).
        /// </summary>
        /// <exception cref="FileFormatException">If the value of the option could not be found.</exception>
        /// <returns>true if Steam is configured to notify the user; false if disabled.</returns>
        private static async Task<bool> IsSteamNotifyAvailableGamesEnabled()
        {
            string configFilePath = Path.Combine(Steam.Path, "userdata", Steam.ActiveUser.ToString(), @"config\localconfig.vdf");
            Regex rgx = new Regex(@"(?<=^\s*""NotifyAvailableGames""\s*"")[01](?="")");

            using (StreamReader reader = File.OpenText(configFilePath))
            {
                string line;
                while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    Match match = rgx.Match(line);
                    if (match.Success)
                    {
                        return match.Value != "0";
                    }
                }
            }

            throw new FileFormatException("Could not find whether Steam notifies available games.");
        }

        /// <summary>
        /// Waits for the Steam desktop window to start.
        /// </summary>
        /// <exception cref="TimeoutException">If Steam Desktop mode did not start in time.</exception>
        /// <returns>The Steam desktop window.</returns>
        private static async Task<WinWindow> WaitForSteamDesktopWindowToStart()
        {
            int msTimeout = 10000;
            const int msPollingInterval = 20;
            WinWindow desktopWindow;

            do
            {
                if ((desktopWindow = Steam.FindDesktopModeWindow()) != null)
                {
                    return desktopWindow;
                }
                await Task.Delay(msPollingInterval).ConfigureAwait(false);
            } while ((msTimeout -= msPollingInterval) >= 0);

            throw new TimeoutException("Steam desktop mode did not start in time.");
        }

        /// <summary>
        /// Steam windows we do not want to see.
        /// Add a window to disable input and transition animations for the window 
        /// as well as minimize it.
        /// Dispose the object to undo all changes.
        /// </summary>
        private class UnwantedSteamWindows : IDisposable
        {
            private readonly List<WinWindow> steamWindows;

            public UnwantedSteamWindows()
            {
                steamWindows = new List<WinWindow>();
            }

            public void Add(WinWindow window)
            {
                if (!steamWindows.Contains(window))
                {
                    steamWindows.Add(window);
                    window.DisableTransitionsAnimations();
                    // Not really necessary, but could protect against misclicks.
                    window.DisableInput();
                }
                window.Minimize();
            }

            public void Dispose()
            {
                foreach (WinWindow window in steamWindows)
                {
                    window.EnableInput();
                    window.EnableTransitionsAnimations();
                }
            }
        }

        // TODO: Increase delays when polling where reasonable.
        /// <summary>
        /// Attempts to return Steam to desktop mode (from Big Picture mode) without showing Steam desktop mode.
        /// Note: This method focuses the currently focused window afterwards.
        /// Try to ensure that the user is not busy interacting with windows when this method is called.
        /// This method is far from perfect, as it heavily relies on polling and timings. 
        /// But there is not much of a choice (aside from hooking method calls).
        /// </summary>
        /// <param name="bpm">The currently active Steam Big Picture mode.</param>
        /// <exception cref="TimeoutException">If the Steam desktop window did not appear in time.</exception>
        public static async Task StealthilyReturnSteamToDesktopMode(SteamBigPictureMode bpm)
        {
            // When we exit Steam Big Picture mode, Steam will open the desktop mode window.
            // This is done presumably using SDL's RaiseWindow method, which will forcibly take focus using AttachThreadInput().
            // Focus is forcibly taken twice for the main window.
            // But Steam may also show marketing messages (notably the "Special Offers" window).
            // And if this window is shown (~3 seconds after the second forced focusing), it will also take forcibly take focus.
            // The process to combat this is therefore quite slow and unreliable.
            
            WinWindow thisWindow = WinWindow.GetFocusedWindow();
            const int msPollingInterval = 20;
            int msTimeout = 3000;
            int timesForegroundWindow = 0;
            bool steamMightShowMarketingMessage;

            bpm.Exit();

            try
            {
                steamMightShowMarketingMessage = await IsSteamNotifyAvailableGamesEnabled().ConfigureAwait(false);
            }
            catch (FileFormatException)
            {
                steamMightShowMarketingMessage = true;
            }
            
            WinWindow steamDesktopWindow = await WaitForSteamDesktopWindowToStart().ConfigureAwait(false);

            using (UnwantedSteamWindows unwantedSteamWindows = new UnwantedSteamWindows())
            {
                unwantedSteamWindows.Add(steamDesktopWindow);
                do
                {
                    WinWindow focusedWindow = WinWindow.GetFocusedWindow();

                    if (!Equals(thisWindow, focusedWindow))
                    {
                        if (focusedWindow == null)
                        {
                            // Should not really happen.
                            logger.Warn("HWND 0 was focused when exiting Steam Big Picture mode.");
                        }
                        else if (focusedWindow.Equals(steamDesktopWindow))
                        {
                            steamDesktopWindow.Minimize();
                            timesForegroundWindow++;
                            msTimeout = 3000; // Reset timeout: the next focus theft should come soon.
                        }
                        else if (focusedWindow.GetProcessId() == steamDesktopWindow.GetProcessId())
                        {
                            // The focused window is a Steam window that is not the main desktop window: 
                            // it is most likely a marketing message.
                            unwantedSteamWindows.Add(focusedWindow);
                            focusedWindow.Minimize();
                            timesForegroundWindow++;
                            msTimeout = 3000; // Reset timeout.
                        }
                        else
                        {
                            // Some other non-Steam window was focused. Let's stop our shenanigans.
                            logger.Warn("Non-Steam window was focused when exiting Steam Big Picture mode.");
                            return;
                        }

                        thisWindow.Focus(); // Return focus. Should succeed provided that the currently focused window is 0.

                        if (timesForegroundWindow == 2 && !steamMightShowMarketingMessage)
                        {
                            // No annoying notification should appear! We are done.
                            logger.Trace("Steam was focused two times and marketing messages are disabled.");
                            return;
                        }
                        else if (timesForegroundWindow == 3)
                        {
                            // We are most probably done now.
                            logger.Trace("Steam was focused three times.");
                            return;
                        }
                    }

                    await Task.Delay(msPollingInterval).ConfigureAwait(false);
                } while ((msTimeout -= msPollingInterval) >= 0);
            }

            if (msTimeout < 0)
            {
                if (timesForegroundWindow == 0)
                {
                    throw new TimeoutException("Waiting for Steam desktop mode to steal focus timed out!");
                }

                // We are forced to rely on a timeout.
                logger.Trace("Waiting for Steam focus theft timed out.");
                if (timesForegroundWindow == 1)
                {
                    logger.Warn("Missed the second Steam desktop mode focus theft.");
                }
            }
        }
    }
}