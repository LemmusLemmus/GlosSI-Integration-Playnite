using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;

namespace GlosSIIntegration
{
    public class GlosSIIntegration : GenericPlugin
    {
        public static readonly string LOC_INTEGRATED_TAG = ResourceProvider.GetString("LOC_GI_IntegratedTag"),
            LOC_IGNORED_TAG = ResourceProvider.GetString("LOC_GI_IgnoredTag"),
            SRC_INTEGRATED_TAG = "[GI] Integrated",
            SRC_IGNORED_TAG = "[GI] Ignored";
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly TopPanelItem topPanel;
        private readonly TextBlock topPanelTextBlock;
        /// <summary>
        /// The overlay that should currently run (irregardless of whether the integration is enabled).
        /// </summary>
        private SteamGame relevantOverlay;
        /// <summary>
        /// Whether the user is currently in-game (disregarding ignored games).
        /// </summary>
        private volatile bool isInGame;
        /// <summary>
        /// The pid of the game that is currently running, or -1 if no game is currently running. May be invalid.
        /// </summary>
        private int runningGamePid;
        /// <summary>
        /// The thread that runs when a game is starting. Handles closing and opening of a new GlosSITarget.
        /// </summary>
        private Thread onGameStartingThread;
        /// <summary>
        /// Set when Playnite has finished starting.
        /// </summary>
        private static readonly ManualResetEvent playniteStarted = new ManualResetEvent(false);

        private bool integrationEnabled;
        public bool IntegrationEnabled
        {
            get { return integrationEnabled; }
            set { integrationEnabled = value; UpdateTopPanel(); }
        }

        public override Guid Id { get; } = Guid.Parse("6b0297da-75e5-4330-bb2d-b64bff22c315");
        public static IPlayniteAPI Api { get; private set; }
        public static GlosSIIntegration Instance { get; private set; }
        private GlosSIIntegrationSettingsViewModel SettingsViewModel { get; set; }

        public GlosSIIntegration(IPlayniteAPI api) : base(api)
        {
            Instance = this;
            Api = api;

            SettingsViewModel = new GlosSIIntegrationSettingsViewModel(this, api);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            relevantOverlay = null;
            runningGamePid = -1;
            isInGame = false;
            onGameStartingThread = null;

            topPanelTextBlock = GetInitialTopPanelTextBlock();
            topPanel = GetInitialTopPanel();
            InitializeIntegrationEnabled();
            InitializeTopPanelColor();

            if (Api.ApplicationInfo.Mode == ApplicationMode.Fullscreen &&
                IntegrationEnabled && GetSettings().UsePlayniteOverlay)
            {
                relevantOverlay = new SteamGame(GetSettings().PlayniteOverlayName);
                CloseGlosSITargets(); // Close any GlosSITarget running since before Playnite was started.
                RunPlayniteOverlay(relevantOverlay);
            }
        }

        private void InitializeIntegrationEnabled()
        {
            if (Api.ApplicationInfo.Mode == ApplicationMode.Desktop)
            {
                IntegrationEnabled = GetSettings().DefaultUseIntegrationDesktop;
            }
            else
            {
                IntegrationEnabled = GetSettings().UseIntegrationFullscreen;
            }
        }

        private TextBlock GetInitialTopPanelTextBlock()
        {
            return new TextBlock()
            {
                FontSize = 20,
                FontFamily = ResourceProvider.GetResource("FontIcoFont") as FontFamily,
                Text = char.ConvertFromUtf32(0xed71),
                Foreground = GetGlyphBrush()
            };
        }

        private TopPanelItem GetInitialTopPanel()
        {
            TopPanelItem topPanel = new TopPanelItem
            {
                Activated = () => TopPanelPressed()
            };
            topPanel.Icon = topPanelTextBlock;
            return topPanel;
        }

        public static GlosSIIntegrationSettings GetSettings()
        {
            return Instance.SettingsViewModel.Settings;
        }

        // Method code heavily inspired by https://github.com/darklinkpower's PlayniteUtilites AddTagToGame method
        // from their PlayniteExtensionsCollection repository.
        public static void AddTagToGame(string tagName, Game game)
        {
            Tag tag = Api.Database.Tags.Add(tagName);

            if (game.Tags == null)
            {
                game.TagIds = new List<Guid> { tag.Id };
                Api.Database.Games.Update(game);
            }
            else if (!game.TagIds.Contains(tag.Id))
            {
                game.TagIds.Add(tag.Id);
                Api.Database.Games.Update(game);
            }
        }

        // Method code heavily inspired by https://github.com/darklinkpower's PlayniteUtilites RemoveTagFromGame method
        // from their PlayniteExtensionsCollection repository.
        public static void RemoveTagFromGame(string tagName, Game game)
        {
            if (game.Tags == null) return;

            Tag tag = game.Tags.FirstOrDefault(x => x.Name == tagName);
            if (tag != null)
            {
                game.TagIds.Remove(tag.Id);
                Api.Database.Games.Update(game);
            }
        }

        public static bool GameHasIntegratedTag(Game game)
        {
            return GameHasTag(game, LOC_INTEGRATED_TAG) || GameHasTag(game, SRC_INTEGRATED_TAG);
        }

        public static bool GameHasIgnoredTag(Game game)
        {
            return GameHasTag(game, LOC_IGNORED_TAG) || GameHasTag(game, SRC_IGNORED_TAG);
        }

        private static bool GameHasTag(Game game, string tagName)
        {
            return game.Tags != null && game.Tags.Any(t => t.Name == tagName);
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            if (onGameStartingThread == null) return;

            runningGamePid = args.StartedProcessId;

            onGameStartingThread.Join();
            onGameStartingThread = null;

            if (IntegrationEnabled && GetSettings().CloseGameWhenOverlayIsClosed)
            {
                KillGameWhenGlosSICloses(runningGamePid);
            }
        }

        /// <summary>
        /// Kills a game when GlosSI force closes.
        /// </summary>
        /// <param name="gamePid">The game to (potentially) kill.</param>
        private void KillGameWhenGlosSICloses(int gamePid)
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
        private void GlosSITargetExited(object sender, EventArgs _, Process game)
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
        private SteamGame GetGameOverlay(Game game)
        {
            string overlayName;

            if (GameHasIntegratedTag(game))
            {
                overlayName = game.Name;
            }
            else if (GetSettings().UseDefaultOverlay && !IsSteamGame(game))
            {
                overlayName = GetSettings().DefaultOverlayName;
            }
            else
            {
                return null;
            }

            return new SteamGame(overlayName);
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            if (GameHasIgnoredTag(args.Game)) return;

            isInGame = true;

            if (onGameStartingThread != null)
            {
                logger.Warn("onGameStartingThread is already in use!");
            }
            onGameStartingThread = new Thread(() => PrepareOverlayForGameStart(args.Game));
            onGameStartingThread.Start();
        }

        private void PrepareOverlayForGameStart(Game game)
        {
            if (ReplaceRelevantOverlay(GetGameOverlay(game)) && IntegrationEnabled && relevantOverlay != null)
            {
                relevantOverlay.RunGlosSITarget();
            }

            logger.Trace($"Game starting: relevant overlay is: {relevantOverlay?.ToString() ?? "null"}.");
        }

        /// <summary>
        /// Displays an error message and logs it.
        /// </summary>
        /// <param name="message">The user-readable error message.</param>
        /// <param name="exception">The exception, if one exists.</param>
        /// <seealso cref="NotifyError(string, string)"/>
        public static void DisplayError(string message, Exception exception = null)
        {
            if (exception == null)
            {
                logger.Error(message);
            }
            else
            {
                logger.Error(exception, message);
            }
            Api.Dialogs.ShowErrorMessage(message, ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"));
        }

        /// <summary>
        /// Displays an error as a notification and logs it.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="id">The ID of the notification.</param>
        /// <seealso cref="DisplayError(string, Exception)"/>
        public static void NotifyError(string message, string id)
        {
            logger.Error(message);
            Api.Notifications.Add(id, message, NotificationType.Error);
        }

        /// <summary>
        /// Updates the <c>relevantOverlay</c> and closes GlosSITarget if the overlay was changed.
        /// </summary>
        /// <param name="overlay">The new relevant overlay.</param>
        /// <returns>True if the overlay was changed; false otherwise.</returns>
        private bool ReplaceRelevantOverlay(SteamGame overlay)
        {
            // Check if the overlay to be started is already running.
            if (relevantOverlay != null && relevantOverlay.Equals(overlay) && IsGlosSITargetRunning())
            {
                logger.Trace($"Overlay \"{relevantOverlay?.ToString() ?? "null"}\" was left running.");
                return false;
            }
            relevantOverlay = overlay;
            if (IntegrationEnabled) CloseGlosSITargets();
            return true;
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (GameHasIgnoredTag(args.Game)) return;

            isInGame = false;
            runningGamePid = -1;

            if (IntegrationEnabled &&
                Api.ApplicationInfo.Mode == ApplicationMode.Fullscreen &&
                GetSettings().UsePlayniteOverlay)
            {
                if (ReplaceRelevantOverlay(new SteamGame(GetSettings().PlayniteOverlayName)))
                {
                    RunPlayniteOverlay(relevantOverlay);
                }
            }
            else
            {
                relevantOverlay = null;
                if (IntegrationEnabled) CloseGlosSITargets();
            }

            logger.Trace($"Game stopped: relevant overlay is: {relevantOverlay?.ToString() ?? "null"}.");
        }

        /// <summary>
        /// Checks if GlosSITarget is currently running.
        /// </summary>
        /// <returns>true if GlosSITarget is running; false otherwise.</returns>
        private bool IsGlosSITargetRunning()
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
        private void RunPlayniteOverlay(SteamGame playniteOverlay)
        {
            if (!GetSettings().UsePlayniteOverlay)
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
                    DisplayError(ResourceProvider.GetString("LOC_GI_GlosSITargetNotFoundOnGameStartError"));
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
        private void ReturnStolenFocus()
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
        private void ReturnStolenFocus(Process proc)
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
        private static void WaitForStolenFocus(Process thief, int interval = 80, int maxSleepTime = 4000)
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
        private void CloseGlosSITargets()
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
                        NotifyError(ResourceProvider.GetString("LOC_GI_CloseGlosSITargetTimelyUnexpectedError"),
                            "GlosSIIntegration-FailedToCloseGlosSITarget");
                    }
                    proc.Close();
                }
            }
            catch (InvalidOperationException) { }
            catch (PlatformNotSupportedException e)
            {
                DisplayError(string.Format(ResourceProvider.GetString("LOC_GI_CloseGlosSITargetUnexpectedError"),
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
        static extern IntPtr FindWindowByCaption(IntPtr intPtrZero, string lpWindowName);

        [DllImport("user32.dll")]
        static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            playniteStarted.Set();
            SettingsViewModel.InitialVerification();
            Api.Database.Tags.Add(LOC_IGNORED_TAG);
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            if (IntegrationEnabled) CloseGlosSITargets();
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            List<GameMenuItem> newGameMenuItems = new List<GameMenuItem>
            {
                new GameMenuItem
                {
                    Description = ResourceProvider.GetString("LOC_GI_GameMenuAddIntegration"),
                    MenuSection = ResourceProvider.GetString("LOC_GI_GameMenuSection"),
                    Action = (arg) => AddGames(arg.Games)
                },

                new GameMenuItem
                {
                    Description = ResourceProvider.GetString("LOC_GI_GameMenuRemoveIntegration"),
                    MenuSection = ResourceProvider.GetString("LOC_GI_GameMenuSection"),
                    Action = (arg) => RemoveGames(arg.Games)
                }
            };

            return newGameMenuItems;
        }

        /// <summary>
        /// Attempts to integrate the games with GlosSI.
        /// Displays a progress bar and a result message.
        /// </summary>
        /// <param name="games">The games to add the GlosSI integration to.</param>
        private void AddGames(List<Game> games)
        {
            logger.Trace("Add integration clicked.");

            if (!SettingsViewModel.InitialVerification()) return;

            bool skipSteamGames = true;

            if (games.TrueForAll((game) => IsSteamGame(game)))
            {
                List<MessageBoxOption> options = new List<MessageBoxOption>
                {
                    new MessageBoxOption(ResourceProvider.GetString("LOCYesLabel"), false, false),
                    new MessageBoxOption(ResourceProvider.GetString("LOCCancelLabel"), true, true)
                };
                if (Api.Dialogs.ShowMessage(games.Count == 1 ?
                    string.Format(ResourceProvider.GetString("LOC_GI_AddGameIsSteamGame"), games[0].Name) :
                    ResourceProvider.GetString("LOC_GI_AddGamesAreSteamGames"),
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"), MessageBoxImage.Warning, options)
                    .Equals(options[1])) return;

                logger.Trace("Adding GlosSI integration to Steam games...");
                skipSteamGames = false;
            }

            int gamesAdded = 0;

            Api.Dialogs.ActivateGlobalProgress((progressBar) => AddGamesProcess(games, progressBar, out gamesAdded, skipSteamGames),
                new GlobalProgressOptions(ResourceProvider.GetString("LOC_GI_AddingIntegrationToGames"), true)
                {
                    IsIndeterminate = false
                });

            logger.Info($"{gamesAdded}/{games.Count} games added.");

            if (gamesAdded == 0)
            {
                Api.Dialogs.ShowMessage(ResourceProvider.GetString("LOC_GI_NoGamesAdded"),
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"));
            }
            else
            {
                int gamesSkipped = games.Count - gamesAdded;
                string message;
                string gamesSkippedMessage;

                if (gamesSkipped == 0)
                {
                    gamesSkippedMessage = "";
                }
                else if (gamesSkipped == 1)
                {
                    gamesSkippedMessage = ResourceProvider.GetString("LOC_GI_OneGameSkipped");
                }
                else
                {
                    gamesSkippedMessage = string.Format(ResourceProvider.GetString("LOC_GI_MultipleGamesSkipped"), gamesSkipped);
                }

                if (gamesAdded == 1)
                {
                    message = string.Format(ResourceProvider.GetString("LOC_GI_OneGameAdded"),
                        gamesSkippedMessage,
                        ResourceProvider.GetString("LOC_GI_RestartSteamReminder"));
                }
                else
                {
                    message = string.Format(ResourceProvider.GetString("LOC_GI_MultipleGamesAdded"),
                        gamesAdded, gamesSkippedMessage,
                        ResourceProvider.GetString("LOC_GI_RestartSteamReminder"));
                }

                Api.Dialogs.ShowMessage(message, ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"));
            }
        }

        /// <summary>
        /// Performs an operation on each game in the game list.
        /// </summary>
        /// <param name="games">The list of games to perform an operation on.</param>
        /// <param name="progressBar">A progress bar for which the progress value is incremented
        /// every time <paramref name="process"/> is run.</param>
        /// <param name="errorMessage">The error message that is shown to the user if 
        /// <paramref name="process"/> throws an exception. 
        /// The message will be formatted with the name of the game and the exception message.</param>
        /// <param name="process">The process to run for each game.</param>
        /// <returns>The number of games for which <paramref name="process"/> returned true.</returns>
        private int ProcessGames(List<Game> games, GlobalProgressActionArgs progressBar,
            string errorMessage, Predicate<Game> process)
        {
            bool hasWarnedUnsupportedCharacters = false;
            int gamesProcessed = 0;
            progressBar.ProgressMaxValue = games.Count();

            using (Api.Database.BufferedUpdate())
            {
                foreach (Game game in games)
                {
                    try
                    {
                        if (process(game)) gamesProcessed++;
                        progressBar.CurrentProgressValue++;
                    }
                    catch (GlosSITargetFile.UnsupportedCharacterException)
                    {
                        if (!hasWarnedUnsupportedCharacters)
                        {
                            hasWarnedUnsupportedCharacters = true;
                            WarnGameHasUnsupportedCharacters();
                        }
                    }
                    catch (GlosSITargetFile.UnexpectedGlosSIBehaviour)
                    {
                        logger.Error(string.Format(errorMessage, game.Name, "UnexpectedGlosSIBehaviour"));
                        break;
                    }
                    catch (Exception e)
                    {
                        DisplayError(string.Format(errorMessage, game.Name, e.Message), e);
                        break;
                    }

                    if (progressBar.CancelToken.IsCancellationRequested) break;
                }
            }

            return gamesProcessed;
        }

        private void AddGamesProcess(List<Game> games, GlobalProgressActionArgs progressBar, out int gamesAdded, bool avoidSteamGames)
        {
            Predicate<Game> process;

            if (avoidSteamGames)
            {
                process = (game) => !IsSteamGame(game) && new GlosSITargetFile(game).Create();
            }
            else
            {
                process = (game) => new GlosSITargetFile(game).Create();
            }

            gamesAdded = ProcessGames(games, progressBar,
                ResourceProvider.GetString("LOC_GI_CreateGlosSITargetUnexpectedError"),
                process);
        }

        private void RemoveGamesProcess(List<Game> games, GlobalProgressActionArgs progressBar, out int gamesRemoved)
        {
            bool process(Game game) => new GlosSITargetFile(game).Remove();

            gamesRemoved = ProcessGames(games, progressBar,
                ResourceProvider.GetString("LOC_GI_RemoveGlosSITargetUnexpectedError"),
                process);
        }

        /// <summary>
        /// Attempts to remove the GlosSI integration of the games.
        /// Displays a progress bar and a result message.
        /// </summary>
        /// <param name="games">The games to remove the GlosSI integration from.</param>
        private void RemoveGames(List<Game> games)
        {
            // TODO: Ask the user for confirmation.

            logger.Trace("Remove integration clicked.");

            if (!SettingsViewModel.InitialVerification()) return;

            int gamesRemoved = 0;

            Api.Dialogs.ActivateGlobalProgress((progressBar) => RemoveGamesProcess(games, progressBar, out gamesRemoved),
                new GlobalProgressOptions(ResourceProvider.GetString("LOC_GI_RemovingIntegrationFromGames"), true)
                {
                    IsIndeterminate = false
                });

            logger.Info($"{gamesRemoved}/{games.Count} games removed.");

            if (gamesRemoved == 0)
            {
                Api.Dialogs.ShowMessage(ResourceProvider.GetString("LOC_GI_NoGamesRemoved"),
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"));
            }
            else if (gamesRemoved == 1)
            {
                Api.Dialogs.ShowMessage(ResourceProvider.GetString("LOC_GI_OneGameRemoved"),
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"));
            }
            else
            {
                Api.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString("LOC_GI_MultipleGamesRemoved"), gamesRemoved),
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"));
            }
        }

        private static void WarnGameHasUnsupportedCharacters()
        {
            WarnUnsupportedCharacters(ResourceProvider.GetString("LOC_GI_GameUnsupportedCharacterWarning"),
                MessageBoxImage.Warning);
        }

        public static void WarnUnsupportedCharacters(string message, MessageBoxImage icon)
        {
            logger.Warn(message);
            List<MessageBoxOption> options = new List<MessageBoxOption>
                        {
                            new MessageBoxOption(ResourceProvider.GetString("LOCOKLabel"), true, true),
                            new MessageBoxOption(ResourceProvider.GetString("LOCMenuHelpTitle"), false, false)
                        };
            MessageBoxOption result = Api.Dialogs.ShowMessage(message,
                ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"), icon, options);
            if (result == options[1])
            {
                GlosSIIntegrationSettingsViewModel.OpenLink("https://github.com/LemmusLemmus/GlosSI-Integration-Playnite/wiki/Limitations#miscellaneous");
            }
        }

        /// <summary>
        /// Checks if a Playnite game is a Steam game.
        /// </summary>
        /// <returns>true if it is a Steam game; false otherwise.</returns>
        public static bool IsSteamGame(Game playniteGame)
        {
            return (playniteGame.Source != null && playniteGame.Source.Name.ToLower() == "steam") ||
                (playniteGame.InstallDirectory != null &&
                Path.GetFullPath(playniteGame.InstallDirectory).Contains(@"Steam\steamapps\common"));
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return SettingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new GlosSIIntegrationSettingsView();
        }

        public override IEnumerable<TopPanelItem> GetTopPanelItems()
        {
            yield return topPanel;
        }

        private void TopPanelPressed()
        {
            logger.Trace("Top panel item pressed.");
            IntegrationEnabled = !IntegrationEnabled;
            CloseGlosSITargets();

            // If the user is currently in-game, launch the game specific overlay.
            if (IntegrationEnabled && isInGame && relevantOverlay != null)
            {
                logger.Trace("Steam Overlay launched whilst in-game.");
                if (!relevantOverlay.RunGlosSITarget()) return;
                if (GetSettings().CloseGameWhenOverlayIsClosed)
                {
                    KillGameWhenGlosSICloses(runningGamePid);
                }
            }
        }

        private void UpdateTopPanel()
        {
            if (IntegrationEnabled)
            {
                topPanel.Title = ResourceProvider.GetString("LOC_GI_TopPanelButtonDisableTooltip");
                topPanelTextBlock.Foreground = GetGlyphBrush();
            }
            else
            {
                topPanel.Title = ResourceProvider.GetString("LOC_GI_TopPanelButtonEnableTooltip");
                topPanelTextBlock.ClearValue(Control.ForegroundProperty);
            }
        }

        private Brush GetGlyphBrush()
        {
            return (Brush)PlayniteApi.Resources.GetResource("GlyphBrush");
        }

        /// <summary>
        /// Updates <c>topPanelTextBlock.Foreground</c> after all plugins (hopefully) have finished initializing, if necessary.
        /// </summary>
        private void InitializeTopPanelColor()
        {
            if (IntegrationEnabled)
            {
                new Thread(() =>
                {
                    Thread.Sleep(2000);
                    UpdateTopPanelGlyphBrush();
                })
                { IsBackground = true }.Start();
            }
        }

        private void UpdateTopPanelGlyphBrush()
        {
            if (IntegrationEnabled)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    topPanelTextBlock.Foreground = GetGlyphBrush();
                });
            }
        }
    }
}