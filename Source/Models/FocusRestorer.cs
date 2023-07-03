using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GlosSIIntegration.Models
{
    class FocusRestorer
    {
        /// <summary>
        /// Sets the window focus to a process. 
        /// A virtual-key press is necessary to ensure that Windows allows the focus to be changed.
        /// </summary>
        /// <param name="processToBeFocused">The process to be focused.</param>
        /// <param name="key">The virtual-key code of the key to be pressed, 
        /// by default the left alt key (VK_LMENU).</param>
        /// <exception cref="InvalidOperationException">If setting the foreground window failed.</exception>
        public static void FocusProcess(Process processToBeFocused, byte key = 0xA4)
        {
            // Trick Windows to permit the usage of SetForegroundWindow().
            KeyboardEvent(key, 0x45, EXTENDEDKEY | 0, 0);
            KeyboardEvent(key, 0x45, EXTENDEDKEY | KEYUP, 0);

            if (!SetForegroundWindow(processToBeFocused.MainWindowHandle))
            {
                throw new InvalidOperationException("Setting foreground window failed.");
            }
        }

        /// <summary>
        /// Attempts to return focus stolen by a process to this application <b>asynchronously</b>.
        /// Will dispose of originalProc afterwards.
        /// Exceptions are merely logged.
        /// </summary>
        /// <param name="thiefProc">The process expected to steal focus.</param>
        /// <param name="originalProc">The process to return focus to.</param>
        /// <param name="timeout">The minimum timeout in milliseconds for the process to take focus.</param>
        public static void ReturnStolenFocusToProcess(Process thiefProc, Process originalProc, int timeout)
        {
            Task.Run(async () =>
            {
                try
                {
                    await WaitForStolenFocus(thiefProc, timeout).ConfigureAwait(false);
                    FocusProcess(originalProc);
                }
                // Since this is run asynchronously and focusing a window is hardly all that important,
                // simply log any exception.
                catch (Exception ex)
                {
                    Playnite.SDK.LogManager.GetLogger().Warn(ex.Message);
                }
                finally
                {
                    originalProc.Dispose();
                }
            });
        }

        /// <summary>
        /// Waits for window focus to be stolen by the process <paramref name="thiefProc"/>.
        /// </summary>
        /// <param name="thiefProc">The process that will steal focus.</param>
        /// <param name="maxSleepTime">The maximum time spent sleeping while polling 
        /// for the main window of the process to take focus.</param>
        /// <exception cref="TimeoutException">If waiting for the process to steal focus took too long.
        /// </exception>
        /// <exception cref="InvalidOperationException">If the process exits before it is detected as 
        /// having stolen focus.</exception>
        public static async Task WaitForStolenFocus(Process thiefProc, int maxSleepTime)
        {
            const int delay = 20;
            int totalDelayTime = 0;

            while (thiefProc.MainWindowHandle == null || GetForegroundWindow() != thiefProc.MainWindowHandle)
            {
                if ((totalDelayTime += delay) > maxSleepTime)
                {
                    throw new TimeoutException("Process did not steal focus in time.");
                }
                await Task.Delay(delay).ConfigureAwait(false);
                thiefProc.Refresh();
            }
        }

        private const uint EXTENDEDKEY = 0x1, KEYUP = 0x2; // KEYEVENTF_EXTENDEDKEY and KEYEVENTF_KEYUP

        [DllImport("user32.dll", EntryPoint = "keybd_event")]
        private static extern void KeyboardEvent(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}
