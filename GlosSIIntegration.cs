using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows;
using System.Threading;

namespace GlosSIIntegration
{
    public class GlosSIIntegration : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private GlosSIIntegrationSettingsViewModel settingsViewModel { get; set; }
        internal static IPlayniteAPI API { get; private set; }
        public static readonly string INTEGRATED_TAG = "[GI] Integrated", IGNORED_TAG = "[GI] Ignored";
        public static GlosSIIntegration Instance { get; private set; }

        public override Guid Id { get; } = Guid.Parse("6b0297da-75e5-4330-bb2d-b64bff22c315");

        private readonly TopPanelItem topPanel;
        private readonly TextBlock topPanelTextBlock;

        public GlosSIIntegration(IPlayniteAPI api) : base(api)
        {
            settingsViewModel = new GlosSIIntegrationSettingsViewModel(this, api);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
            API = api;
            Instance = this;

            topPanelTextBlock = GetInitialTopPanelTextBlock();
            topPanel = GetInitialTopPanel();
            UpdateTopPanel();
            InitializeTopPanelColor();
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
            return Instance.settingsViewModel.Settings;
        }

        // Method code heavily inspired by https://github.com/darklinkpower's PlayniteUtilites AddTagToGame method
        // from their PlayniteExtensionsCollection repository.
        public static void AddTagToGame(string tagName, Game game)
        {
            Tag tag = API.Database.Tags.Add(tagName);

            if(game.Tags == null)
            {
                game.TagIds = new List<Guid> { tag.Id };
                API.Database.Games.Update(game);
            }
            else if (!game.TagIds.Contains(tag.Id))
            {
                game.TagIds.Add(tag.Id);
                API.Database.Games.Update(game);
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
                API.Database.Games.Update(game);
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
            // TODO: The user might not necessarily always want the previous overlay to be closed when a new game is started.
            CloseGlosSITargets();

            if (GameHasIgnoredTag(args.Game)) return;
            
            if (GetSettings().IntegrationEnabled && GameHasIntegratedTag(args.Game))
            {
                if (!(new GlosSITarget(args.Game)).HasJsonFile())
                {
                    API.Notifications.Add($"{Id}-OnGameStarted-NoJsonFile",
                        $"GlosSI Integration failed to run the Steam Shortcut: The .json target file is missing.",
                        NotificationType.Error);
                    // TODO: Remove the GlosSI Integrated tag?
                    return;
                }

                try
                {
                    new SteamGameID(args.Game).Run();
                }
                catch (Exception e)
                {
                    API.Notifications.Add($"{Id}-OnGameStarted-OverlayOpened",
                        $"GlosSI Integration failed to run the Steam Shortcut:\n{e}",
                        NotificationType.Error);
                }

                if (GetSettings().CloseGameWhenOverlayIsClosed)
                {
                    // TODO: Set up a thread that closes the application when the overlay is closed via the overlay itself (i.e. forcefully closed).
                }
            }
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (GameHasIgnoredTag(args.Game)) return;

            if (GetSettings().IntegrationEnabled && GameHasIntegratedTag(args.Game))
            {
                CloseGlosSITargets();
                // TODO: Run below method only if in fullscreen mode.
                // RunPlayniteOverlay();
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
                API.Notifications.Add($"{Id}-RunPlayniteOverlay",
                    $"GlosSI Integration failed to run the Playnite Overlay Steam Shortcut: \n{e}",
                    NotificationType.Error);
            }
        }

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
                        API.Notifications.Add($"{Id}-CloseGlosSITargets",
                            $"GlosSI Integration failed to close the Steam Overlay in time.",
                            NotificationType.Error);
                    }
                    proc.Close();
                }
            }
            catch (InvalidOperationException) { }
            catch (PlatformNotSupportedException e)
            {
                API.Notifications.Add($"{Id}-CloseGlosSITargets", 
                    $"GlosSI Integration failed to close the Steam Shortcut:\n{e}", 
                    NotificationType.Error);
            }
        }

        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
            // Add code to be executed when game is uninstalled.
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Add code to be executed when Playnite is initialized.

            // TODO: Verify settings.
            // Use a non-serialized variable in settings to keep track of if the settings are valid?
            // Check that the settings have been verified before reacting to any event & the topPanel.

            API.Database.Tags.Add(IGNORED_TAG);
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // Add code to be executed when Playnite is shutting down.
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

        private void AddGames(List<Game> games)
        {
            int gamesAdded = 0;

            API.Dialogs.ActivateGlobalProgress((progressBar) => AddGamesProcess(games, progressBar, out gamesAdded),
                new GlobalProgressOptions("Adding GlosSI integration to games...", true)
                {
                    IsIndeterminate = false
                });

            if (gamesAdded == 0)
            {
                API.Dialogs.ShowMessage($"No games were added as GlosSI Steam Shortcuts. " +
                $"This could be due to the games being Steam games, already having been added or having the ignored tag.", "GlosSI Integration");
            }
            if (gamesAdded == 1)
            {
                API.Dialogs.ShowMessage($"The game was successfully added as GlosSI Steam Shortcut. " +
                $"Steam has to be restarted for the changes to take effect!", "GlosSI Integration");
            }
            else
            {
                int gamesSkipped = games.Count - gamesAdded;
                API.Dialogs.ShowMessage($"{gamesAdded} games were successfully added as GlosSI Steam Shortcuts{(gamesSkipped > 0 ? $" ({gamesSkipped} games were skipped)" : "")}. " +
                $"Steam has to be restarted for the changes to take effect!", "GlosSI Integration");
            }
        }

        private void AddGamesProcess(List<Game> games, GlobalProgressActionArgs progressBar, out int gamesAdded)
        {
            gamesAdded = 0;
            progressBar.ProgressMaxValue = games.Count();

            using (API.Database.BufferedUpdate())
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
                    catch (FileNotFoundException)
                    {
                        API.Notifications.Add($"{Id}-AddGamesFileMissing",
                            "The DefaultTarget.json file was not found. The adding process was aborted.",
                            NotificationType.Error);
                        return;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        API.Notifications.Add($"{Id}-AddGamesDirMissing",
                            "The GlosSI Target Path directory could not be found. The adding process was aborted.",
                            NotificationType.Error);
                        return;
                    }
                    catch (Exception e)
                    {
                        API.Notifications.Add($"{Id}-GeneralAddGames", $"GlosSI Integration failed to add the GlosSI Target " +
                            $"Configuration file for {game.Name}, the adding process was aborted:\n" +
                            $"{e}", NotificationType.Error);
                        return;
                    }
                    if (progressBar.CancelToken.IsCancellationRequested) return;
                }
            }
        }

        private void RemoveGames(List<Game> games)
        {
            // TODO: Ask the user for confirmation.

            int gamesRemoved = 0;

            API.Dialogs.ActivateGlobalProgress((progressBar) => RemoveGamesProcess(games, progressBar, out gamesRemoved), 
                new GlobalProgressOptions("Removing GlosSI integration from games...", true)
                {
                    IsIndeterminate = false
                });

            if (gamesRemoved == 0)
            {
                API.Dialogs.ShowMessage("No GlosSI/Steam integrations were removed.",
                    "GlosSI Integration");
            }
            else if (gamesRemoved == 1)
            {
                API.Dialogs.ShowMessage($"The GlosSI/Steam integration of the game \"{games[0].Name}\" was removed!",
                    "GlosSI Integration");
            }
            else
            {
                API.Dialogs.ShowMessage($"The GlosSI/Steam integration of {gamesRemoved} games were removed!", 
                    "GlosSI Integration");
            }
        }

        private void RemoveGamesProcess(List<Game> games, GlobalProgressActionArgs progressBar, out int gamesRemoved)
        {
            gamesRemoved = 0;
            progressBar.ProgressMaxValue = games.Count();

            using (API.Database.BufferedUpdate())
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
                        API.Notifications.Add($"{Id}-RemoveGames", $"GlosSI Integration failed to remove the GlosSI Target " +
                            $"Configuration file for {game.Name}, the removal process was aborted:\n" +
                            $"{e}", NotificationType.Error);
                        return;
                    }
                    if (progressBar.CancelToken.IsCancellationRequested) return;
                }
            }
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settingsViewModel;
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
            GetSettings().IntegrationEnabled = !GetSettings().IntegrationEnabled;
            CloseGlosSITargets();
            UpdateTopPanel();

            // TODO: If IntegrationEnabled, check if the user is in-game, and if so start the game specific overlay.
        }

        private void UpdateTopPanel()
        {
            if (GetSettings().IntegrationEnabled)
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
            if (GetSettings().IntegrationEnabled)
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
            if (GetSettings().IntegrationEnabled)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    topPanelTextBlock.Foreground = GetGlyphBrush();
                });
            }
        }
    }
}