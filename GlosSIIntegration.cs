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

namespace GlosSIIntegration
{
    public class GlosSIIntegration : GenericPlugin
    {
        public static readonly string INTEGRATED_TAG = "[GI] Integrated", IGNORED_TAG = "[GI] Ignored";
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

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            // Add code to be executed when game is finished installing.
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {

        }

        public static bool GameHasIntegratedTag(Game game)
        {
            return GameHasTag(game, INTEGRATED_TAG);
        }

        public static bool GameHasIgnoredTag(Game game)
        {
            return GameHasTag(game, IGNORED_TAG);
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
                    DisplayError("OnGameStarted-NoJsonFile", 
                        "GlosSI Integration failed to run the Steam Shortcut: The .json target file could not be found.");
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
        /// <param name="fullError">The full error message, if one exists.</param>
        public void DisplayError(string source, string message, string fullError = null)
        {
            logger.Error($"{message}{(fullError != null ? $"\t{fullError}" : "")}");
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

            try
            {
                new SteamGameID(GetSettings().PlayniteOverlayName).Run();
            }
            catch (Exception e)
            {
                DisplayError("RunPlayniteOverlay", $"GlosSI Integration failed to run the Playnite Overlay Steam Shortcut: \n{e.Message}", e.ToString());
            }
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
                        DisplayError("CloseGlosSITargets", "GlosSI Integration failed to close the Steam Overlay in time.");
                    }
                    proc.Close();
                }
            }
            catch (InvalidOperationException) { }
            catch (PlatformNotSupportedException e)
            {
                DisplayError("CloseGlosSITargets", $"GlosSI Integration failed to close the Steam Shortcut:\n{e.Message}", e.ToString());
            }
        }

        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
            // Add code to be executed when game is uninstalled.
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            SettingsViewModel.InitialVerification();
            Api.Database.Tags.Add(IGNORED_TAG);
            if (Api.ApplicationInfo.Mode == ApplicationMode.Fullscreen && IntegrationEnabled)
            {
                RunPlayniteOverlay();
            }
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            CloseGlosSITargets();
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            // Add code to be executed when library is updated.
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            List<GameMenuItem> newGameMenuItems = new List<GameMenuItem>
            {
                new GameMenuItem
                {
                    Description = "Add Integration",
                    MenuSection = "GlosSI Integration",
                    Action = (arg) => AddGames(arg.Games)
                },

                new GameMenuItem
                {
                    Description = "Remove Integration",
                    MenuSection = "GlosSI Integration",
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

            int gamesAdded = 0;

            Api.Dialogs.ActivateGlobalProgress((progressBar) => AddGamesProcess(games, progressBar, out gamesAdded),
                new GlobalProgressOptions("Adding GlosSI integration to games...", true)
                {
                    IsIndeterminate = false
                });

            logger.Info($"{gamesAdded}/{games.Count} games added.");

            if (gamesAdded == 0)
            {
                Api.Dialogs.ShowMessage($"No games were added as GlosSI Steam Shortcuts. " +
                $"This could be due to the games being Steam games, already having been added or having the ignored tag.", "GlosSI Integration");
            }
            if (gamesAdded == 1)
            {
                Api.Dialogs.ShowMessage($"The game was successfully added as GlosSI Steam Shortcut. " +
                $"Steam has to be restarted for the changes to take effect!", "GlosSI Integration");
            }
            else
            {
                int gamesSkipped = games.Count - gamesAdded;
                Api.Dialogs.ShowMessage($"{gamesAdded} games were successfully added as GlosSI Steam Shortcuts{(gamesSkipped > 0 ? $" ({gamesSkipped} games were skipped)" : "")}. " +
                $"Steam has to be restarted for the changes to take effect!", "GlosSI Integration");
            }
        }

        private void AddGamesProcess(List<Game> games, GlobalProgressActionArgs progressBar, out int gamesAdded)
        {
            gamesAdded = 0;
            progressBar.ProgressMaxValue = games.Count();

            using (Api.Database.BufferedUpdate())
            {
                foreach (Game game in games)
                {
                    try
                    {
                        if ((new GlosSITarget(game)).Create())
                        {
                            gamesAdded++;
                        }
                    }
                    catch (Exception e)
                    {
                        DisplayError("GeneralAddGames", $"GlosSI Integration failed to add the GlosSI Target " +
                            $"Configuration file for {game.Name}, the adding process was aborted:\n" +
                            $"{e.Message}", e.ToString());
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
                new GlobalProgressOptions("Removing GlosSI integration from games...", true)
                {
                    IsIndeterminate = false
                });

            logger.Info($"{gamesRemoved}/{games.Count} games removed.");

            if (gamesRemoved == 0)
            {
                Api.Dialogs.ShowMessage("No GlosSI/Steam integrations were removed.",
                    "GlosSI Integration");
            }
            else if (gamesRemoved == 1)
            {
                Api.Dialogs.ShowMessage($"The GlosSI/Steam integration of the game \"{games[0].Name}\" was removed!",
                    "GlosSI Integration");
            }
            else
            {
                Api.Dialogs.ShowMessage($"The GlosSI/Steam integration of {gamesRemoved} games were removed!", 
                    "GlosSI Integration");
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
                        DisplayError("RemoveGames", $"Failed to remove the GlosSI Target " +
                            $"Configuration file for {game.Name}, the removal process was aborted:\n" +
                            $"{e.Message}", e.ToString());
                        return;
                    }
                    if (progressBar.CancelToken.IsCancellationRequested) return;
                }
            }
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
                topPanel.Title = "Disable GlosSI Integration";
                topPanelTextBlock.Foreground = GetGlyphBrush();
            }
            else
            {
                topPanel.Title = "Enable GlosSI Integration";
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