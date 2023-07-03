#define REPLACE_EXISTING_GAME_OVERLAY

using Playnite.SDK;
using Playnite.SDK.Models;
using System;

namespace GlosSIIntegration.Models
{
    class OverlaySwitchingDecisionMaker
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>
        /// The game overlay that should currently be used, 
        /// irregardless of whether the integration is currently enabled.
        /// </summary>
        private GameOverlay relevantGameOverlay;

        /// <summary>
        /// Instantiates a new <see cref="OverlaySwitchingDecisionMaker"/> that changes the overlay as needed.
        /// <para/>
        /// Multiple instances of <see cref="OverlaySwitchingDecisionMaker"/> should not exist simultaneously.
        /// </summary>
        public OverlaySwitchingDecisionMaker()
        {
            relevantGameOverlay = null;

            GlosSIIntegration.Instance.GameStartingEvent += (e) => GameStarting(e.Game);
            GlosSIIntegration.Instance.GameStartedEvent += (e) => GameStarted(e.Game, e.StartedProcessId);
            GlosSIIntegration.Instance.GameStoppedEvent += (e) => GameStopped(e.Game);
            GlosSIIntegration.Instance.GameStartupCancelledEvent += (e) => GameStartupCancelled(e.Game);
            GlosSIIntegration.Instance.IntegrationToggledEvent += ToggleOverlay;
            GlosSIIntegration.Instance.ApplicationStoppedEvent += (e) => 
            {
                relevantGameOverlay?.Dispose();
                OverlaySwitchingCoordinator.Instance.ScheduleClose();
                OverlaySwitchingCoordinator.Instance.Wait();
            };

            // Start the Playnite overlay, if there is one.
            PlayniteOverlay playniteOverlay = PlayniteOverlay.Create();
            if (playniteOverlay != null)
            {
                TryScheduleSwitchTo(playniteOverlay);
            }
        }

        /// <summary>
        /// Updates the overlay for when a game is starting.
        /// </summary>
        /// <param name="game">The game that is starting.</param>
        private void GameStarting(Game game)
        {
            if (IsGameToBeIgnored(game)) return;

            if (relevantGameOverlay != null)
            {
#if REPLACE_EXISTING_GAME_OVERLAY
                logger.Debug($"Started game \"{game.Name}\" while another game with overlay is running. " +
                    $"Replacing the previous overlay...");
#else
                logger.Debug($"Ignoring game \"{game.Name}\" starting: " +
                    $"There is already a game overlay in use.");
                return;
#endif
                relevantGameOverlay.Dispose();
            }

            // relevantGameOverlay is updated even if the integration is disabled,
            // since it is used by ToggleOverlay(bool).
            relevantGameOverlay = GameOverlay.Create(game);
            TryScheduleSwitchTo(relevantGameOverlay);
        }

        /// <summary>
        /// Checks if the user has tagged the game to be ignored. 
        /// An ignored game should be treated as if it does not exist 
        /// and was never started or closed.
        /// </summary>
        /// <param name="game">The game to check whether it should be ignored.</param>
        /// <returns>True if the game should be ignored; false otherwise.</returns>
        private static bool IsGameToBeIgnored(Game game)
        {
            if (GlosSIIntegration.GameHasIgnoredTag(game))
            {
                logger.Trace($"Ignoring game \"{game.Name}\": the game has the ignored tag.");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Updates the overlay when a game has started.
        /// </summary>
        /// <param name="startedProcessId">The PID of the started game.</param>
        private void GameStarted(Game game, int startedProcessId)
        {
            if (relevantGameOverlay != null && relevantGameOverlay.IsGameSame(game))
            {
                relevantGameOverlay.AttachGameProcess(startedProcessId);
            }
        }

        /// <summary>
        /// Updates the overlay when a game startup is cancelled.
        /// </summary>
        /// <param name="game">The game whose startup has been cancelled.</param>
        private void GameStartupCancelled(Game game)
        {
            if (relevantGameOverlay == null || IsGameToBeIgnored(game)) return;

            // In this case, return the overlay to Playnite (and overwrite relevantGameOverlay).
            // Even if another game is currently running, one can assume that the user has changed
            // their attention away from the game.
            // Either way, such a situation is improbable.
            ReturnOverlayToPlaynite();
        }

        /// <summary>
        /// Updates the overlay for a game that has stopped.
        /// </summary>
        /// <param name="game">The game that has stopped.</param>
        private void GameStopped(Game game)
        {
            if (IsGameToBeIgnored(game)) return;

            if (relevantGameOverlay != null && !relevantGameOverlay.IsGameSame(game))
            {
                logger.Debug($"Ignoring game \"{game.Name}\" stopping: " +
                    $"The game is not the relevant integrated game.");
                return;
            }

            ReturnOverlayToPlaynite();
        }

        /// <summary>
        /// Switches the overlay in response to the user returning to the Playnite library.
        /// Switches to the Playnite overlay if there is one, otherwise closes any running overlay.
        /// </summary>
        private void ReturnOverlayToPlaynite()
        {
            TryScheduleSwitchTo(PlayniteOverlay.Create());
            relevantGameOverlay?.Dispose();
            relevantGameOverlay = null;
        }

        /// <summary>
        /// Toggles the overlay on or off (when the user toggles the overlay/integration toggle button).
        /// </summary>
        /// <param name="toggleOn">The value the toggle button was toggled to.</param>
        private void ToggleOverlay(bool toggleOn)
        {
            if (toggleOn)
            {
                if (relevantGameOverlay != null)
                {
                    logger.Debug("Steam Overlay launched while in-game.");
                    TryScheduleSwitchTo(relevantGameOverlay);
                }
                else
                {
                    // Currently, PlayniteOverlay.Create() will always return null.
                    // This is becuase the Playnite overlay is always null in desktop mode,
                    // which must be the current mode since the overlay/integration toggle button
                    // does not exist in fullscreen mode (due to Playnite limitations).
                    // Playnite might however make TopPanelItems accessible in fullscreen mode
                    // at some point. As such, this merely serves as future-proofing.
                    PlayniteOverlay playniteOverlay = PlayniteOverlay.Create();
                    if (playniteOverlay != null)
                    {
                        TryScheduleSwitchTo(playniteOverlay);
                    }
                }
            }
            else
            {
                OverlaySwitchingCoordinator.Instance.ScheduleClose();
            }
        }

        private static void TryScheduleSwitchTo(Overlay overlay)
        {
            try
            {
                OverlaySwitchingCoordinator.Instance.ScheduleSwitchTo(overlay);
            }
            catch (InvalidOperationException ex)
            {
                logger.Error(ex, "Failed to switch overlay! Closing any existing overlay instead.");
                OverlaySwitchingCoordinator.Instance.ScheduleClose();
            }
        }
    }
}
