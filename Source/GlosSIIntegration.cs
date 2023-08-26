using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows;
using System.Threading;
using System.IO;
using GlosSIIntegration.Models.Overlays;
using GlosSIIntegration.Models.GlosSITargets.Files;
using GlosSIIntegration.Models.GlosSITargets.Types;

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

        private bool integrationEnabled;
        public bool IntegrationEnabled
        {
            get { return integrationEnabled; }
            private set
            {
                integrationEnabled = value;
                UpdateTopPanel();
                IntegrationToggledEvent?.Invoke(value);
            }
        }

        public override Guid Id { get; } = Guid.Parse("6b0297da-75e5-4330-bb2d-b64bff22c315");
        public static IPlayniteAPI Api { get; private set; }
        public static GlosSIIntegration Instance { get; private set; }
        private static volatile bool hasPlayniteStarted = false;
        /// <summary>
        /// True when Playnite has finished starting.
        /// </summary>
        public static bool HasPlayniteStarted { get { return hasPlayniteStarted; } }
        public event Action<OnApplicationStoppedEventArgs> ApplicationStoppedEvent;
        public event Action<OnGameStartingEventArgs> GameStartingEvent;
        public event Action<OnGameStartedEventArgs> GameStartedEvent;
        public event Action<OnGameStoppedEventArgs> GameStoppedEvent;
        public event Action<OnGameStartupCancelledEventArgs> GameStartupCancelledEvent;
        public event Action<OnApplicationStartedEventArgs> ApplicationStartedEvent;
        public event Action<bool> IntegrationToggledEvent;
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

            topPanelTextBlock = GetInitialTopPanelTextBlock();
            topPanel = GetInitialTopPanel();
            InitializeIntegrationEnabled();
            InitializeTopPanelColor();

            // Initialize automatic overlay switching.
            new OverlaySwitchingDecisionMaker();
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
            GameStartingEvent?.Invoke(args);
        }

        public override void OnGameStarted(OnGameStartedEventArgs args)
        {
            GameStartedEvent?.Invoke(args);
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
            // Both logging and Notifications.Add should be thread-safe and OK to call from a non-UI thread.
            logger.Error(message);
            Api.Notifications.Add(id, message, NotificationType.Error);
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            GameStoppedEvent?.Invoke(args);
        }

        public override void OnGameStartupCancelled(OnGameStartupCancelledEventArgs args)
        {
            GameStartupCancelledEvent?.Invoke(args);
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            hasPlayniteStarted = true;
            SettingsViewModel.InitialVerification();
            Api.Database.Tags.Add(LOC_IGNORED_TAG);
            ApplicationStartedEvent?.Invoke(args);
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            ApplicationStoppedEvent?.Invoke(args);
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

        // TODO: Move this to a new class.
        // TODO: Outright refuse to add games if the version requirement is not fullfilled.
        // Also reword version error message, and update code in GlosSITargetFile.cs
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
                    catch (GlosSITargetFile.UnexpectedGlosSIBehaviourException)
                    {
                        logger.Error(string.Format(errorMessage, game.Name, "UnexpectedGlosSIBehaviour"));
                        break;
                    }
                    // A lot of things could potentially go wrong.
                    // Notify the user of any potential error instead of crashing.
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
                process = (game) => !string.IsNullOrEmpty(game.Name) && !IsSteamGame(game) && new GameGlosSITarget(game).File.Create();
            }
            else
            {
                process = (game) => !string.IsNullOrEmpty(game.Name) && new GameGlosSITarget(game).File.Create();
            }

            gamesAdded = ProcessGames(games, progressBar,
                ResourceProvider.GetString("LOC_GI_CreateGlosSITargetUnexpectedError"),
                process);
        }

        private void RemoveGamesProcess(List<Game> games, GlobalProgressActionArgs progressBar, out int gamesRemoved)
        {
            bool process(Game game) => !string.IsNullOrEmpty(game.Name) && new GameGlosSITarget(game).File.Remove();

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