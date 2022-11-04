using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace GlosSIIntegration
{
    /// <summary>
    /// Represents the current state of the GlosSITarget overlay.
    /// </summary>
    static class OverlayState
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        /// <summary>
        /// Set when Playnite has finished starting.
        /// </summary>
        private static readonly ManualResetEvent playniteStarted = new ManualResetEvent(false);
        /// <summary>
        /// Whether the user is currently in-game (disregarding ignored games).
        /// </summary>
        private static volatile bool isInGame = false;
        /// <summary>
        /// The overlay that should currently run (irregardless of whether the integration is enabled).
        /// </summary>
        private static SteamGame relevantOverlay = null;
        /// <summary>
        /// The pid of the game that is currently running, or -1 if no game is currently running. May be invalid.
        /// </summary>
        private static int runningGamePid = -1;
        /// <summary>
        /// The thread that runs when a game is starting. Handles closing and opening of a new GlosSITarget.
        /// </summary>
        private static Thread onGameStartingThread = null;

        /// <summary>
        /// Initializes the overlay.
        /// </summary>
        public static void Initialize()
        {
            ReturnOverlayToPlaynite();
        }

        private static void ReturnOverlayToPlaynite()
        {
            isInGame = false;
            runningGamePid = -1;

            if (IsIntegrationEnabled() && IsFullscreenMode() && GlosSIIntegration.GetSettings().UsePlayniteOverlay)
            {
                if (ReplaceRelevantOverlay(new SteamGame(GlosSIIntegration.GetSettings().PlayniteOverlayName)))
                {
                    RunPlayniteOverlay(relevantOverlay);
                }
            }
            else if (relevantOverlay != null)
            {
                relevantOverlay = null;
                if (IsIntegrationEnabled()) CloseGlosSITargets();
            }
        }

        /// <summary>
        /// Updates the overlay for when a game is starting.
        /// <see cref="GameStarted(int)"/> must be the next method that is called.
        /// </summary>
        /// <param name="game">Game that is starting.</param>
        public static void GameStarting(Game game)
        {
            if (GlosSIIntegration.GameHasIgnoredTag(game)) return;

            isInGame = true;

            if (onGameStartingThread != null)
            {
                logger.Warn("onGameStartingThread is already in use!");
            }
            onGameStartingThread = new Thread(() => PrepareOverlayForGameStart(game));
            onGameStartingThread.Start();
        }

        /// <summary>
        /// Updates the overlay for when a game has started.
        /// </summary>
        /// <param name="startedProcessId">The PID of the started game.</param>
        public static void GameStarted(int startedProcessId)
        {
            if (onGameStartingThread == null) return;

            runningGamePid = startedProcessId;

            onGameStartingThread.Join();
            onGameStartingThread = null;

            if (relevantOverlay != null && IsIntegrationEnabled() && GlosSIIntegration.GetSettings().CloseGameWhenOverlayIsClosed)
            {
                KillGameWhenGlosSICloses(runningGamePid);
            }
        }

        /// <summary>
        /// Updates the overlay for when a game has stopped.
        /// </summary>
        /// <param name="game"></param>
        public static void GameStopped(Game game)
        {
            if (GlosSIIntegration.GameHasIgnoredTag(game)) return;

            ReturnOverlayToPlaynite();

            logger.Trace($"Game stopped: relevant overlay is: {relevantOverlay?.ToString() ?? "null"}.");
        }

        /// <summary>
        /// Informs the overlay that Playnite is up and running.
        /// This method only has to be called once.
        /// </summary>
        public static void PlayniteStarted()
        {
            playniteStarted.Set();
        }

        /// <summary>
        /// Closes the overlay (if the integration is enabled).
        /// </summary>
        public static void Close()
        {
            if (IsIntegrationEnabled()) CloseGlosSITargets();
        }

        public static void IntegrationToggled()
        {
            CloseGlosSITargets();

            // If the user is currently in-game, launch the game specific overlay.
            if (IsIntegrationEnabled() && isInGame && relevantOverlay != null)
            {
                logger.Trace("Steam Overlay launched whilst in-game.");
                if (!relevantOverlay.RunGlosSITarget()) return;
                if (GlosSIIntegration.GetSettings().CloseGameWhenOverlayIsClosed)
                {
                    KillGameWhenGlosSICloses(runningGamePid);
                }
            }
        }

        private static bool IsIntegrationEnabled()
        {
            return GlosSIIntegration.Instance.IntegrationEnabled;
        }

        private static bool IsFullscreenMode()
        {
            return GlosSIIntegration.Api.ApplicationInfo.Mode == ApplicationMode.Fullscreen;
        }

        private static void PrepareOverlayForGameStart(Game game)
        {
            if (ReplaceRelevantOverlay(GetGameOverlay(game)) && IsIntegrationEnabled() && relevantOverlay != null)
            {
                relevantOverlay.RunGlosSITarget();
            }

            logger.Trace($"Game starting: relevant overlay is: {relevantOverlay?.ToString() ?? "null"}.");
        }

        /// <summary>
        /// Kills a game when GlosSI force closes.
        /// </summary>
        /// <param name="gamePid">The game to (potentially) kill.</param>
        private static void KillGameWhenGlosSICloses(int gamePid)
        {
            try
            {
                Process glosSITarget = WaitForGlosSITargetToStart();
                Process game = Process.GetProcessById(gamePid);
                glosSITarget.EnableRaisingEvents = true;
                glosSITarget.Exited += (sender, e) => GlosSITargetExited(sender, e, game);
            }
            catch (ArgumentException e)
            {
                logger.Error(e, $"Waiting for GlosSI to close failed: the game with pid {gamePid} is not running.");
                return;
            }
            catch (TimeoutException e)
            {
                logger.Error($"Waiting for GlosSI to close failed: {e.Message}");
                return;
            }
        }

        /// <summary>
        /// Delegate for the <c>glosSITarget.Exited</c> event.
        /// Kills a game if the game is still running and if GlosSITarget was killed.
        /// </summary>
        /// <param name="sender">The GlosSITarget process.</param>
        /// <param name="_">Ignored.</param>
        /// <param name="game">The game to attempt to kill.</param>
        private static void GlosSITargetExited(object sender, EventArgs _, Process game)
        {
            Process glosSITarget = sender as Process;

            if (!game.HasExited)
            {
                // Check if GlosSI was killed or not, i.e. if it was closed via the Steam overlay.
                // Unless something went wrong, ExitCode 1 should mean that the process was killed.
                if (glosSITarget.ExitCode == 1)
                {
                    logger.Trace("GlosSI killed, killing game in retaliation...");
                    try
                    {
                        game.Kill(); // TODO: Might want to close the game more gracefully? At least as an alternative?
                    }
                    catch (Exception ex)
                    {
                        logger.Warn(ex, "Killing game failed:");
                    }
                }
                else if (glosSITarget.ExitCode == 0)
                {
                    logger.Trace("GlosSI closed normally, not killing game.");
                }
                else
                {
                    logger.Warn($"GlosSI closed with exit code {glosSITarget.ExitCode}, not killing game.");
                }
            }

            glosSITarget.Dispose();
            game.Dispose();
        }

        /// <summary>
        /// Waits for GlosSITarget to start and returns the found process.
        /// </summary>
        /// <returns>The found GlosSITarget process.</returns>
        /// <exception cref="TimeoutException">If GlosSITarget did not start after 10 seconds.</exception>
        private static Process WaitForGlosSITargetToStart()
        {
            int sleptTime = 0;
            Process[] p;

            while ((p = Process.GetProcessesByName("GlosSITarget")).Length == 0)
            {
                Thread.Sleep(333);
                if ((sleptTime += 333) > 10000)
                {
                    throw new TimeoutException("GlosSITarget did not start in time.");
                }
            }

            if (p.Length > 1)
            {
                logger.Warn($"Multiple ({p.Length}) GlosSITargets were found.");
                for (int i = 1; i < p.Length; i++) p[i].Dispose();
            }

            return p[0];
        }

        /// <summary>
        /// Gets the overlay associated with a game.
        /// </summary>
        /// <param name="game">The game to get the overlay for.</param>
        /// <returns>If found, the overlay; <c>null</c> otherwise.</returns>
        private static SteamGame GetGameOverlay(Game game)
        {
            string overlayName;

            if (GlosSIIntegration.GameHasIntegratedTag(game))
            {
                overlayName = game.Name;
            }
            else if (GlosSIIntegration.GetSettings().UseDefaultOverlay && !GlosSIIntegration.IsSteamGame(game))
            {
                overlayName = GlosSIIntegration.GetSettings().DefaultOverlayName;
            }
            else
            {
                return null;
            }

            return new SteamGame(overlayName);
        }

        /// <summary>
        /// Updates the <c>relevantOverlay</c> and closes GlosSITarget if the overlay was changed.
        /// </summary>
        /// <param name="overlay">The new relevant overlay.</param>
        /// <returns>True if the overlay was changed; false otherwise.</returns>
        private static bool ReplaceRelevantOverlay(SteamGame overlay)
        {
            // Check if the overlay to be started is already running.
            if (relevantOverlay != null && relevantOverlay.Equals(overlay) && IsGlosSITargetRunning())
            {
                logger.Trace($"Overlay \"{relevantOverlay?.ToString() ?? "null"}\" was left running.");
                return false;
            }
            relevantOverlay = overlay;
            if (IsIntegrationEnabled()) CloseGlosSITargets();
            return true;
        }

        /// <summary>
        /// Checks if GlosSITarget is currently running.
        /// </summary>
        /// <returns>true if GlosSITarget is running; false otherwise.</returns>
        private static bool IsGlosSITargetRunning()
        {
            Process[] glosSITargets = Process.GetProcessesByName("GlosSITarget");
            if (glosSITargets.Length != 0)
            {
                foreach (Process p in glosSITargets)
                {
                    p.Dispose();
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Starts the Playnite GlosSI/Steam overlay.
        /// </summary>
        /// <param name="playniteOverlay">The Playnite overlay to start.</param>
        private static void RunPlayniteOverlay(SteamGame playniteOverlay)
        {
            if (!GlosSIIntegration.GetSettings().UsePlayniteOverlay)
            {
                logger.Warn("Attempted to run the Playnite overlay despite it being disabled.");
                return;
            }

            // It is up to personal preference whether to start this in a new thread.
            // The difference is whether the user can see their library or a "loading" screen while the Steam overlay is starting.
            new Thread(() =>
            {
                if (!playniteOverlay.RunGlosSITarget())
                {
                    GlosSIIntegration.DisplayError(ResourceProvider.GetString("LOC_GI_GlosSITargetNotFoundOnGameStartError"));
                    return;
                }
                playniteStarted.WaitOne();
                try
                {
                    ReturnStolenFocus();
                }
                catch (TimeoutException e)
                {
                    logger.Trace($"Failed to return focus to Playnite: {e.Message}");
                }
            })
            { IsBackground = true }.Start();
        }

        /// <summary>
        /// Assuming that GlosSITarget has or will soon steal the focus from this application, 
        /// attempts to return the focus to this application.
        /// Only returns focus if the user is not currently in-game.
        /// </summary>
        /// <exception cref="TimeoutException">If GlosSITarget took too long to start.</exception>
        private static void ReturnStolenFocus()
        {
            // Wait for GlosSITarget to start, if it has not already.
            using (Process glosSITarget = WaitForGlosSITargetToStart())
            {
                // For some reason focus is sometimes stolen twice.
                // An alternative solution is to simply use a delay of say 250 ms before calling FocusSelf().
                int attempts = 1;
                try
                {
                    for (; attempts <= 3; attempts++) // Third time's a charm.
                    {
                        ReturnStolenFocus(glosSITarget);
                    }
                    logger.Trace("All attempts to return focus to Playnite passed.");
                }
                catch (TimeoutException) { }
                logger.Trace($"{attempts} attempts were made to return focus to Playnite.");
            }
        }

        /// <summary>
        /// Attempts to return focus stolen by a process to Playnite once, unless the user is currently in game.
        /// </summary>
        /// <param name="proc">The process expected to steal focus.</param>
        /// <exception cref="TimeoutException">If an operation took too long.</exception>
        private static void ReturnStolenFocus(Process proc)
        {
            // Wait for GlosSITarget to steal focus, if it has not already.
            WaitForStolenFocus(proc);
            if (isInGame)
            {
                throw new TimeoutException("Game started before process stole focus.");
            }
            FocusSelf();
        }

        /// <summary>
        /// Waits for window focus to be stolen by <paramref name="thief"/>.
        /// </summary>
        /// <param name="thief">The process which will steal focus.</param>
        private static void WaitForStolenFocus(Process thief, int interval = 120, int maxSleepTime = 8000)
        {
            int sleptTime = 0;
            while (GetForegroundWindow() != thief.MainWindowHandle)
            {
                Thread.Sleep(interval);
                if ((sleptTime += interval) > maxSleepTime)
                {
                    throw new TimeoutException("Process did not steal focus in time.");
                }
            }
        }

        /// <summary>
        /// Sets the window focus to this process. 
        /// Unfortunately, a virtual-key press is necessary to ensure that Windows allows the focus to be changed.
        /// </summary>
        /// <param name="unusedKey">The virtual-key code of the key to be pressed, by default the left alt key (VK_LMENU).</param>
        private static void FocusSelf(byte unusedKey = 0xA4)
        {
            // Fool Windows to permit the usage of SetForegroundWindow().
            keybd_event(unusedKey, 0x45, EXTENDEDKEY | 0, 0);
            keybd_event(unusedKey, 0x45, EXTENDEDKEY | KEYUP, 0);

            using (Process currentProcess = Process.GetCurrentProcess())
            {
                if (!SetForegroundWindow(currentProcess.MainWindowHandle))
                {
                    logger.Warn("Setting foreground window to Playnite failed.");
                }
            }
        }

        private const uint EXTENDEDKEY = 0x1, KEYUP = 0x2; // KEYEVENTF_EXTENDEDKEY & KEYEVENTF_KEYUP

        #pragma warning disable IDE1006
        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
        #pragma warning restore IDE1006

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        /// <summary>
        /// Closes all currently running GlosSITarget processes.
        /// </summary>
        private static void CloseGlosSITargets()
        {
            logger.Trace("Closing GlosSITargets...");
            try
            {
                Process[] glosSITargets = Process.GetProcessesByName("GlosSITarget");

                // It is assumed that there is no reason for the user to ever want to have multiple GlosSITargets
                // running simultaneously. As such, they are all closed.
                foreach (Process proc in glosSITargets)
                {
                    if (!proc.CloseMainWindow())
                    {
                        // The GlosSITarget overlay is most likely disabled, close it by other means.
                        logger.Trace("Closing GlosSITarget without overlay.");
                        // Since this method gets the process by name,
                        // it will only work reliably if there is only one GlosSITarget running.
                        // There is no reason to bother with supporting that case, as
                        // "Multiple instances of the target calls for trouble...".
                        CloseWindowByCaption("GlosSITarget");
                    }
                }

                foreach (Process proc in glosSITargets)
                {
                    if (!proc.WaitForExit(10000))
                    {
                        GlosSIIntegration.NotifyError(ResourceProvider.GetString("LOC_GI_CloseGlosSITargetTimelyUnexpectedError"),
                            "GlosSIIntegration-FailedToCloseGlosSITarget");
                    }
                    proc.Close();
                }
            }
            catch (InvalidOperationException) { }
            catch (PlatformNotSupportedException e)
            {
                GlosSIIntegration.DisplayError(string.Format(ResourceProvider.GetString("LOC_GI_CloseGlosSITargetUnexpectedError"),
                    e.Message), e);
            }
        }

        /// <summary>
        /// Attempts to close a window by sending the message WM_CLOSE. The method is capable of closing windows without a GUI.
        /// </summary>
        /// <param name="lpWindowName">The name of the window to be closed.</param>
        private static void CloseWindowByCaption(string lpWindowName)
        {
            IntPtr windowPtr = FindWindowByCaption(IntPtr.Zero, lpWindowName);

            if (windowPtr == IntPtr.Zero)
            {
                logger.Warn("No window to close was found.");
                return;
            }

            SendMessage(windowPtr, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        }

        private const uint WM_CLOSE = 0x0010;

        /// <summary>
        /// Finds a window by caption. Note that the first parameter must be IntPtr.Zero.
        /// </summary>
        [DllImport("user32.dll", EntryPoint = "FindWindow")]
        private static extern IntPtr FindWindowByCaption(IntPtr intPtrZero, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    }
}
