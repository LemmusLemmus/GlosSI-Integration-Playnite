using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Diagnostics;
using System.IO;

namespace GlosSIIntegration
{
    public class GlosSIIntegration : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private GlosSIIntegrationSettingsViewModel settingsViewModel { get; set; }
        private Process glosSIOverlay;
        private readonly IPlayniteAPI API;
        private static IGameDatabase database;
        public static readonly string INTEGRATED_TAG = "[GI] Integrated", IGNORED_TAG = "[GI] Ignored";
        public static GlosSIIntegration Instance { get; private set; }

        public override Guid Id { get; } = Guid.Parse("6b0297da-75e5-4330-bb2d-b64bff22c315");

        public GlosSIIntegration(IPlayniteAPI api) : base(api)
        {
            settingsViewModel = new GlosSIIntegrationSettingsViewModel(this, api);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
            glosSIOverlay = null;
            API = api;
            database = api.Database;
            Instance = this;
        }

        public static GlosSIIntegrationSettings GetSettings()
        {
            return Instance.settingsViewModel.Settings;
        }

        // Method code heavily inspired by https://github.com/darklinkpower's PlayniteUtilites AddTagToGame method
        // from their PlayniteExtensionsCollection repository.
        public static void AddTagToGame(string tagName, Game game)
        {
            Tag tag = database.Tags.Add(tagName);

            if(game.Tags == null)
            {
                game.TagIds = new List<Guid> { tag.Id };
                database.Games.Update(game);
            }
            else if (!game.TagIds.Contains(tag.Id))
            {
                game.TagIds.Add(tag.Id);
                database.Games.Update(game);
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
                database.Games.Update(game);
            }
        }

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            // Add code to be executed when game is finished installing.
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            if (GameHasIgnoredTag(args.Game)) return;

            if(glosSIOverlay != null)
            {
                // TODO: Give the user a choice? Replace the already open overlay or do not open a new overlay.
                // If the overlay is replaced, make sure that this doesn't also close the previous game.
            }
            else if(GetSettings().IntegrationEnabled && GameHasIntegratedTag(args.Game))
            {
                // TODO: Stop any already running GlosSI overlays.

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
                    glosSIOverlay = (new SteamGameID(args.Game)).Run();
                }
                catch (Exception e)
                {
                    API.Notifications.Add($"{Id}-OnGameStarted-OverlayOpened",
                        $"GlosSI Integration failed to run the Steam Shortcut: {e.Message}",
                        NotificationType.Error);
                }
            }
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
            return game.Tags.Any(t => t.Name == tagName);
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            if(GetSettings().CloseGameWhenOverlayIsClosed)
            {
                // TODO: Set up a thread that closes the application when the overlay is closed.
            }

        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if (GameHasIgnoredTag(args.Game)) return;

            if (GetSettings().IntegrationEnabled && GameHasIntegratedTag(args.Game))
            {
                try
                {
                    glosSIOverlay.CloseMainWindow();
                    glosSIOverlay = null;
                }
                catch (InvalidOperationException) { }
                catch (PlatformNotSupportedException e)
                {
                    API.Notifications.Add($"{Id}-OnGameStopped", $"GlosSI Integration failed to close the Steam Shortcut: {e.Message}", NotificationType.Error);
                }

                // TODO: Start the playnite GlosSI overlay, if the user has configured one.
            }
        }

        public override void OnGameUninstalled(OnGameUninstalledEventArgs args)
        {
            // Add code to be executed when game is uninstalled.
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            // Add code to be executed when Playnite is initialized.
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            // Add code to be executed when Playnite is shutting down.
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
                    Action = (arg) => AddGames(arg)
                },

                new GameMenuItem
                {
                    Description = "Remove Integration",
                    MenuSection = "GlosSI Integration",
                    Action = (arg) => RemoveGames(arg)
                }
            };

            return newGameMenuItems;
        }

        private void AddGames(GameMenuItemActionArgs args)
        {
            int addedGames = 0;

            foreach (Game game in args.Games)
            {
                try
                {
                    if ((new GlosSITarget(game)).Create())
                    {
                        addedGames++;
                    }
                }
                catch(FileNotFoundException)
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
                        $"Configuration file for {game.Name}, the adding process was aborted: " +
                        $"{e.Message}", NotificationType.Error);
                    return;
                }
            }

            if (addedGames > 0)
            {
                API.Dialogs.ShowMessage($"{addedGames}/{args.Games.Count} games were successfully added as GlosSI Steam Shortcuts. " +
                $"Steam has to be restarted for the changes to take effect!", "GlosSI Integration");
            }
            else
            {
                API.Dialogs.ShowMessage($"No games were added as GlosSI Steam Shortcuts. " +
                $"This could be due to the games being Steam games, already having been added or having the ignored tag.", "GlosSI Integration");
            }
        }

        private void RemoveGames(GameMenuItemActionArgs args)
        {
            // TODO: Ask the user for confirmation.

            foreach (Game game in args.Games)
            {
                try
                {
                    (new GlosSITarget(game)).Remove();
                }
                catch (Exception e)
                {
                    API.Notifications.Add($"{Id}-RemoveGames", $"GlosSI Integration failed to remove the GlosSI Target " +
                        $"Configuration file for {game.Name}, the removal process was aborted: " +
                        $"{e.Message}", NotificationType.Error);
                    return;
                }
            }

            // TODO: Message the user that the operation succeeded and that Steam should be restarted.
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settingsViewModel;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new GlosSIIntegrationSettingsView();
        }
    }
}