using Playnite.SDK;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models.Overlays
{
    /// <summary>
    /// Encapsulates static methods relating to a GlosSITarget process.
    /// <para>
    /// Note that only one GlosSITarget process can run at a time, at least for any length of time.
    /// </para>
    /// </summary>
    internal static class GlosSITargetProcess // TODO: Make instantiable! Also inherit from process?
    {
        private const string WindowClassName = "SFML_Window";
        private const string WindowName = "GlosSITarget";
        private const string ProcessName = WindowName;
        private static readonly ILogger logger = LogManager.GetLogger();

        /// <summary>
        /// Waits for GlosSITarget to start and returns the found process.
        /// </summary>
        /// <param name="timeout">The approximate timeout in milliseconds.</param>
        /// <returns>The found GlosSITarget process.</returns>
        /// <exception cref="TimeoutException">If GlosSITarget did not start after 
        /// <paramref name="timeout"/> milliseconds.</exception>
        public static async Task<Process> WaitForProcessToStart(int timeout = 30000)
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
                    const string errorMsg = "GlosSITarget did not start in time. " +
                        "Ensure that the specified path to GlosSITarget is correct and has not been changed. " +
                        "Also make sure that the Steam shortcut has not been renamed.";
                    logger.Error(errorMsg);
                    throw new TimeoutException(errorMsg);
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

            while (WinWindow.Find(WindowClassName, "GlosSITarget") == null)
            {
                await Task.Delay(pollingDelay).ConfigureAwait(false);
                if ((sleptTime += pollingDelay) > timeout)
                {
                    throw new TimeoutException("GlosSITarget window did not open in time.");
                }
            }

            logger.Trace("GlosSITarget window opened.");
        }

        // TODO: Make static property. Or update the version requirement and remove this check?
        // GetSettings().GlosSIVersion is not exactly thread-safe, but it is unlikely to ever change...
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

        public static WinWindow FindWindow()
        {
            return WinWindow.Find(WindowClassName, WindowName);
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
            // GlosSITarget's window must have started by now, should be ensured by WaitForWindowToStart().
            WinWindow window = FindWindow();

            if (window == null)
            {
                return;
            }

            try
            {
                // The below method is used instead of Process.CloseMainWindow()
                // because GlosSITarget is not guaranteed to have a main window.
                window.Close();
            }
            catch (InvalidOperationException ex)
            {
                logger.Warn(ex, ex.Message);
            }
        }

        /// <summary>
        /// Checks if a GlosSITarget process is currently running.
        /// </summary>
        /// <returns></returns>
        public static bool IsRunning()
        {
            using (Process process = GetRunning())
            {
                return process != null;
            }
        }

        /// <summary>
        /// Gets any currently running GlosSITarget process.
        /// </summary>
        /// <returns>The currently running GlosSITarget process, 
        /// or <c>null</c> if GlosSITarget is not currently running.</returns>
        public static Process GetRunning()
        {
            return ExtractSingleProcessFromArray(Process.GetProcessesByName(ProcessName));
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
