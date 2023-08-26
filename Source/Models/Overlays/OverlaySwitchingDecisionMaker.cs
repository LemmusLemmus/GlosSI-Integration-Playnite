#define REPLACE_EXISTING_GAME_OVERLAY

using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Threading.Tasks;
using System.Windows;
using GlosSIIntegration.Models.SteamLauncher;
using GlosSIIntegration.Models.Overlays.Types;

namespace GlosSIIntegration.Models.Overlays
{
    internal class OverlaySwitchingDecisionMaker
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
                RunBlocking(OverlaySwitchingCoordinator.Instance.Exit);
            };
            GlosSIIntegration.Instance.ApplicationStartedEvent += (e) =>
            {
                // Start the Playnite overlay, if there is one.
                PlayniteOverlay playniteOverlay = PlayniteOverlay.Create();
                if (playniteOverlay != null)
                {
                    TryStartOverlay(playniteOverlay);
                }
            };
        }

        private static void RunBlocking(Func<Task> function)
        {
            Task.Run(function).GetAwaiter().GetResult();
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
                // TODO: Update wiki https://github.com/LemmusLemmus/GlosSI-Integration-Playnite/wiki/Limitations#running-multiple-games-simultaneously
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
            if (GlosSIIntegration.Instance.IntegrationEnabled)
            {
                RunBlocking(async () =>
                {
                    await TryScheduleSwitchTo(relevantGameOverlay).ConfigureAwait(false);
                    // It is neccessary to wait in order to avoid overlay starting/closing from stealing focus momentarily.
                    // Momentary focus loss would be fine when in Playnite, but not when in-game.
                    await OverlaySwitchingCoordinator.Instance.AwaitTask().ConfigureAwait(false);
                });
            }
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
            ReturnToPlaynite();
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

            if (WasStartedFromSteam(relevantGameOverlay))
            {
                ReturnToSteam();
            }
            else
            {
                ReturnToPlaynite();
            }
        }

        /// <summary>
        /// Checks if an overlay was started from Steam and not this extension.
        /// <para>
        /// WARNING: The overlay must have started before this method is called.
        /// Otherwise old or non-existent data could be used!
        /// </para>
        /// </summary>
        /// <param name="overlay">The overlay to check if it was started from Steam.</param>
        /// <returns>true if the overlay was started from steam; false otherwise
        /// Also returns false if <paramref name="overlay"/> is <c>null</c>.</returns>
        private bool WasStartedFromSteam(Overlay overlay)
        {
            if (overlay == null)
            {
                return false;
            }

            if (relevantGameOverlay != null)
            {
                lock (relevantGameOverlay.stateLock)
                {
                    if (!relevantGameOverlay.State.StartedByExtension)
                    {
                        // Presumably started via Steam.
                        return true;
                    }
                }
            }
            return false;
        }

        private void ReturnToSteam()
        {
            if (Steam.Mode is SteamBigPictureMode mode)
            {
                // Return to Steam Big Picture mode as fast as possible.
                // Makes the transition prettier by showing Playnite momentarily as opposed to for several seconds.
                mode.MainWindow.Show();
            }
            // If the game was started from Steam desktop mode,
            // no need to show the window: Steam normally does not focus it.

            // TODO: Would be better if could prevent Playnite from maximizing in the first place,
            // especially since it also causes the taskbar to flash.
            Application.Current.MainWindow.WindowState = WindowState.Minimized;

            if (relevantGameOverlay != null) // There is nothing to close if there is no game overlay.
            {
                relevantGameOverlay.Dispose();
                relevantGameOverlay = null;
                CloseCurrentOverlay();
            }
        }

        /// <summary>
        /// Switches the overlay in response to the user returning to the Playnite library.
        /// Switches to the Playnite overlay if there is one, otherwise closes any running overlay.
        /// </summary>
        private void ReturnToPlaynite()
        {
            PlayniteOverlay playniteOverlay = PlayniteOverlay.Create();

            if (playniteOverlay != null)
            {
                TryStartOverlay(playniteOverlay);
            }
            else if (relevantGameOverlay != null) // There is nothing to close if there is no game overlay.
            {
                CloseCurrentOverlay();
            }

            if (relevantGameOverlay != null)
            {
                relevantGameOverlay.Dispose();
                relevantGameOverlay = null;
            }
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
                    TryStartOverlay(relevantGameOverlay);
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
                        TryStartOverlay(playniteOverlay);
                    }
                }
            }
            else
            {
                RunBlocking(async () =>
                {
                    await OverlaySwitchingCoordinator.Instance.ScheduleClose(false).ConfigureAwait(false);
                });
            }
        }

        private static void TryStartOverlay(Overlay overlay)
        {
            if (GlosSIIntegration.Instance.IntegrationEnabled)
            {
                RunBlocking(async () =>
                {
                    await TryScheduleSwitchTo(overlay).ConfigureAwait(false);
                });
            }
        }

        private static void CloseCurrentOverlay()
        {
            RunBlocking(async () =>
            {
                await OverlaySwitchingCoordinator.Instance.ScheduleClose(true).ConfigureAwait(false);
            });
        }

        private static async Task TryScheduleSwitchTo(Overlay overlay)
        {
            try
            {
                await OverlaySwitchingCoordinator.Instance.ScheduleSwitchTo(overlay).ConfigureAwait(false);

                if (overlay is GameOverlay)
                {
                    // If a game is running when the overlay starts
                    // (or potentially when Steam focus loss is combated, see SteamBigPictureMode.PreventFocusTheft() for details),
                    // the game may lose focus. Avoid this by ensuring that the game overlay has finished starting.
                    await OverlaySwitchingCoordinator.Instance.AwaitTask().ConfigureAwait(false);
                }
            }
            catch (InvalidOperationException ex)
            {
                logger.Error(ex, "Failed to switch overlay! Closing any existing overlay instead.");
                await OverlaySwitchingCoordinator.Instance.ScheduleClose(true).ConfigureAwait(false);
            }
        }
    }
}
