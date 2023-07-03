using Playnite.SDK.Models;
using System;
using System.Diagnostics;

namespace GlosSIIntegration.Models
{
    class GameOverlay : Overlay, IDisposable
    {
        private Process runningGameProcess;
        private readonly object runningGameLock = new object();
        private readonly Game associatedGame;
        private bool overlayClosedExternally;

        /// <summary>
        /// Instantiates a new game overlay object.
        /// </summary>
        /// <param name="associatedGame">The game for which this overlay is used.</param>
        /// <param name="overlayName">The name of the overlay to be created. 
        /// Must correspond to a known existing overlay.</param>
        /// <exception cref="InvalidOperationException">If the path to GlosSI has not been set.</exception>
        protected GameOverlay(Game associatedGame, string overlayName) : base(overlayName)
        {
            overlayClosedExternally = true; // Assumed to be true until proven otherwise.
            runningGameProcess = null;
            this.associatedGame = associatedGame;
        }

        /// <summary>
        /// Creates the overlay associated with a game, if there is one.
        /// Note: This method does not check if the game has the ignored tag.
        /// </summary>
        /// <param name="game">The game to create the overlay for.</param>
        /// <returns>If the game should have an overlay, the overlay; <c>null</c> otherwise.</returns>
        public static GameOverlay Create(Game game)
        {
            try
            {
                if (GlosSIIntegration.GameHasIntegratedTag(game))
                {
                    return new GameOverlay(game, game.Name);
                }
                else if (GlosSIIntegration.GetSettings().UseDefaultOverlay
                    && !GlosSIIntegration.IsSteamGame(game))
                {
                    return new GameOverlay(game, GlosSIIntegration.GetSettings().DefaultOverlayName);
                }
            }
            catch (InvalidOperationException ex)
            {
                logger.Warn($"Cannot create game overlay: {ex.Message}");
            }

            return null;
        }

        public bool IsGameSame(Game game)
        {
            if (game == null) return false;

            return game.Equals(associatedGame);
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

        protected internal override void BeforeOverlayClosed()
        {
            overlayClosedExternally = false;
        }

        protected internal override void OnOverlayClosed(int overlayExitCode)
        {
            // Note that runningGameProcess is not disposed here.
            // This is because the overlay may be reused for the same game.

            try
            {
                if (!overlayClosedExternally)
                {
                    logger.Trace("Overlay was closed by the extension.");
                    return;
                }

                lock (runningGameLock)
                {
                    if (runningGameProcess == null)
                    {
                        logger.Warn("Game overlay has no attached game process.");
                        return;
                    }

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
                    if (GlosSIIntegration.GetSettings().CloseGameWhenOverlayIsClosed)
                    {
                        KillGame();
                    }
                }
            }
            finally
            {
                overlayClosedExternally = true; // Reset
                base.OnOverlayClosed(overlayExitCode);
            }
        }

        private void KillGame()
        {
            try
            {
                logger.Trace("Attempting to kill game...");
                // TODO: Might want to close the game more gracefully? At least as an alternative?
                runningGameProcess.Kill();
                logger.Debug("Killed game in retaliation for GlosSI being killed.");
            }
            catch (InvalidOperationException ex)
            {
                logger.Debug($"Game closed before GlosSITarget, not doing anything: {ex.Message}.");
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
    }
}
