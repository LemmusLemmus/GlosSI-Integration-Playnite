using GlosSIIntegration.Models.Overlays.Types;
using Playnite.SDK;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models.Overlays
{
    /// <summary>
    /// Singleton class responsible for coordinating overlay switching.
    /// </summary>
    sealed internal class OverlaySwitchingCoordinator
    {
        /// <summary>
        /// Used to synchronize access to <see cref="currentOverlay"/>. 
        /// A simple lock could have been used, but using a semaphore makes things easier by not having to actively avoid awaits.
        /// </summary>
        private readonly SemaphoreSlim currentOverlaySemaphore;
        private Overlay currentOverlay;
        private volatile Task currentSwitchingTask;
        private volatile Task processWaitingTask; // TODO: Double check that this is thread safe!
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly OverlaySwitchingCoordinator instance;
        public static OverlaySwitchingCoordinator Instance { get { return instance; } }

        static OverlaySwitchingCoordinator()
        {
            instance = new OverlaySwitchingCoordinator();
        }

        private OverlaySwitchingCoordinator()
        {
            currentOverlay = null;
            currentOverlaySemaphore = new SemaphoreSlim(1, 1);
            processWaitingTask = Task.CompletedTask;
            currentSwitchingTask = Task.CompletedTask;
        }

        /// <summary>
        /// Waits for issued overlay commands to complete (be it closing or switching to an overlay) 
        /// and closes any running overlay.
        /// This additionally ensures disposal of objects used to perform those actions.
        /// </summary>
        public async Task Exit()
        {
            await Instance.ScheduleClose(false).ConfigureAwait(false);
            await AwaitTask().ConfigureAwait(false);
            await processWaitingTask.ConfigureAwait(false);
            logger.Trace("Everything closed.");
        }

        // Importantly, ensures that no focus loss will occur afterwards.
        public async Task AwaitTask()
        {
            await currentSwitchingTask.ConfigureAwait(false);
        }

        /// <summary>
        /// Schedules switching of the currently active overlay to the provided overlay.
        /// If the provided overlay is <c>null</c>, the current overlay is simply closed via <see cref="ScheduleClose"/>.
        /// If the overlay is currently being switched or closed, waits until that finishes first.
        /// </summary>
        /// <param name="overlay">The new overlay to switch to, or <c>null</c> to close the overlay.</param>
        /// <exception cref="InvalidOperationException">If the overlay cannot be run.</exception>
        public async Task ScheduleSwitchTo(Overlay overlay)
        {
            if (overlay == null)
            {
                await ScheduleClose(true).ConfigureAwait(false);
            }
            else
            {
                // Verification is done here so that the caller of this
                // method can quickly and easily handle the exception if thrown.
                overlay.Target.VerifyRunnable();
                logger.Trace("Scheduled switching overlay");
                await currentSwitchingTask.ConfigureAwait(false);
                currentSwitchingTask = SwitchOverlay(overlay);
            }
        }

        /// <summary>
        /// Schedules the closing of any currently running overlay (started by this class).
        /// If the overlay is currently being switched or closed, waits until that finishes first.
        /// </summary>
        /// <param name="closeAny">If true, will close any currently running GlosSITarget process, even if it is unrelated to this extension.
        /// If false, only closes those currently handled by the extension.</param>
        public async Task ScheduleClose(bool closeAny)
        {
            logger.Trace("Scheduled closing overlay");
            await currentSwitchingTask.ConfigureAwait(false);
            currentSwitchingTask = CloseCurrentOverlay(closeAny);
        }

        /// <summary>
        /// Starts a new overlay process, closing any already running process.
        /// Ensures that <see cref="processWaitingTask"/> completes and that the previous process (if any) has closed.
        /// <para>
        /// Note: While this method is called, the <see cref="processWaitingTask"/> variable may not be changed.
        /// </para>
        /// Note: Neither <see cref="Overlay.BeforeClosed"/> nor <see cref="Overlay.OnStarted"/> is called
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
                closeOrReplaceOverlay = overlay.Target.Run;
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
                ExternallyStartedOverlay externalOverlay = await ExternallyStartedOverlay.GetCurrent().ConfigureAwait(false);
                if (externalOverlay == null)
                {
                    if (overlayAutomaticallyReplaced)
                    {
                        overlay.Target.Run();
                    }
                }
                else
                {
                    await CloseExternallyStartedOverlay(externalOverlay, closeOrReplaceOverlay).ConfigureAwait(false);
                }
            }

            if (!overlayAutomaticallyReplaced)
            {
                overlay.Target.Run();
            }

            // The wrong process could be retrieved if GlosSITarget happens to
            // be started now from outside this extension. Very unlikely though.
            return await GlosSITargetProcess.WaitForProcessToStart().ConfigureAwait(false);
        }

        private async Task CloseExternallyStartedOverlay(ExternallyStartedOverlay externalOverlay, Action closingAction)
        {
            externalOverlay.BeforeClosed();
            Task<int> waitForExitTask = externalOverlay.WaitForExit();
            closingAction();
            await externalOverlay.OnClosed(await waitForExitTask.ConfigureAwait(false)).ConfigureAwait(false);
        }

        /// <summary>
        /// Starts a new overlay and updates <see cref="currentOverlay"/> and <see cref="processWaitingTask"/>.
        /// <para>
        /// <see cref="processWaitingTask"/> will have completed after this method is done. 
        /// This task may only run one at a time.
        /// </para>
        /// </summary>
        /// <param name="overlay">The overlay to start.</param>
        private async Task StartOverlay(Overlay overlay)
        {
            await currentOverlaySemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                currentOverlay?.BeforeClosed();
            }
            finally
            {
                currentOverlaySemaphore.Release();
            }

            logger.Trace("await overlay.BeforeOverlayStarted()");
            await overlay.BeforeStarted().ConfigureAwait(false);
            logger.Trace("done await overlay.BeforeOverlayStarted()");
            logger.Trace("await StartNewOverlayProcess(overlay)");
            Process newProcess = await StartNewOverlayProcess(overlay).ConfigureAwait(false);
            logger.Trace("done await StartNewOverlayProcess(overlay)");

            Task<int> waitForExitAsync = newProcess.WaitForExitAsyncSafe();

            try
            {
                logger.Trace("await GlosSITargetProcess.WaitForWindowToStart()");
                await GlosSITargetProcess.WaitForWindowToStart().ConfigureAwait(false);
                logger.Trace("done await GlosSITargetProcess.WaitForWindowToStart()");
            }
            catch (TimeoutException ex)
            {
                logger.Warn(ex.Message);
            }

            // Update current overlay.
            await currentOverlaySemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                logger.Trace("OnOverlayStarted");
                overlay.OnStarted(newProcess);
                logger.Trace("done OnOverlayStarted");
                if (currentOverlay != null)
                {
                    logger.Error("Something went wrong when managing the overlay: " +
                        "currentOverlay was somehow not null.");
                }
                currentOverlay = overlay;

                StartProcessWaitingTask(waitForExitAsync);
            }
            finally
            {
                currentOverlaySemaphore.Release();
            }
        }

        /// <summary>
        /// Awaits the <paramref name="waitForExitAsyncTask"/> and handles the exit of the overlay 
        /// (by calling <see cref="Overlay.OnClosed(int)"/> and setting <see cref="currentOverlay"/> to <c>null</c>).
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
                await currentOverlaySemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    await currentOverlay.OnClosed(processExitCode).ConfigureAwait(false);
                    currentOverlay = null;
                }
                finally
                {
                    currentOverlaySemaphore.Release();
                }
                logger.Trace("Done waiting");
            }
        }

        // Must be called after currentOverlay has been set and OnOverlayStarted has been called
        // (since the processWaitingTask will sooner or later reset the currentOverlay and call OnOverlayClosed)
        private void StartProcessWaitingTask(Task<int> waitForExitAsyncTask)
        {
            async Task StartWaiting(Task<int> waitForExit)
            {
                try
                {
                    await WaitForOverlayToClose(waitForExit).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Since the processWaitingTask is not necessarily awaited
                    // (or rather, not timely awaited)
                    // and since a failure in waiting should not, for example,
                    // make starting a new overlay fail, catch and log any potential exception here.
                    logger.Error(ex, "Something went wrong when waiting for the overlay to close:");
                }
            }
            processWaitingTask = StartWaiting(waitForExitAsyncTask);
        }

        /// <summary>
        /// Attempts to switch to the provided overlay.
        /// <para>This task may only be run one at a time.</para>
        /// </summary>
        /// <param name="overlay">The overlay to switch to.</param>
        private async Task SwitchOverlay(Overlay overlay)
        {
            logger.Trace("Switching overlay");

            if (await TryReplaceOverlay(overlay).ConfigureAwait(false))
            {
                // The overlay was simply replaced.
                logger.Trace("Overlay was simply replaced!");
                return;
            }
            logger.Trace("starting overlay");
            await StartOverlay(overlay).ConfigureAwait(false);
            logger.Trace("done starting overlay");

            logger.Trace("Done switching overlay!");
        }

        /// <summary>
        /// Closes the current overlay and waits for it to close.
        /// </summary>
        /// <param name="closeAny">If true, will close any currently running GlosSITarget process, 
        /// even if it is unrelated to this extension.
        /// If false, only closes those currently handled by the extension.</param>
        private async Task CloseCurrentOverlay(bool closeAny)
        {
            // Wait for overlay to actually close.
            // Prevents, for example, replacing of an overlay currently being closed.
            Task waitingTask = null;

            await currentOverlaySemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (currentOverlay != null)
                {
                    currentOverlay.BeforeClosed();
                    logger.Debug("Closing GlosSITarget.");
                    GlosSITargetProcess.Close();
                    waitingTask = processWaitingTask;
                }
                else if (closeAny)
                {
                    // If any other GlosSITargetProcess is running, close it.
                    // This is because there might be a running overlay started from Steam that has yet to be recognized.
                    ExternallyStartedOverlay externalOverlay = await ExternallyStartedOverlay.GetCurrent().ConfigureAwait(false);
                    if (externalOverlay == null)
                    {
                        return;
                    }

                    logger.Debug("Closing externally started overlay.");
                    await CloseExternallyStartedOverlay(externalOverlay, GlosSITargetProcess.Close).ConfigureAwait(false);
                    logger.Trace("Externally started overlay was closed.");
                }
            }
            finally
            {
                currentOverlaySemaphore.Release();
            }

            if (waitingTask != null) // TODO: Could be made prettier.
            {
                logger.Trace("Waiting for overlay to close...");
                await waitingTask.ConfigureAwait(false);
                logger.Trace("Done waiting for overlay to close.");
            }
        }

        /// <summary>
        /// Attempts to replace the <see cref="currentOverlay"/> if 
        /// the <paramref name="newOverlay"/> can reasonably replace it.
        /// </summary>
        /// <param name="newOverlay">The new overlay.</param>
        /// <returns>True if the overlay was replaced; false otherwise.</returns>
        private async Task<bool> TryReplaceOverlay(Overlay newOverlay)
        {
            await currentOverlaySemaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                if (currentOverlay == newOverlay)
                {
                    // If this were to happen, it is probably a problem with the code.
                    logger.Warn("Attempted to switch to the same overlay.");
                    return true; // The overlay has been "replaced" by simply not touching it.
                }
                else if (currentOverlay != null)
                {
                    if (!currentOverlay.StartsSameShortcutAs(newOverlay))
                    {
                        return false;
                    }

                    currentOverlay = newOverlay.Replaces(currentOverlay);
                    logger.Debug("Overlay replaced.");
                    return true;
                }

                // Current overlay is null.
                // Check if a currently running externally started GlosSITarget process can be replaced.
                using (ExternallyStartedOverlay externalOverlay =
                    await ExternallyStartedOverlay.GetCurrent().ConfigureAwait(false))
                {
                    if (externalOverlay == null || !externalOverlay.StartsSameShortcutAs(newOverlay))
                    {
                        // TODO: Return the externalOverlay so that it may be reused later?
                        return false;
                    }

                    StartProcessWaitingTask(externalOverlay.WaitForExit());
                    currentOverlay = newOverlay.Replaces(externalOverlay);
                    logger.Trace("Replaced externally started running overlay.");
                    return true;
                }
            }
            finally
            {
                currentOverlaySemaphore.Release();
            }
        }
    }
}
