using Playnite.SDK;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models
{
    /// <summary>
    /// Singleton class responsible for coordinating overlay switching.
    /// </summary>
    sealed partial class OverlaySwitchingCoordinator
    {
        /// <summary>
        /// Object used to lock access to <see cref="currentOverlay"/>.
        /// </summary>
        private readonly object currentOverlayLock;
        private Overlay currentOverlay;
        private volatile Task processWaitingTask;
        private readonly OverwritingTaskStandbyer taskStandbyer;
        private static readonly Action<Exception> taskExceptionHandler;
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly OverlaySwitchingCoordinator instance;
        public static OverlaySwitchingCoordinator Instance { get { return instance; } }

        static OverlaySwitchingCoordinator()
        {
            instance = new OverlaySwitchingCoordinator();
            // TODO: Send error as notification as well?
            taskExceptionHandler = ex => logger.Error($"Something went wrong when managing the overlay:\n{ex}");
        }

        private OverlaySwitchingCoordinator()
        {
            currentOverlay = null;
            currentOverlayLock = new object();
            processWaitingTask = Task.CompletedTask;
            taskStandbyer = new OverwritingTaskStandbyer(taskExceptionHandler, false);
        }

        /// <summary>
        /// Waits for all (if any) issued overlay commands to complete (be it closing or switching to an overlay).
        /// This additionally ensures disposal of objects used to perform those actions.
        /// </summary>
        public void Wait()
        {
            taskStandbyer.WaitOnTasks();
            processWaitingTask?.Wait();
            logger.Trace("Closed everything.");
        }

        /// <summary>
        /// Schedules switching of the currently active overlay to the provided overlay.
        /// If the provided overlay is <c>null</c>, the current overlay is simply closed via <see cref="ScheduleClose"/>.
        /// <para>
        /// The method returns immediately if <see cref="GlosSIIntegration.integrationEnabled"/> is false.
        /// </para>
        /// </summary>
        /// <param name="overlay">The new overlay to switch to, or <c>null</c> to close the overlay.</param>
        /// <exception cref="InvalidOperationException">If the overlay cannot be run.</exception>
        public void ScheduleSwitchTo(Overlay overlay)
        {
            if (!GlosSIIntegration.Instance.IntegrationEnabled) return;

            if (overlay == null)
            {
                ScheduleClose();
            }
            else
            {
                // Verification is done here so that the caller of this
                // method can quickly and easily handle the exception if thrown.
                overlay.SteamShortcut.VerifyRunnable();
                logger.Trace("Scheduled switching overlay");
                taskStandbyer.StartNewTask(() => SwitchOverlay(overlay));
            }
        }

        /// <summary>
        /// Schedules the closing of any currently running overlay.
        /// </summary>
        public void ScheduleClose()
        {
            logger.Trace("Scheduled closing overlay");
            taskStandbyer.StartNewTask(CloseCurrentOverlay);
        }

        /// <summary>
        /// Starts a new overlay process, closing any already running process.
        /// Ensures that <see cref="processWaitingTask"/> completes and that the previous process (if any) has closed.
        /// <para>
        /// Note: While this method is called, the <see cref="processWaitingTask"/> variable may not be changed.
        /// </para>
        /// Note: Neither <see cref="Overlay.BeforeOverlayClosed"/> nor <see cref="Overlay.OnOverlayStarted"/> is called
        /// by this method.
        /// </summary>
        /// <param name="overlay">The overlay to start.</param>
        /// <returns>The started process.</returns>
        private async Task<Process> StartNewOverlayProcess(Overlay overlay)
        {
            Action closeOrReplaceOverlay;
            bool overlayAutomaticallyReplaced = GlosSITargetProcess.DoesNewReplaceOld();

            if (overlayAutomaticallyReplaced)
            {
                closeOrReplaceOverlay = overlay.SteamShortcut.Run;
            }
            else
            {
                closeOrReplaceOverlay = GlosSITargetProcess.Close;
            }

            logger.Trace("Starting new GlosSITarget process");
            
            if (!processWaitingTask.IsCompleted)
            {
                // Since the currently active overlay is the one being waited on,
                // just wait on processWaitingTask.
                // Assumes that no overlay is currently replacing the existing one.
                closeOrReplaceOverlay();
                await processWaitingTask.ConfigureAwait(false);
            }
            else
            {
                // Find any running GlosSITarget process not started by this extension.
                // This is to avoid some polling and importantly wait for it to exit
                // as to not confuse it with the soon-to-be started overlay.
                // Confusion can occur because the GlosSITarget process is started via a Steam URL,
                // and as such a process handle/identifer must be actively searched for and found.
                using (Process alreadyRunningProcess = GlosSITargetProcess.GetRunning())
                {
                    Task<int> waitForExitTask = alreadyRunningProcess?.WaitForExitAsyncSafe();
                    closeOrReplaceOverlay();
                    if (waitForExitTask != null)
                    {
                        await waitForExitTask.ConfigureAwait(false);
                    }
                }
            }

            if (!overlayAutomaticallyReplaced)
            {
                overlay.SteamShortcut.Run();
            }

            // The wrong process could be retrieved if GlosSITarget happens to
            // be started now from outside this extension. Very unlikely though.
            return await GlosSITargetProcess.WaitForProcessToStart().ConfigureAwait(false);
        }

        /// <summary>
        /// Starts a new overlay and updates <see cref="currentOverlay"/>.
        /// <para>
        /// <see cref="processWaitingTask"/> will have completed after this method is done. 
        /// This task may only run one at a time.
        /// </para>
        /// </summary>
        /// <param name="overlay">The overlay to start.</param>
        /// <returns>A task that waits for the started overlay process to exit.</returns>
        private async Task<Task<int>> StartOverlay(Overlay overlay)
        {
            lock (currentOverlayLock)
            {
                currentOverlay?.BeforeOverlayClosed();
            }

            Process newProcess = await StartNewOverlayProcess(overlay).ConfigureAwait(false);

            // Start waitForExitAsync as soon as possible.
            Task<int> waitForExitTask = newProcess.WaitForExitAsyncSafe();

            try
            {
                await GlosSITargetProcess.WaitForWindowToStart().ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                logger.Warn(ex.Message);
            }

            // Update current process.
            lock (currentOverlayLock)
            {
                overlay.OnOverlayStarted(newProcess);
                if (currentOverlay != null)
                {
                    logger.Error("Something went wrong when managing the overlay: " +
                        "currentOverlay was somehow not null.");
                }
                currentOverlay = overlay;
            }

            return waitForExitTask;
        }

        /// <summary>
        /// Awaits the <paramref name="waitForExitAsyncTask"/> and handles the exit of the overlay 
        /// (by calling <see cref="Overlay.OnOverlayClosed(int)"/> and setting <see cref="currentOverlay"/> to <c>null</c>).
        /// <para>This task may only be run one at a time.</para>
        /// </summary>
        /// <param name="waitForExitAsyncTask">A task that waits for the overlay process to exit.</param>
        private async Task WaitForOverlayToClose(Task<int> waitForExitAsyncTask)
        {
            // Assume exit code 0 in the unlikely event of waitForExitAsyncTask throwing an exception.
            int processExitCode = 0;

            try
            {
                logger.Trace("Waiting for overlay to close...");
                processExitCode = await waitForExitAsyncTask.ConfigureAwait(false);
                logger.Trace($"Overlay closed with exit code {processExitCode}");
            }
            finally
            {
                lock (currentOverlayLock)
                {
                    currentOverlay.OnOverlayClosed(processExitCode);
                    currentOverlay = null;
                }
                logger.Trace("Done waiting");
            }
        }

        /// <summary>
        /// Attempts to switch to the provided overlay.
        /// <para>This task may only be run one at a time.</para>
        /// </summary>
        /// <param name="overlay">The overlay to switch to.</param>
        private async Task SwitchOverlay(Overlay overlay)
        {
            logger.Trace("Switching overlay");

            if (ReplaceOverlay(overlay))
            {
                // The overlay was simply replaced.
                return;
            }

            Task<int> waitForExitTask = await StartOverlay(overlay).ConfigureAwait(false);

            processWaitingTask = Task.Run(async () =>
            {
                try
                {
                    await WaitForOverlayToClose(waitForExitTask);
                }
                // Since the processWaitingTask is not necessarily awaited
                // (or rather, not timely awaited)
                // and since a failure in waiting should not make, for example,
                // starting a new overlay fail, catch and log any potential exception here.
                catch (Exception ex)
                {
                    logger.Error(ex, "Something went wrong when waiting for the overlay to close:");
                }
            });
        }

        /// <summary>
        /// Closes the current overlay and waits for it to close.
        /// </summary>
        private async Task CloseCurrentOverlay()
        {
            lock (currentOverlayLock)
            {
                if (currentOverlay != null)
                {
                    currentOverlay.BeforeOverlayClosed();
                    logger.Debug("Closing GlosSITarget.");
                    GlosSITargetProcess.Close();
                }
                else
                {
                    logger.Trace("Cannot close overlay: current overlay is null.");
                }
            }

            // Wait for overlay to actually close.
            // Prevents, for example, replacing of an overlay currently being closed.
            logger.Trace("Waiting for overlay to close...");
            await processWaitingTask.ConfigureAwait(false);
            logger.Trace("Done waiting for overlay to close.");
        }

        /// <summary>
        /// Attempts to replace the <see cref="currentOverlay"/> if 
        /// the <paramref name="overlay"/> can reasonably replace it.
        /// </summary>
        /// <param name="overlay">The new overlay.</param>
        /// <returns>True if the overlay was replaced; false otherwise.</returns>
        private bool ReplaceOverlay(Overlay overlay)
        {
            lock (currentOverlayLock)
            {
                if (currentOverlay == overlay)
                {
                    // If this were to happen, it is probably a problem with the code.
                    logger.Warn("Attempted to switch to the same overlay.");
                    return true; // The overlay has been "replaced" by simply not touching it.
                }
                else if (currentOverlay != null && currentOverlay.StartsSameShortcutAs(overlay))
                {
                    currentOverlay = overlay.Replaces(currentOverlay);
                    logger.Debug("Overlay replaced.");
                    return true;
                }
                return false;
            }
        }
    }
}
