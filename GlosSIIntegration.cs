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

namespace GlosSIIntegration
{
    public class GlosSIIntegration : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private GlosSIIntegrationSettingsViewModel settings { get; set; }
        private Process glosSIOverlay;
        private readonly INotificationsAPI notifications;
        public static readonly string INTEGRATED_TAG = "[GI] Integrated";

        public override Guid Id { get; } = Guid.Parse("6b0297da-75e5-4330-bb2d-b64bff22c315");

        public GlosSIIntegration(IPlayniteAPI api) : base(api)
        {
            settings = new GlosSIIntegrationSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };
            glosSIOverlay = null;
            notifications = api.Notifications;
        }

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            // Add code to be executed when game is finished installing.
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            if(settings.Settings.IntegrationEnabled && GameHasIntegratedTag(args.Game) && glosSIOverlay == null)
            {
                // TODO: Stop any already running GlosSI overlays.

                if (!GlosSITarget.HasJsonFile(args.Game))
                {
                    notifications.Add($"{Id}-OnGameStarted-NoJsonFile",
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
                    notifications.Add($"{Id}-OnGameStarted-OverlayOpened",
                        $"GlosSI Integration failed to run the Steam Shortcut: {e.Message}",
                        NotificationType.Error);
                }
            }
        }

        private static bool GameHasIntegratedTag(Game game)
        {
            return game.Tags.Any(t => t.Name == INTEGRATED_TAG);
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            if(settings.Settings.CloseGameWhenOverlayIsClosed)
            {
                // TODO: Set up a thread that closes the application when the overlay is closed.
            }

        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            if(settings.Settings.IntegrationEnabled && GameHasIntegratedTag(args.Game))
            {
                try
                {
                    glosSIOverlay.CloseMainWindow();
                    glosSIOverlay = null;
                }
                catch (InvalidOperationException) { }
                catch (PlatformNotSupportedException e)
                {
                    notifications.Add($"{Id}-OnGameStopped", $"GlosSI Integration failed to close the Steam Shortcut: {e.Message}", NotificationType.Error);
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

        }

        private void RemoveGames(GameMenuItemActionArgs args)
        {
            // TODO: Ask the user for confirmation.
            try
            {
                foreach (Game game in args.Games)
                {
                    GlosSITarget.Remove(game);
                }
            }
            catch (Exception e)
            {
                notifications.Add($"{Id}-RemoveGames", $"GlosSI Integration failed to remove a GlosSI Target Configuration file, the removal process was aborted: {e.Message}", NotificationType.Error);
            }
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new GlosSIIntegrationSettingsView();
        }
    }
}