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
            else if (GetSettings().UseDefaultOverlay)
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
                    DisplayError("OnGameStarted-NoJsonFile", 
                        ResourceProvider.GetString("LOC_GI_GlosSITargetNotFoundOnGameStartError"));
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
        /// Displays an error as a Playnite notification and logs it.
        /// </summary>
        /// <param name="source">The source of the error.</param>
        /// <param name="message">The user-readable error message.</param>
        /// <param name="exception">The exception, if one exists.</param>
        public void DisplayError(string source, string message, Exception exception = null)
        {
            if (exception == null)
            {
                logger.Error(message);
            }
            else
            {
                logger.Error(exception, message);
            }
            Api.Notifications.Add($"{Id}-{source}", message, NotificationType.Error);
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
            
            new SteamGameID(GetSettings().PlayniteOverlayName).Run();
        }

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
                    proc.CloseMainWindow();
                }
                foreach (Process proc in glosSITargets)
                {
                    if (!proc.WaitForExit(10000))
                    {
                        DisplayError("CloseGlosSITargets", 
                            ResourceProvider.GetString("LOC_GI_CloseGlosSITargetTimelyUnexpectedError"));
                    }
                    proc.Close();
                }
            }
            catch (InvalidOperationException) { }
            catch (PlatformNotSupportedException e)
            {
                DisplayError("CloseGlosSITargets", 
                    string.Format(ResourceProvider.GetString("LOC_GI_CloseGlosSITargetUnexpectedError"), e.Message), e);
            }
        }

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
            else if (gamesAdded == 1)
            {
                Api.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString("LOC_GI_OneGameAdded"), 
                    ResourceProvider.GetString("LOC_GI_RestartSteamReminder")), 
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"));
            }
            else
            {
                int gamesSkipped = games.Count - gamesAdded;
                
                Api.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString("LOC_GI_MultipleGamesAdded"), 
                    gamesAdded, 
                    gamesSkipped > 0 ? string.Format(ResourceProvider.GetString("LOC_GI_GamesSkipped"), gamesSkipped) : 
                    "", ResourceProvider.GetString("LOC_GI_RestartSteamReminder")), 
                    ResourceProvider.GetString("LOC_GI_DefaultWindowTitle"));
            }
        }

        private void AddGamesProcess(List<Game> games, GlobalProgressActionArgs progressBar, out int gamesAdded, bool avoidSteamGames)
        {
            gamesAdded = 0;
            progressBar.ProgressMaxValue = games.Count();

            using (Api.Database.BufferedUpdate())
            {
                foreach (Game game in games)
                {
                    try
                    {
                        if (avoidSteamGames && IsSteamGame(game)) continue;

                        if (new GlosSITarget(game).Create()) gamesAdded++;
                    }
                    catch (Exception e)
                    {
                        
                        DisplayError("GeneralAddGames", 
                            string.Format(ResourceProvider.GetString("LOC_GI_CreateGlosSITargetUnexpectedError"), 
                            game.Name, e.Message), e);
                        return;
                    }

                    if (progressBar.CancelToken.IsCancellationRequested) return;
                }
            }
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

        private void RemoveGamesProcess(List<Game> games, GlobalProgressActionArgs progressBar, out int gamesRemoved)
        {
            gamesRemoved = 0;
            progressBar.ProgressMaxValue = games.Count();

            using (Api.Database.BufferedUpdate())
            {
                foreach (Game game in games)
                {
                    try
                    {
                        if ((new GlosSITarget(game)).Remove())
                        {
                            gamesRemoved++;
                        }
                        progressBar.CurrentProgressValue++;
                    }
                    catch (Exception e)
                    {
                        DisplayError("RemoveGames", 
                            string.Format(ResourceProvider.GetString("LOC_GI_RemoveGlosSITargetUnexpectedError"),
                            game.Name, e.Message), e);
                        return;
                    }
                    if (progressBar.CancelToken.IsCancellationRequested) return;
                }
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