using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models
{
    sealed partial class OverlaySwitchingCoordinator
    {
        /// <summary>
        /// Encapsulates static methods relating to a GlosSITarget process.
        /// <para>
        /// Note that only one GlosSITarget process can run at a time, at least for any length of time.
        /// </para>
        /// Only <see cref="OverlaySwitchingCoordinator"/> is allowed to access this class.
        /// Other classes should not be messing around with the GlosSITarget process
        /// unless explicitly permitted to do so.
        /// </summary>
        private static class GlosSITargetProcess
        {
            /// <summary>
            /// Waits for GlosSITarget to start and returns the found process.
            /// </summary>
            /// <param name="timeout">The approximate timeout in milliseconds.</param>
            /// <returns>The found GlosSITarget process.</returns>
            /// <exception cref="TimeoutException">If GlosSITarget did not start after 
            /// <paramref name="timeout"/> milliseconds.</exception>
            public static async Task<Process> WaitForProcessToStart(int timeout = 300000)
            {
                // TODO: Could be made non-polling by using the ManagementEventWatcher class.

                const int pollingDelay = 333;
                int sleptTime = 0;
                Process foundProcess;

                logger.Trace("Waiting for GlosSITarget to start...");

                while ((foundProcess = GetRunning()) == null)
                {
                    await Task.Delay(pollingDelay).ConfigureAwait(false);
                    if ((sleptTime += pollingDelay) > timeout)
                    {
                        throw new TimeoutException("GlosSITarget did not start in time.");
                    }
                }

                logger.Trace("GlosSITarget started.");

                return foundProcess;
            }

            /// <summary>
            /// Waits for the GlosSITarget window to start.
            /// </summary>
            /// <param name="timeout">The approximate timeout in milliseconds.</param>
            /// <exception cref="TimeoutException">If the window did not start after 
            /// <paramref name="timeout"/> milliseconds.</exception>
            public static async Task WaitForWindowToStart(int timeout = 5000)
            {
                const int pollingDelay = 50;
                int sleptTime = 0;

                logger.Trace("Waiting for GlosSITarget window to open...");

                while (ProcessExtensions.FindWindow("GlosSITarget") == IntPtr.Zero)
                {
                    await Task.Delay(pollingDelay).ConfigureAwait(false);
                    if ((sleptTime += pollingDelay) > timeout)
                    {
                        throw new TimeoutException("GlosSITarget window did not open in time.");
                    }
                }

                logger.Trace("GlosSITarget window opened.");
            }

            /// <summary>
            /// Checks if starting a new GlosSITarget process would automatically 
            /// replace (i.e. close) any old process.
            /// If not, starting a new GlosSITarget process while another
            /// GlosSITarget process is running will simply result in the started 
            /// GlosSITarget process closing itself.
            /// This depends the installed GlosSI version.
            /// </summary>
            /// <returns>True if starting a GlosSITarget process would automatically 
            /// replace any currently running GlosSITarget process; false otherwise.</returns>
            public static bool DoesNewReplaceOld()
            {
                // No need to close any already running GlosSITarget process, if
                // launching GlosSITarget version >= v0.1.2.0,
                // since it should close any already running GlosSITarget process automatically.
                return GlosSIIntegration.GetSettings().GlosSIVersion >= new Version("0.1.2.0");
            }

            /// <summary>
            /// Starts closing of any currently running GlosSITarget process. 
            /// If no process to close was found, simply logs a warning.
            /// <para>
            /// Note that GlosSITarget's window must have started before this method is called 
            /// and that the process is not necessarily closed yet when the method returns.
            /// </para>
            /// </summary>
            public static void Close()
            {
                try
                {
                    // The below method is used instead of Process.CloseMainWindow()
                    // because GlosSITarget is not guaranteed to have a main window.
                    // GlosSITarget's window must have started by now, should be ensured by WaitForWindowToStart().
                    ProcessExtensions.CloseProcessByName("GlosSITarget");
                }
                catch (InvalidOperationException ex)
                {
                    logger.Warn(ex.Message);
                }
            }

            /// <summary>
            /// Gets any currently running GlosSITarget process.
            /// </summary>
            /// <returns>The currently running GlosSITarget process, 
            /// or <c>null</c> if GlosSITarget is not currently running.</returns>
            public static Process GetRunning()
            {
                return ExtractSingleProcessFromArray(Process.GetProcessesByName("GlosSITarget"));
            }

            /// <summary>
            /// Extracts the first process from an array of processes. 
            /// Any additional processes are disposed and a warning is logged.
            /// </summary>
            /// <param name="processes">The array of processes to pick the first from.</param>
            /// <returns>A process from the array.</returns>
            private static Process ExtractSingleProcessFromArray(Process[] processes)
            {
                if (processes.Length == 0)
                {
                    return null;
                }

                if (processes.Length > 1)
                {
                    logger.Warn($"Multiple ({processes.Length}) processes were found.");
                    for (int i = 1; i < processes.Length; i++) processes[i].Dispose();
                }

                return processes[0];
            }
        }
    }
}
