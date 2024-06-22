using GlosSIIntegration.Models.GlosSITargets.Types;
using Playnite.SDK.Models;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models.Overlays.Types
{
    internal class GameOverlay : SteamStartableOverlay, IDisposable
    {
        private Process runningGameProcess;
        private readonly object runningGameLock = new object();
        private readonly bool closeGameWhenOverlayClosed;
        public Game AssociatedGame { get; }

        /// <summary>
        /// Creates an overlay object for the game, even if the game should not actually have one.
        /// </summary>
        /// <param name="associatedGame">The game for which this overlay is used.</param>
        /// <exception cref="InvalidOperationException">If the path to GlosSI has not been set.</exception>
        protected GameOverlay(Game associatedGame) : this(associatedGame, new GameGlosSITarget(associatedGame)) { }

        /// <summary>
        /// Creates an overlay object for the game, even if the game should not actually have one.
        /// </summary>
        /// <param name="associatedGame">The game for which this overlay is used.</param>
        /// <param name="target">The <see cref="GlosSITarget"/> associated with the game.</param>
        /// <exception cref="InvalidOperationException">If the path to GlosSI has not been set.</exception>
        protected GameOverlay(Game associatedGame, GlosSITarget target) : base(target)
        {
            runningGameProcess = null;
            AssociatedGame = associatedGame;
            closeGameWhenOverlayClosed = GlosSIIntegration.GetSettings().CloseGameWhenOverlayIsClosed;
        }

        /// <summary>
        /// Creates the overlay associated with a game, if there is one.
        /// Note: This method does not check if the game has the ignored tag.
        /// </summary>
        /// <param name="game">The game to create the overlay for.</param>
        /// <returns>The overlay if the game should have an overlay; <c>null</c> otherwise.</returns>
        public static GameOverlay Create(Game game)
        {
            try
            {
                if (GlosSIIntegration.GameHasIntegratedTag(game))
                {
                    return new GameOverlay(game);
                }
                else if (GlosSIIntegration.GetSettings().UseDefaultOverlay
                    && !GlosSIIntegration.IsSteamGame(game))
                {
                    return new DefaultGameOverlay(game);
                }
            }
            catch (InvalidOperationException ex)
            {
                logger.Warn($"Cannot create game overlay: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Checks if <paramref name="game"/> references the same game as <see cref="AssociatedGame"/>.
        /// </summary>
        /// <param name="game">The game to compare.</param>
        /// <returns>true if the same game is referenced; false otherwise.</returns>
        public bool IsGameSame(Game game) // TODO: Remove and instead just use AssociatedGame.Equals() wherever needed?
        {
            if (game == null) return false;

            return game.Equals(AssociatedGame);
        }

        /// <summary>
        /// Associates a game process with this overlay.
        /// If this overlay is closed by the user, the attached game process will also be closed.
        /// </summary>
        /// <param name="gamePid">The PID of the game process.</param>
        public void AttachGameProcess(int gamePid)
        {
            lock (runningGameLock)
            {
                if (runningGameProcess != null)
                {
                    logger.Error("Attempted to set game overlay game process twice.");
                    return;
                }

                try
                {
                    runningGameProcess = Process.GetProcessById(gamePid);
                }
                catch (ArgumentException)
                {
                    logger.Warn($"{gamePid} is not a valid game PID to track");
                }
                catch (InvalidOperationException ex)
                {
                    logger.Error(ex, "Failed to get game process by id.");
                }
            }
        }

        protected override async Task OnClosedCalled(int overlayExitCode)
        {
            Task baseTask = base.OnClosedCalled(overlayExitCode);

            try
            {
                // Note that runningGameProcess is not disposed here.
                // This is because the overlay may be reused for the same game.

                lock (runningGameLock)
                {
                    // Trivia: The extension used to check the exit code of GlosSITarget
                    // to avoid killing the game when the user closed the GlosSITarget
                    // process normally (i.e. not via Steam).
                    // However, Steam appears to have changed the way it closes games,
                    // as such the exit code can no longer be relied on.
                    // Before one could assume an exit code of 1 meant that the game
                    // was closed via the Steam overlay, 
                    // unless something went wrong or the process was killed by other means.
                    //
                    // TODO: Add a new method of checking if GlosSITarget was closed via
                    // the Steam overlay or not.
                    if (closeGameWhenOverlayClosed &&
                        // Check that the overlay was closed from the overlay/externally,
                        // and not via the extension.
                        !State.ClosedByExtension &&
                        // If the game has already closed, there is no need to close it.
                        !runningGameProcess.HasExitedSafe() &&
                        // If a GlosSITarget is running, the overlay was probably replaced by another
                        // GlosSITarget process, and closing the game would be unexpected.
                        !GlosSITargetProcess.IsRunning())
                    {
                        if (runningGameProcess == null)
                        {
                            logger.Warn("Game overlay has no attached game process.");
                            return;
                        }
                        KillGame();
                    }
                }
            }
            finally
            {
                await baseTask.ConfigureAwait(false);
            }
        }

        private void KillGame()
        {
            try
            {
                // TODO: Might want to close the game more gracefully? At least as an alternative?
                // TODO: This does not work for all games, as the PID reported might not be the actual game exe, but rather an auxillary exe.
                // Some kind of heuristic is needed.
                logger.Debug($"Killing game (with PID {runningGameProcess.Id}) in retaliation for GlosSI being killed.");
                runningGameProcess.Kill();
            }
            catch (InvalidOperationException ex)
            {
                logger.Warn($"Game closed before GlosSITarget, not doing anything: {ex.Message}.");
            }
            catch (Exception ex)
            when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
            {
                logger.Error(ex, "Killing game failed:");
            }
            finally
            {
                runningGameProcess.Dispose();
                runningGameProcess = null;
            }
        }

        /// <summary>
        /// Disposes the game process held by this object, if any.
        /// This method can safely be called at any time: the object remains usable afterwards: 
        /// it simply becomes unable to close the currently running game when the overlay is closed.
        /// </summary>
        public void Dispose()
        {
            lock (runningGameLock)
            {
                if (runningGameProcess != null)
                {
                    runningGameProcess.Dispose();
                    runningGameProcess = null;
                }
            }
        }

        protected override void OnStartedCalled() { }

        protected override void BeforeClosedCalled() { }
    }
}
