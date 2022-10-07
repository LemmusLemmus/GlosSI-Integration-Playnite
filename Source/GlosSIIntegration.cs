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
        private SteamGameID runningGameOverlay;

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

            runningGameOverlay = null;
            topPanelTextBlock = GetInitialTopPanelTextBlock();
            topPanel = GetInitialTopPanel();
            InitializeIntegrationEnabled();
            InitializeTopPanelColor();
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

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            if (GameHasIgnoredTag(args.Game)) return;

            CloseGlosSITargets();

            string overlayName;
            
            if (GameHasIntegratedTag(args.Game))
            {
                overlayName = args.Game.Name;
                runningGameOverlay = new SteamGameID(args.Game);
            }
            else if (GetSettings().UseDefaultOverlay && !IsSteamGame(args.Game))
            {
                overlayName = GetSettings().DefaultOverlayName;
                runningGameOverlay = new SteamGameID(overlayName);
            }
            else
            {
                return;
            }

            if (IntegrationEnabled)
            {
                if (!GlosSITarget.HasJsonFile(overlayName))
                {
                    // TODO: Make the notification more helpful.
                    // A currently probable reason for this happening is due to a name change.
                    // Perhaps add a help link?
                    DisplayError(ResourceProvider.GetString("LOC_GI_GlosSITargetNotFoundOnGameStartError"));
                    return;
                }

                runningGameOverlay.Run();

                if (GetSettings().CloseGameWhenOverlayIsClosed)
                {
                    // TODO: Set up a thread that closes the application when the overlay is closed via the overlay itself (i.e. forcefully closed).
                    // Alternatively start this thread in OnGameStarted().
                    // The GlosSITarget log can be checked to determine if the application was forcefully closed or not.
                    //logger.Trace("GlosSI watcher thread started...");
                }
            }
        }

        /// <summary>
        /// Displays an error message and logs it.
        /// </summary>
        /// <param name="message">The user-readable error message.</param>
        /// <param name="exception">The exception, if one exists.</param>
        public void DisplayError(string message, Exception exception = null)
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

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (GameHasIgnoredTag(args.Game)) return;

            runningGameOverlay = null;

            if (IntegrationEnabled)
            {
                CloseGlosSITargets();
                if (Api.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
                {
                    RunPlayniteOverlay();
                }
            }
        }

        /// <summary>
        /// Starts the Playnite GlosSI/Steam overlay, if the user has enabled/configured one.
        /// </summary>
        private void RunPlayniteOverlay()
        {
            if (!GetSettings().UsePlayniteOverlay) return;

            // It is up to personal preference whether to start this in a new thread.
            // The difference is whether the user can see their library or a "loading" screen while the Steam overlay is starting.
            new Thread(() =>
            {
                new SteamGameID(GetSettings().PlayniteOverlayName).Run();
                try
                {
                    ReturnStolenFocus("GlosSITarget");
                }
                catch (TimeoutException e)
                {
                    logger.Warn($"Failed to return focus to Playnite: {e.Message}");
                }
            }).Start();
        }

        /// <summary>
        /// Assuming that <paramref name="processName"/> has or will soon steal the focus from this application, returns the focus to this application.
        /// </summary>
        /// <param name="processName">The name of the process that will steal window focus.</param>
        /// <exception cref="TimeoutException">If an operation took too long.</exception>
        private static void ReturnStolenFocus(string processName)
        {
            Process[] p;
            int sleptTime = 0;

            // Wait for the process to start, if it has not already.
            while ((p = Process.GetProcessesByName(processName)).Length == 0)
            {
                Thread.Sleep(300);
                if ((sleptTime += 300) > 10000)
                {
                    throw new TimeoutException("Failed to find a GlosSITarget process to return focus from in time.");
                }
            }

            if (p.Length > 1) logger.Warn($"Multiple ({p.Length}) GlosSITargets were found in full screen mode.");

            // Wait for the process to steal focus, if it has not already.
            WaitForStolenFocus(p[0]);
            FocusSelf();
            // For some reason focus is sometimes stolen twice.
            // An alternative solution is to simply use a delay of say 250 ms before calling FocusSelf().
            WaitForStolenFocus(p[0]);
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

            if (!SetForegroundWindow(Process.GetCurrentProcess().MainWindowHandle))
            {
                logger.Warn("Setting foreground window to Playnite failed.");
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
            try
            {
                Process[] glosSITargets = Process.GetProcessesByName("GlosSITarget");

                // It is assumed that there is no reason for the user to ever want to have multiple GlosSITargets
                // running simultaneously. As such, they are all closed.
                foreach (Process proc in glosSITargets)
                {
                    if(!proc.CloseMainWindow())
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
                        string errorMessage = ResourceProvider.GetString("LOC_GI_CloseGlosSITargetTimelyUnexpectedError");
                        logger.Error(errorMessage);
                        Api.Notifications.Add("GlosSIIntegration-FailedToCloseGlosSITarget", errorMessage, NotificationType.Error);
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
            SettingsViewModel.InitialVerification();
            Api.Database.Tags.Add(LOC_IGNORED_TAG);
            if (Api.ApplicationInfo.Mode == ApplicationMode.Fullscreen && IntegrationEnabled)
            {
                RunPlayniteOverlay();
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            CloseGlosSITargets();
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
        /// every time <paramref name="proccess"/> is run.</param>
        /// <param name="errorMessage">The error message that is shown to the user if 
        /// <paramref name="proccess"/> throws an exception. 
        /// The message will be formatted with the name of the game and the exception message.</param>
        /// <param name="proccess">The proccess to run for each game.</param>
        /// <returns>The number of games for which <paramref name="proccess"/> returned true.</returns>
        private int ProcessGames(List<Game> games, GlobalProgressActionArgs progressBar,
            string errorMessage, Predicate<Game> proccess)
        {
            bool hasWarnedUnsupportedCharacters = false;
            int gamesProccessed = 0;
            progressBar.ProgressMaxValue = games.Count();

            using (Api.Database.BufferedUpdate())
            {
                foreach (Game game in games)
                {
                    try
                    {
                        if (proccess(game)) gamesProccessed++;
                        progressBar.CurrentProgressValue++;
                    }
                    catch (GlosSITarget.UnsupportedCharacterException)
                    {
                        if (!hasWarnedUnsupportedCharacters)
                        {
                            hasWarnedUnsupportedCharacters = true;
                            WarnGameHasUnsupportedCharacters();
                        }
                    }
                    catch (GlosSITarget.UnexpectedGlosSIBehaviour)
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

            return gamesProccessed;
        }

        private void AddGamesProcess(List<Game> games, GlobalProgressActionArgs progressBar, out int gamesAdded, bool avoidSteamGames)
        {
            Predicate<Game> process;

            if (avoidSteamGames)
            {
                process = (game) => !IsSteamGame(game) && new GlosSITarget(game).Create();
            }
            else
            {
                process = (game) => new GlosSITarget(game).Create();
            }

            gamesAdded = ProcessGames(games, progressBar,
                ResourceProvider.GetString("LOC_GI_CreateGlosSITargetUnexpectedError"),
                process);
        }

        private void RemoveGamesProcess(List<Game> games, GlobalProgressActionArgs progressBar, out int gamesRemoved)
        {
            bool process(Game game) => new GlosSITarget(game).Remove();

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
            if (runningGameOverlay != null && IntegrationEnabled)
            {
                logger.Trace("Steam Overlay launched whilst in-game.");
                runningGameOverlay.Run();
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

        // Commented out due to the method probably being overkill.
        // If the user changes the constants, a simple restart or toggling of the button will update the color used.
        // There are probably better ways to do this.
        /*
        /// <summary>
        /// Creates a FileSystemWatcher that updates the color of the TopPanelTextBox if 
        /// the brush used is potentially changed by Lacro59's plugin ThemeModifier.
        /// </summary>
        private FileSystemWatcher CreateThemeModifierWatcher()
        {
            FileSystemWatcher watcher = null;
            try
            {
                watcher = new FileSystemWatcher
                {
                    Path = Path.GetFullPath(Path.Combine(GetPluginUserDataPath(), "..", "ec2f4013-17e6-428a-b8a9-5e34a3b80009")),
                    NotifyFilter = NotifyFilters.LastAccess | NotifyFilters.LastWrite,
                    Filter = "config.json",
                    EnableRaisingEvents = true
                };
                watcher.Changed += new FileSystemEventHandler(OnThemeModifierChanged);
            }
            catch { }
            return watcher;
        }

        private void OnThemeModifierChanged(object source, FileSystemEventArgs e)
        {
            UpdateTopPanelGlyphBrush();
        }*/

        /// <summary>
        /// Updates <c>topPanelTextBlock.Foreground</c> after all plugins (hopefully) have finished initializing, if necessary.
        /// </summary>
        private void InitializeTopPanelColor()
        {
            if (IntegrationEnabled)
            {
                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;
                    Thread.Sleep(2000);
                    UpdateTopPanelGlyphBrush();
                }).Start();
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